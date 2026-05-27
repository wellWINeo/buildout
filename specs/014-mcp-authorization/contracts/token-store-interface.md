# Contract: ITokenStore Interface

**Feature**: `014-mcp-authorization` | **Date**: 2025-05-27

## Location

`src/Buildout.Mcp/Auth/ITokenStore.cs`

## Signature

```csharp
namespace Buildout.Mcp.Auth;

public interface ITokenStore
{
    Task<McpTokenRecord> CreateTokenAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<McpTokenRecord>> ListTokensAsync(CancellationToken ct = default);
    Task<bool> RevokeTokenAsync(Guid tokenId, CancellationToken ct = default);
    Task<McpTokenRecord?> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task MapTokenAsync(Guid tokenId, Guid buildinKeyId, CancellationToken ct = default);
    Task<BuildinKeyRecord> CreateBuildinKeyAsync(string name, string keyValue, CancellationToken ct = default);
    Task<IReadOnlyList<BuildinKeyRecord>> ListBuildinKeysAsync(CancellationToken ct = default);
}
```

## Record Types

```csharp
public record McpTokenRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string TokenHash { get; init; }
    public Guid? BuildinKeyId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public string? Metadata { get; init; }
}

public record BuildinKeyRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string KeyValue { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

## Contract

### CreateTokenAsync

Generates a new MCP token, hashes it, stores the hash, and returns the record. The raw token value is included in `McpTokenRecord` ONLY at creation time and MUST NOT be stored or retrievable later.

**Postconditions**: Token is active (`RevokedAt` is null). `TokenHash` is the SHA-256 hex of the raw token.

### ListTokensAsync

Returns all tokens (active and revoked), ordered by creation date descending.

### RevokeTokenAsync

Sets `RevokedAt` on the token. Returns `true` if the token was found and revoked, `false` if not found or already revoked. Idempotent.

### ValidateTokenAsync

Looks up the token by its SHA-256 hash. Returns the token record if found and not revoked. Returns null if not found, revoked, or hash doesn't match.

**Performance**: MUST complete in <5ms (SC-004). Indexed lookup on `token_hash`.

### MapTokenAsync

Sets `BuildinKeyId` on the token. Used in `mapped` mode to link a token to a Buildin Bot API key. Throws if the token or key doesn't exist.

### CreateBuildinKeyAsync

Stores a new Buildin Bot API key. The key value is stored in plaintext (must be retrievable to send as Bearer token).

### ListBuildinKeysAsync

Returns all Buildin keys, ordered by creation date.

## Implementation

`AdoNetTokenStore` — follows the same ADO.NET pattern as `AdoNetAuditTrail` (feature 013). Uses `Microsoft.Data.Sqlite` for SQLite and `Npgsql` for PostgreSQL.

## CLI Commands

The token store is accessed by CLI commands (not MCP tools):

| Command | Maps to |
|---------|---------|
| `auth token create --name <name>` | `CreateTokenAsync` |
| `auth token list` | `ListTokensAsync` |
| `auth token revoke --id <guid>` | `RevokeTokenAsync` |
| `auth token map --token-id <guid> --key-id <guid>` | `MapTokenAsync` |

CLI commands are only applicable when `Auth:Mode` is `proxy` or `mapped`. When mode is `none` or `passthrough`, the commands inform the user and exit.
