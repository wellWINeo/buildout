using Buildout.Core.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.Mcp.Auth;

public sealed class ProxyAuthenticator : IRequestAuthenticator
{
    private readonly string _globalBotToken;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<ProxyAuthenticator> _logger;

    public ProxyAuthenticator(IConfiguration configuration, ITokenStore tokenStore, ILogger<ProxyAuthenticator> logger)
    {
        _globalBotToken = configuration.GetValue<string>("BotToken") ?? string.Empty;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<AuthResult> AuthenticateAsync(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Authorization header is missing or invalid");
            return AuthResult.Failure("Authorization header is required");
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        var tokenRecord = await _tokenStore.ValidateTokenAsync(token);

        if (tokenRecord == null || tokenRecord.RevokedAt.HasValue)
        {
            _logger.LogWarning("Invalid or revoked token provided");
            return AuthResult.Failure("Invalid or revoked token");
        }

        return AuthResult.Success(_globalBotToken, tokenRecord.Name);
    }
}