# Research: MCP Authorization Modes

**Feature**: `014-mcp-authorization` | **Date**: 2025-05-27

## R1 — Per-Request Token Swap via `AsyncLocal`-Backed Delegating Provider

**Decision**: Introduce `ContextualTokenProvider : BaseBearerTokenAuthenticationProvider` in `Buildout.Core/Buildin/Authentication/` that wraps an `AsyncLocal<string?>` for per-request token override.

**Rationale**: Kiota's `HttpClientRequestAdapter` calls `IAuthenticationProvider.AuthenticateRequestAsync` on every outbound HTTP call. The `BotBuildinClient` is a singleton, but the auth provider reference it holds is called per-request. By backing the provider with `AsyncLocal<string?>`, each concurrent request flow gets its own token copy — no race conditions. The `IHttpContextAccessor` alternative was rejected because it would require `Buildout.Core` to reference ASP.NET Core, violating the core/presentation separation principle.

**Implementation**: `ContextualTokenProvider.OverrideToken(string)` returns an `IDisposable` scope. The auth filter wraps each MCP tool call with `using var _ = ContextualTokenProvider.OverrideToken(resolvedToken)`.

**Alternatives considered**:
- Scoped `IBuildinClient` — rejected: wasteful (new `HttpClientRequestAdapter` + generated API client per request), doesn't compose with singleton cache.
- `IHttpContextAccessor` in Core — rejected: couples `Buildout.Core` to ASP.NET Core.

## R2 — Token Hashing: SHA-256 (No Salt, No New Dependencies)

**Decision**: Use `System.Security.Cryptography.SHA256` from the .NET BCL. No salt. No new NuGet packages.

**Rationale**: MCP tokens are server-generated high-entropy strings (UUIDs or random hex, 128+ bits). Key-derivation functions (BCrypt, Argon2, PBKDF2) protect low-entropy passwords — that threat model does not apply. SHA-256's preimage resistance (2^256) makes recovery computationally infeasible. Salt adds nothing when inputs are already unique and high-entropy. The spec says "standard one-way hash" — SHA-256 is the canonical interpretation.

**Implementation**: Static `TokenHasher` class in `Buildout.Mcp/Auth/` with `Hash(string) → string` (lowercase hex) and `Verify(string token, string storedHash) → bool` (timing-safe via `CryptographicOperations.FixedTimeEquals`).

**Alternatives considered**:
- BCrypt/Argon2 — rejected: unnecessary computational cost for high-entropy inputs; would require new NuGet packages.
- HMAC-SHA-256 with a server key — rejected: adds a secret to manage with no meaningful security benefit over plain SHA-256 for random inputs.

## R3 — FluentMigrator: Shared Runner, Migration Number 002

**Decision**: Auth tables use `[Migration(2)]`. FluentMigrator runner is shared with audit trails — both `AddAuditTrail()` and `AddAuth()` call `AddFluentMigratorCore().ConfigureRunner(...)`, which is idempotent (`TryAdd` patterns). The `ScanIn(assembly).For.Migrations()` call discovers all migrations in the `Buildout.Mcp` assembly regardless of namespace.

**Rationale**: The spec requires "same database as audit trails (shared connection, same SQLite file / PostgreSQL database)." A single runner calling `MigrateUp()` runs all migrations in version order (1, then 2). No assembly scanning conflict.

**Gotcha — migration gate in Program.cs**: The current condition `if (isHttpTransport && auditOptions.Enabled)` only runs migrations when audit is enabled. If auth is enabled (`proxy`/`mapped`) but audit is not, migrations never run. Fix: broaden the gate to `if (isHttpTransport && (auditOptions.Enabled || authNeedsDb))` where `authNeedsDb` is true when `Auth:Mode` is `proxy` or `mapped`.

**Alternatives considered**:
- Separate runner per feature — rejected: FluentMigrator's `TryAdd` makes separate registrations converge to the same runner anyway; explicit sharing is clearer.
- Separate database — rejected: spec says "same database."

## R4 — Table Schema: FK Column on `mcp_tokens` (No Mapping Table)

**Decision**: Use a nullable `buildin_key_id` FK on `mcp_tokens` instead of a separate `token_key_mappings` table.

