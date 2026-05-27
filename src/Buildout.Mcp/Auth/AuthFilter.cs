using Buildout.Core.Auth;
using Buildout.Core.Buildin.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.Mcp.Auth;

public sealed class AuthFilter : IConfigureOptions<ModelContextProtocol.Server.McpServerOptions>
{
    private readonly IRequestAuthenticator _authenticator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthFilter> _logger;

    public AuthFilter(
        IRequestAuthenticator authenticator,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthFilter> logger)
    {
        _authenticator = authenticator;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public void Configure(ModelContextProtocol.Server.McpServerOptions options)
    {
        options.Filters.Request.CallToolFilters.Add(CreateHandler);
    }

    private ModelContextProtocol.Server.McpRequestHandler<ModelContextProtocol.Protocol.CallToolRequestParams, ModelContextProtocol.Protocol.CallToolResult> CreateHandler(
        ModelContextProtocol.Server.McpRequestHandler<ModelContextProtocol.Protocol.CallToolRequestParams, ModelContextProtocol.Protocol.CallToolResult> next)
    {
        return async (context, cancellationToken) =>
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return await next(context, cancellationToken);
            }

            var authorizationHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            var authResult = await _authenticator.AuthenticateAsync(authorizationHeader);

            if (!authResult.IsAuthenticated)
            {
                _logger.LogWarning("Authentication failed for tool {ToolName}: {Error}", context.Params?.Name, authResult.ErrorMessage);
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                throw new UnauthorizedAccessException(authResult.ErrorMessage ?? "Authentication failed");
            }

            using var _ = ContextualTokenProvider.OverrideToken(authResult.ResolvedBotToken!);
            if (!string.IsNullOrEmpty(authResult.TokenIdentity))
            {
                httpContext.Items["AuthIdentity"] = authResult.TokenIdentity;
            }

            return await next(context, cancellationToken);
        };
    }
}