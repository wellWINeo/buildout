# Contract: Audit Configuration Schema

**Feature**: `013-audit-trails` | **Date**: 2025-05-25

## Location

`src/Buildout.Core/Audit/AuditOptions.cs`, `src/Buildout.Core/Audit/AuditOptionsValidator.cs`

## Configuration Keys

| Key | Type | Default | Required | Validation | Env Var |
|-----|------|---------|----------|------------|---------|
| `Audit:Enabled` | `bool` | `false` | no | — | `Buildout__Audit__Enabled` |
| `Audit:Provider` | `string` | `null` | yes if `Enabled=true` | `"sqlite"` or `"postgresql"` | `Buildout__Audit__Provider` |
| `Audit:SqlitePath` | `string` | `null` | yes if `Provider=sqlite` | Non-empty. Valid file path. | `Buildout__Audit__SqlitePath` |
| `Audit:ConnectionString` | `string` | `null` | yes if `Provider=postgresql` | Non-empty. MUST NOT be logged. | `Buildout__Audit__ConnectionString` |
| `Audit:MaxParameterLength` | `int` | `10000` | no | `> 0` | `Buildout__Audit__MaxParameterLength` |

## Validation Rules (AuditOptionsValidator)

`IValidateOptions<AuditOptions>` implementation. Validate `Validate(string? name, AuditOptions options)`:

1. If `Enabled=false`: return `ValidateResult.Success`. No further validation.
2. If `Enabled=true` and `Provider` is null/empty: return failure `"Audit:Provider is required when Audit:Enabled is true. Expected 'sqlite' or 'postgresql'."`.
3. If `Provider` is not `"sqlite"` or `"postgresql"`: return failure `"Audit:Provider must be 'sqlite' or 'postgresql'. Got: '{Provider}'."`.
4. If `Provider="sqlite"` and `SqlitePath` is null/empty: return failure `"Audit:SqlitePath is required when Audit:Provider is 'sqlite'."`.
5. If `Provider="postgresql"` and `ConnectionString` is null/empty: return failure `"Audit:ConnectionString is required when Audit:Provider is 'postgresql'."`.
6. If `MaxParameterLength <= 0`: return failure `"Audit:MaxParameterLength must be greater than 0. Got: {MaxParameterLength}."`.

## JSON Example

```json
{
  "Audit": {
    "Enabled": true,
    "Provider": "sqlite",
    "SqlitePath": "/var/lib/buildout/audit.db",
    "MaxParameterLength": 10000
  }
}
```

## Environment Variable Example

```bash
export Buildout__Audit__Enabled="true"
export Buildout__Audit__Provider="postgresql"
export Buildout__Audit__ConnectionString="Host=localhost;Database=buildout_audit;Username=audit;Password=secret"
export Buildout__Audit__MaxParameterLength="5000"
```

## docs/configuration.md Update

The `docs/configuration.md` key table MUST be updated in the same PR to include all 5 `Audit:*` keys, their types, defaults, required flags, validation rules, and env var forms.