**Rationale**: The spec says "One MCP token maps to exactly one Buildin Bot API Key." A separate mapping table adds a join with no benefit. The nullable FK supports:
- `proxy` mode: `buildin_key_id` is NULL (uses global key from config)
- `mapped` mode: `buildin_key_id` points to the mapped key
- Multiple tokens can point to the same key (N:1)

**Tables**: `mcp_tokens` (id, name, token_hash, buildin_key_id nullable FK, created_at, revoked_at, metadata), `buildin_keys` (id, name, key_value, created_at).

**Alternatives considered**:
- Separate `token_key_mappings` table — rejected: adds a join for a 1:1 relationship. The spec says "true N:N" but in practice it's N:1 (many tokens can share one key, one token maps to exactly one key).

## R5 — Cache Interaction with Multi-Key Modes

**Decision**: When `Auth:Mode` is `passthrough` or `mapped`, page read caching MUST be disabled or made key-aware. The simplest correct approach: when auth mode is not `none` or `proxy`, register `NullPageReadCache` instead of `PageReadCache`, bypassing the cache entirely.

**Rationale**: `CachingPageContentProvider` uses `pageId` as the cache key with no token identity. In `passthrough` mode, each request may use a different Buildin Bot API key (different workspace). In `mapped` mode, different MCP tokens may map to different Buildin keys. Without key-awareness, token A's page fetch could be served from cache to token B, leaking data across workspaces.

Since `none` and `proxy` modes use a single global key, the cache is safe in those modes. Disabling the cache for multi-key modes is the minimal correct fix. A key-aware cache can be introduced in a future feature if performance requires it.

**Alternatives considered**:
- Include resolved token hash in cache key — rejected: changes the `IPageReadCache` interface, affecting multiple callers for an edge case.
- Per-token cache instances — rejected: singleton registration doesn't support this without significant DI restructuring.

## R6 — `AuthOptions` Shares Provider/ConnectionString with `AuditOptions`

**Decision**: `AuthOptions` has its own `Provider`, `SqlitePath`, and `ConnectionString` properties. When audit and auth are both enabled, they MUST use the same database (spec requirement). The options validator does NOT enforce this — it validates that the individual config is well-formed. The migration runner's `WithGlobalConnectionString()` uses the value from whichever feature registers first.

**Rationale**: Both features need their own options for independent enablement (audit-only, auth-only, both). The spec's shared-database constraint is a deployment concern, not a code constraint. Configuration documentation should note: "When both Audit and Auth are enabled, they must point to the same database."

## R7 — CLI Token Commands Under `auth` Branch

**Decision**: Use Spectre.Console.Cli's branch pattern: `auth token create`, `auth token list`, `auth token revoke`, `auth token map`. New settings base `AuthSettings` inheriting from `BuildoutCommandSettings`.

**Rationale**: Groups all auth commands under a single prefix. The `auth` branch pattern follows the existing `db view` and `skills install/remove` patterns in `Program.cs`. The `map` command is only meaningful for `mapped` mode — it should validate the current auth mode and inform the user if not applicable (spec edge case).

**Alternatives considered**:
- Flat commands (`token-create`, `token-list`) — rejected: pollutes the top-level command namespace.
- No CLI commands, database-only management — rejected: spec FR-008 requires CLI commands.

## R8 — Auth Filter Integration with Audit Trail

**Decision**: The auth filter runs BEFORE the audit filter in the MCP pipeline. When both are enabled, the audit entry records the MCP token identity (not the Buildin Bot API key) in the `SessionId` or a new `AuthIdentity` field on `AuditEntry`.

**Rationale**: FR-015 says "each audit entry MUST reference the MCP token identity used to authenticate the request." This requires a minor extension to `AuditEntry` — adding an `AuthIdentity` field. The auth filter sets this on the `HttpContext.Items` collection, and the audit filter reads it.

**Implementation**: Add `string? AuthIdentity` to `AuditEntry`. The auth filter stores the resolved token identity in `HttpContext.Items["AuthIdentity"]`. The audit filter reads it when constructing the entry. In `none` and `passthrough` modes, the field is empty.

**Alternatives considered**:
- Reuse `SessionId` for auth identity — rejected: conflates two distinct concepts (session ID vs. authenticated identity).
- Pass auth identity through `AsyncLocal` — rejected: `HttpContext.Items` is the natural per-request bag for ASP.NET, and the audit filter already accesses `IHttpContextAccessor`.
