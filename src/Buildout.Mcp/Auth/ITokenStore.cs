using Buildout.Core.Auth;

namespace Buildout.Mcp.Auth;

public interface ITokenStore
{
    Task<(McpTokenRecord record, string rawToken)> CreateTokenAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<McpTokenRecord>> ListTokensAsync(CancellationToken ct = default);
    Task<bool> RevokeTokenAsync(Guid tokenId, CancellationToken ct = default);
    Task<McpTokenRecord?> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task MapTokenAsync(Guid tokenId, Guid buildinKeyId, CancellationToken ct = default);
    Task<BuildinKeyRecord> CreateBuildinKeyAsync(string name, string keyValue, CancellationToken ct = default);
    Task<IReadOnlyList<BuildinKeyRecord>> ListBuildinKeysAsync(CancellationToken ct = default);
}

public sealed record McpTokenRecord(Guid Id, string Name, string TokenHash, Guid? BuildinKeyId, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

public sealed record BuildinKeyRecord(Guid Id, string Name, string KeyValue, DateTimeOffset CreatedAt);