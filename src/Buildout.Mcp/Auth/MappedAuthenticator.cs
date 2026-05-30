using Buildout.Core.Auth;
using Microsoft.Extensions.Logging;

namespace Buildout.Mcp.Auth;

public sealed class MappedAuthenticator : IRequestAuthenticator
{
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<MappedAuthenticator> _logger;

    public MappedAuthenticator(ITokenStore tokenStore, ILogger<MappedAuthenticator> logger)
    {
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

        if (!tokenRecord.BuildinKeyId.HasValue)
        {
            _logger.LogWarning("Token has no mapped Buildin key");
            return AuthResult.Failure("Token has no mapped Buildin key");
        }

        var buildinKey = await _tokenStore.ListBuildinKeysAsync();
        var key = buildinKey.FirstOrDefault(k => k.Id == tokenRecord.BuildinKeyId.Value);

        if (key == null)
        {
            _logger.LogWarning("Mapped Buildin key not found");
            return AuthResult.Failure("Mapped Buildin key not found");
        }

        return AuthResult.Success(key.KeyValue, tokenRecord.Name);
    }
}