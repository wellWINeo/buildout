using Buildout.Core.Auth;
using Microsoft.Extensions.Logging;

namespace Buildout.Mcp.Auth;

public sealed class PassthroughAuthenticator : IRequestAuthenticator
{
    private readonly ILogger<PassthroughAuthenticator> _logger;

    public PassthroughAuthenticator(ILogger<PassthroughAuthenticator> logger)
    {
        _logger = logger;
    }

    public Task<AuthResult> AuthenticateAsync(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Authorization header is missing or invalid");
            return Task.FromResult(AuthResult.Failure("Authorization header is required"));
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Authorization header contains empty token value");
            return Task.FromResult(AuthResult.Failure("Authorization header contains empty token value"));
        }

        return Task.FromResult(AuthResult.Success(token, null));
    }
}