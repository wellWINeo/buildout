# Data Model: MCP Authorization Modes

**Feature**: `014-mcp-authorization` | **Date**: 2025-05-27

## Entities

### AuthMode (enum)

The authorization mode for the MCP server.

```
None = 0        # No MCP auth; global BotToken (default, current behavior)
Passthrough = 1 # Client provides Buildin Bot API key per request
Proxy = 2       # Client provides MCP token; server validates and uses global BotToken
Mapped = 3      # Client provides MCP token; server validates and resolves mapped BotToken
```

### AuthOptions (configuration)

Bound to `Auth` configuration section.

| Property | Type | Default | Validation |
|----------|------|---------|------------|
| `Mode` | `AuthMode` | `None` | Must be a valid `AuthMode` value. |
| `Provider` | `string?` | `null` | Required when `Mode` is `Proxy` or `Mapped`. Must be `"sqlite"` or `"postgresql"`. |
| `SqlitePath` | `string?` | `null` | Required when `Provider="sqlite"`. Non-empty, valid file path. |
| `ConnectionString` | `string?` | `null` | Required when `Provider="postgresql"`. Non-empty. MUST NOT be logged or echoed (secret). |

### IRequestAuthenticator (interface — Buildout.Core)

Resolves authentication for an incoming MCP request.

| Method | Return | Description |
|--------|--------|-------------|
| `AuthenticateAsync(string? authorizationHeader)` | `Task<AuthResult>` | Validates the incoming credentials and returns the result with the resolved Buildin Bot API key. |

### AuthResult (record — Buildout.Core)

| Field | Type | Description |
|-------|------|-------------|
| `IsAuthenticated` | `bool` | Whether authentication succeeded. |
| `ResolvedBotToken` | `string?` | The Buildin Bot API key to use for outbound calls. Null if authentication failed. |
| `TokenIdentity` | `string?` | Human-readable identity of the authenticated MCP token (for audit trail). Null for `none`/`passthrough` modes. |
| `ErrorMessage` | `string?` | Error description if authentication failed. Null if successful. |

**Factory methods**:

- `Success(string botToken, string? identity)` → `AuthResult` with `IsAuthenticated = true`.
- `Failure(string error)` → `AuthResult` with `IsAuthenticated = false`.

### McpToken (database entity)

An operator-issued credential used by MCP clients to authenticate.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier. |
| `Name` | `string` | Human-readable label for the token (max 255 chars). |
| `TokenHash` | `string` | SHA-256 hash of the token value (64-char lowercase hex). |
| `BuildinKeyId` | `Guid?` | FK to `buildin_keys`. Null in `proxy` mode (uses global key). Non-null in `mapped` mode. |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp. |
| `RevokedAt` | `DateTimeOffset?` | Revocation timestamp. Null if active. |
| `Metadata` | `string?` | Optional JSON metadata (default `"{}"`). |

### BuildinKey (database entity)

A Buildin Bot API key stored for use in `mapped` mode.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier. |
| `Name` | `string` | Human-readable label (max 255 chars). |
| `KeyValue` | `string` | The Buildin Bot API key in plaintext (must be retrievable to send as Bearer token). Max 1024 chars. |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp. |

## Database Schema (Version 2 — extends Version 1 from feature 013)

```sql
CREATE TABLE IF NOT EXISTS buildin_keys (
    id              TEXT NOT NULL PRIMARY KEY,
    name            TEXT NOT NULL,
    key_value       TEXT NOT NULL,
    created_at      TEXT NOT NULL                    -- ISO 8601 UTC
);

CREATE INDEX IF NOT EXISTS idx_buildin_keys_name ON buildin_keys (name);

CREATE TABLE IF NOT EXISTS mcp_tokens (
    id              TEXT NOT NULL PRIMARY KEY,
    name            TEXT NOT NULL,
    token_hash      TEXT NOT NULL,                   -- SHA-256 hex
    buildin_key_id  TEXT,                            -- nullable FK → buildin_keys.id
    created_at      TEXT NOT NULL,                   -- ISO 8601 UTC
    revoked_at      TEXT,                            -- ISO 8601 UTC, null if active
    metadata        TEXT DEFAULT '{}',
    FOREIGN KEY (buildin_key_id) REFERENCES buildin_keys(id)
);

CREATE INDEX IF NOT EXISTS idx_mcp_tokens_token_hash ON mcp_tokens (token_hash);
```

**PostgreSQL type adjustments**:
- `id` → `UUID`
- `created_at` / `revoked_at` → `TIMESTAMPTZ`
- `buildin_key_id` → `UUID`

## Relationships

- `McpToken.BuildinKeyId` → `BuildinKey.Id` (nullable FK, N:1 — many tokens can share one key)
- `McpToken` has no relationship to `AuditEntry` — the audit trail references token identity via `AuditEntry.AuthIdentity` (a string field, not an FK)

## State Transitions

### McpToken

```
[Created] ──revoke──→ [Revoked]
```

- Tokens start in the **Created** state (`RevokedAt` is null).
- Revocation is irreversible — sets `RevokedAt` to current UTC timestamp.
- There is no reactivation.

## AuditEntry Extension

Add `string? AuthIdentity` field to `AuditEntry` (from feature 013):

| Field | Type | Description |
|-------|------|-------------|
| `AuthIdentity` | `string?` | The MCP token identity for audit. Empty for `none`/`passthrough` modes. Set by auth filter. |

Database migration: `ALTER TABLE audit_entries ADD COLUMN auth_identity TEXT DEFAULT NULL;`

## Interfaces

### IRequestAuthenticator (Buildout.Core)

```csharp
namespace Buildout.Core.Auth;

public interface IRequestAuthenticator
{
    Task<AuthResult> AuthenticateAsync(string? authorizationHeader);
}
```

### ITokenStore (Buildout.Mcp)

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

### ContextualTokenProvider (Buildout.Core)

```csharp
namespace Buildout.Core.Buildin.Authentication;

public sealed class ContextualTokenProvider : BaseBearerTokenAuthenticationProvider
{
    public static IDisposable OverrideToken(string token);
}
```

Swaps the Buildin Bot API key used for outbound calls within an async scope. Falls back to the default token when no override is active.

## Token Hashing

- Algorithm: SHA-256 (BCL `System.Security.Cryptography.SHA256`)
- Input: UTF-8 bytes of the raw token string
- Output: 64-character lowercase hex string
- Verification: Timing-safe comparison via `CryptographicOperations.FixedTimeEquals`
- No salt (high-entropy input makes salt unnecessary)
