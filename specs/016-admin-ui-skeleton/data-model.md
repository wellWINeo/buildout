# Data Model: Management UI Skeleton

**Feature**: 016-admin-ui-skeleton  
**Date**: 2026-06-01

---

## Entities

### ApiKey

Represents a credential issued for API access.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Human-readable label, e.g. "CI Bot Key" |
| `Status` | `ApiKeyStatus` | Enum: `Active`, `Revoked` |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `LastUsedAt` | `DateTimeOffset?` | Last successful use; null if never used |

**Validation rules (mock layer)**:
- `Name` must be non-empty.
- `CreatedAt` must be in the past.

**State transitions**:
```
Active ──revoke──→ Revoked
```
*(Not implemented in the skeleton — status is display-only.)*

---

### AuditLogEntry

Represents a recorded administrative or system event.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Unique identifier |
| `Actor` | `string` | Who performed the action, e.g. "admin@example.com" |
| `Action` | `string` | What was done, e.g. "CreatePage", "RevokeKey" |
| `Resource` | `string` | Affected resource identifier, e.g. page UUID or key name |
| `Timestamp` | `DateTimeOffset` | When the event occurred |
| `Details` | `string?` | Optional free-text context |

**Ordering**: Entries are displayed in descending `Timestamp` order (newest first).

---

## Mock Data Contracts

### IApiKeyService

```csharp
public interface IApiKeyService
{
    IReadOnlyList<ApiKey> GetAll();
}
```

Registered as `Singleton`. Returns a fixed hardcoded list of ≥5 `ApiKey` records covering both `Active` and `Revoked` statuses and varied dates.

### IAuditLogService

```csharp
public interface IAuditLogService
{
    IReadOnlyList<AuditLogEntry> GetAll();
}
```

Registered as `Singleton`. Returns a fixed hardcoded list of ≥10 `AuditLogEntry` records spanning the past 30 days, covering several different `Action` values.

---

## Enumerations

```csharp
public enum ApiKeyStatus { Active, Revoked }
```

---

## Relationships

```
AdminUI
  └─ IApiKeyService  ──returns──→  ApiKey[]
  └─ IAuditLogService ─returns──→  AuditLogEntry[]
```

No foreign-key or cross-entity relationships exist in the skeleton; entities are independent lists.
