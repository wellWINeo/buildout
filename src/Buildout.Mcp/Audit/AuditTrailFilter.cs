using System.Diagnostics;
using System.Text.Json;
using Buildout.Core.Audit;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;

namespace Buildout.Mcp.Audit;

public class AuditTrailFilter
{
    private readonly IAuditTrail _auditTrail;
    private readonly ILogger<AuditTrailFilter> _logger;

    public AuditTrailFilter(IAuditTrail auditTrail, ILogger<AuditTrailFilter> logger)
    {
        _auditTrail = auditTrail;
        _logger = logger;
    }

    public async Task<CallToolResult> ExecuteAsync(
        CallToolRequestParams request,
        RequestContext<CallToolRequestParams> context,
        Func<CallToolRequestParams, RequestContext<CallToolRequestParams>, Task<CallToolResult>> next)
    {
        var stopwatch = Stopwatch.StartNew();
        AuditOutcome outcome = AuditOutcome.Success;
        string? errorDetails = null;

        try
        {
            var result = await next(request, context);
            return result;
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

                var parametersDict = request.Arguments;
                var parametersJson = JsonSerializer.Serialize(parametersDict);
                var entry = new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    ToolName = request.Name,
                    SessionId = null,
                    Timestamp = DateTimeOffset.UtcNow,
                    Parameters = AuditEntry.Truncate(parametersJson, 10000),
                    Outcome = outcome,
                    Duration = stopwatch.Elapsed,
                    ErrorDetails = errorDetails
                };

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _auditTrail.RecordEntryAsync(entry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to record audit entry for tool {ToolName}", entry.ToolName);
                    }
                });
            }
    }
}