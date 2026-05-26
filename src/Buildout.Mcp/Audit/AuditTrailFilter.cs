using System.Diagnostics;
using System.Text.Json;
using Buildout.Core.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Audit;

/// <summary>
/// Registers an MCP CallTool filter that records an <see cref="AuditEntry"/> for every
/// tool invocation. Implemented as <see cref="IConfigureOptions{McpServerOptions}"/> so
/// the filter is wired into the MCP pipeline through the normal DI options pipeline,
/// after the DI container is fully built — no intermediate <c>BuildServiceProvider</c>
/// required.
/// </summary>
public sealed class AuditTrailFilter : IConfigureOptions<McpServerOptions>
{
    private static readonly Action<ILogger, string, Exception?> s_failedToRecord =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, "AuditRecordFailed"),
            "Failed to record audit entry for tool {ToolName}");

    private readonly IAuditTrail _auditTrail;
    private readonly IOptions<AuditOptions> _auditOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditTrailFilter> _logger;

    public AuditTrailFilter(
        IAuditTrail auditTrail,
        IOptions<AuditOptions> auditOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditTrailFilter> logger)
    {
        _auditTrail = auditTrail;
        _auditOptions = auditOptions;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public void Configure(McpServerOptions options)
    {
        options.Filters.Request.CallToolFilters.Add(CreateHandler);
    }

    private McpRequestHandler<CallToolRequestParams, CallToolResult> CreateHandler(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (context, cancellationToken) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var outcome = AuditOutcome.Success;
            string? errorDetails = null;

            try
            {
                return await next(context, cancellationToken);
            }
            catch (Exception ex)
            {
                outcome = AuditOutcome.Failure;
                errorDetails = ex.Message;
                throw;
            }
            finally
            {
                stopwatch.Stop();

                var maxLen = _auditOptions.Value.MaxParameterLength;
                var sessionId = _httpContextAccessor.HttpContext?
                    .Request.Headers["Mcp-Session-Id"]
                    .FirstOrDefault();
                var parametersJson = JsonSerializer.Serialize(context.Params?.Arguments);
                var entry = new AuditEntry
                {
                    ToolName = context.Params?.Name ?? string.Empty,
                    SessionId = sessionId,
                    Parameters = AuditEntry.Truncate(parametersJson, maxLen),
                    Outcome = outcome,
                    Duration = stopwatch.Elapsed,
                    ErrorDetails = errorDetails is null ? null : AuditEntry.Truncate(errorDetails, maxLen),
                };

                var auditTrail = _auditTrail;
                var logger = _logger;
                // CancellationToken.None: the audit write must complete even if the
                // request's cancellation token is signalled after the tool returns.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await auditTrail.RecordEntryAsync(entry, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        s_failedToRecord(logger, entry.ToolName, ex);
                    }
                }, CancellationToken.None);
            }
        };
    }
}
