# Contract: Auth Configuration Schema

**Feature**: `014-mcp-authorization` | **Date**: 2025-05-27

## Configuration Keys

| Key | Type | Default | Required | Validation | Env Var Form |
|-----|------|---------|----------|------------|--------------|
| `Auth:Mode` | `string` | `none` | no | `none`, `passthrough`, `proxy`, or `mapped` | `Buildout__Auth__Mode` |
| `Auth:Provider` | `string` | — | yes if `Mode=proxy` or `Mode=mapped` | `sqlite` or `postgresql` | `Buildout__Auth__Provider` |
| `Auth:SqlitePath` | `string` | — | yes if `Provider=sqlite` | non-empty, valid file path | `Buildout__Auth__SqlitePath` |
| `Auth:ConnectionString` | `string` | — | yes if `Provider=postgresql` | non-empty | `Buildout__Auth__ConnectionString` |

## Startup Validation Rules

`AuthOptionsValidator : IValidateOptions<AuthOptions>` enforces:

1. `Mode` must be a valid `AuthMode` value (case-insensitive parse).
2. If `Mode` is `Proxy` or `Mapped`, `Provider` must be `"sqlite"` or `"postgresql"`.
3. If `Provider` is `"sqlite"`, `SqlitePath` must be non-empty.
4. If `Provider` is `"postgresql"`, `ConnectionString` must be non-empty.
5. If `Mode` is `None` or `Passthrough`, `Provider`/`SqlitePath`/`ConnectionString` are ignored (no validation).

Validation runs at startup via `ValidateOnStart()`. A misconfigured process refuses to start with a single human-readable error naming the offending key.

## JSON Examples

**None mode (default):**
```json
{
  "Auth": {
    "Mode": "none"
  }
}
```

**Passthrough mode:**
```json
{
  "Auth": {
    "Mode": "passthrough"
  }
}
```

**Proxy mode (SQLite):**
```json
{
  "Auth": {
    "Mode": "proxy",
    "Provider": "sqlite",
    "SqlitePath": "/path/to/auth.db"
  }
}
```

**Mapped mode (PostgreSQL):**
```json
{
  "Auth": {
    "Mode": "mapped",
    "Provider": "postgresql",
    "ConnectionString": "Host=localhost;Port=5432;Database=buildout;Username=user;Password=pass"
  }
}
```

## Environment Variable Examples

```bash
export Buildout__Auth__Mode="proxy"
export Buildout__Auth__Provider="sqlite"
export Buildout__Auth__SqlitePath="/path/to/auth.db"
```

## docs/configuration.md Update

The existing `docs/configuration.md` must be updated to add the `Auth:*` keys to the Configuration Keys table, the JSON examples, and the env-var examples. This satisfies Principle VII (Dual-Channel Configuration).

## Shared Database Note

When both `Audit` and `Auth` are enabled, they MUST point to the same database (same SQLite file or PostgreSQL database). The configuration allows independent settings for flexibility, but the documentation should note this constraint.
