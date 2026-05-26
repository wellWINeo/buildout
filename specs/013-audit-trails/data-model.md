# Data Model: MCP Audit Trails

**Feature**: `013-audit-trails` | **Date**: 2025-05-25

## Entities

### AuditEntry

Represents a single MCP tool invocation record. Immutable value type.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier (UUID v7 for time-ordering). |
| `ToolName` | `string` | MCP tool name (e.g., `"get_page_markdown"`, `"update_page"`). |
| `SessionId` | `string?` | `Mcp-Session-Id` header value from the HTTP transport. Null for stdio (should not occur if filter is HTTP-only). |
| `Timestamp` | `DateTimeOffset` | UTC timestamp of invocation start. |
| `Parameters` | `string` | Serialized JSON of tool parameters, truncated to `MaxParameterLength`. |
| `Outcome` | `AuditOutcome` | Enum: `Success` or `Failure`. |
| `Duration` | `TimeSpan` | Wall-clock duration of the tool call. |
| `ErrorDetails` | `string?` | Error message on failure. Null on success. Truncated to `MaxParameterLength`. |

### AuditOutcome (enum)

```
Success = 0
Failure = 1
```

### AuditOptions (configuration)

Bound to `Audit` configuration section.

| Property | Type | Default | Validation |
|----------|------|---------|------------|
| `Enabled` | `bool` | `false` | — |
| `Provider` | `string?` | `null` | Required when `Enabled=true`. Must be `"sqlite"` or `"postgresql"`. |
| `SqlitePath` | `string?` | `null` | Required when `Provider="sqlite"`. Non-empty, valid file path. |
| `ConnectionString` | `string?` | `null` | Required when `Provider="postgresql"`. Non-empty. MUST NOT be logged or echoed (secret). |
| `MaxParameterLength` | `int` | `10000` | Must be `> 0`. |

## Database Schema (Version 1)

Applies identically to both SQLite and PostgreSQL (with type adjustments noted).

```sql
CREATE TABLE IF NOT EXISTS audit_entries (
    id              TEXT NOT NULL PRIMARY KEY,           -- UUID v7 as text
    tool_name       TEXT NOT NULL,                       -- e.g. "get_page_markdown"
    session_id      TEXT,                                -- Mcp-Session-Id (nullable for safety)
    timestamp       TEXT NOT NULL,                       -- ISO 8601 UTC
    parameters      TEXT NOT NULL DEFAULT '{}',          -- JSON, truncated
    outcome         INTEGER NOT NULL,                    -- 0=Success, 1=Failure
    duration_ms     INTEGER NOT NULL,                    -- milliseconds
    error_details   TEXT                                 -- null on success
);

CREATE INDEX IF NOT EXISTS idx_audit_entries_timestamp ON audit_entries (timestamp);
CREATE INDEX IF NOT EXISTS idx_audit_entries_tool_name ON audit_entries (tool_name);
CREATE INDEX IF NOT EXISTS idx_audit_entries_session_id ON audit_entries (session_id);
```

> Schema versioning is managed by FluentMigrator's `VersionInfo` table — no `schema_version` column needed.

**PostgreSQL type adjustments**:
- `id` → `TEXT` (or `UUID` — using TEXT for consistency with SQLite)
- `timestamp` → `TIMESTAMPTZ`
- `outcome` → `SMALLINT`
- `duration_ms` → `BIGINT`

## Relationships

- `AuditEntry` has no foreign keys — it is an append-only log.
- `AuditOptions` is a standalone configuration shape with no relationships to other entities.

## State Transitions

None. Audit entries are append-only. No update or delete operations are in scope.

## Interfaces

### IAuditTrail (Buildout.Core)

```csharp
namespace Buildout.Core.Audit;

public interface IAuditTrail
{
    Task RecordEntryAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
```

- `NullAuditTrail`: No-op implementation (returns completed task). Registered when `Audit:Enabled=false`.
- `Linq2DbAuditTrail`: Persists via linq2db `DataConnection` (works with both SQLite and PostgreSQL). Registered when `Audit:Enabled=true`.

## Parameter Truncation

When `AuditEntry.Parameters` or `ErrorDetails` exceeds `MaxParameterLength`:
1. Truncate to `MaxParameterLength - 3` characters.
2. Append `"..."` suffix.
3. The resulting string is at most `MaxParameterLength` characters.

Truncation is performed by a static helper `AuditEntry.Truncate(string? value, int maxLength)` — pure function, testable in isolation.
