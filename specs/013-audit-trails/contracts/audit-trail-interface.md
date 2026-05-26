# Contract: IAuditTrail Interface

**Feature**: `013-audit-trails` | **Date**: 2025-05-25

## Location

`src/Buildout.Core/Audit/IAuditTrail.cs`

## Signature

```csharp
namespace Buildout.Core.Audit;

public interface IAuditTrail
{
    Task RecordEntryAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
```

## Contract

### RecordEntryAsync

Persists an `AuditEntry` to the configured audit trail store.

**Preconditions**:
- `entry` is a valid `AuditEntry` with all required fields populated.

**Postconditions**:
- On success: entry is durably persisted. No return value.
- On failure: the failure is logged via `ILogger`. The exception MUST NOT propagate to the caller. The method returns a completed task.

**Thread safety**: Implementations MUST be safe for concurrent calls from multiple tool invocations.

**Performance**: The method SHOULD return quickly (fire-and-forget). The caller (audit filter) does not await the persistence result before responding to the MCP client.

## Implementations

| Class | Project | Registered When |
|-------|---------|-----------------|
| `NullAuditTrail` | `Buildout.Mcp` | `Audit:Enabled=false` (default) |
| `Linq2DbAuditTrail` | `Buildout.Mcp` | `Audit:Enabled=true` (works with both SQLite and PostgreSQL via linq2db `DataConnection`) |

## DI Registration

Registered in `Buildout.Mcp` via `AddAuditTrail(this IServiceCollection, IConfiguration)` extension method, following the existing pattern in `ServiceCollectionExtensions.cs`.

```csharp
public static IServiceCollection AddAuditTrail(this IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<AuditOptions>()
        .Bind(configuration.GetSection("Audit"))
        .ValidateOnStart();
    services.AddSingleton<IValidateOptions<AuditOptions>, AuditOptionsValidator>();

    services.AddSingleton<IAuditTrail>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<AuditOptions>>().Value;
        if (!opts.Enabled) return new NullAuditTrail();

        // Register linq2db DataConnection with provider-specific configuration
        services.AddLinqToDBContext<AuditDataConnection>((provider, options) =>
        {
            var auditOpts = provider.GetRequiredService<IOptions<AuditOptions>>().Value;
            switch (auditOpts.Provider)
            {
                case "sqlite":
                    options.UseSQLite($"Data Source={auditOpts.SqlitePath}");
                    break;
                case "postgresql":
                    options.UsePostgreSQL(auditOpts.ConnectionString);
                    break;
            }
        });

        // Run FluentMigrator migrations
        var runner = sp.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();

        return new Linq2DbAuditTrail(
            sp.GetRequiredService<IDataContext>(),
            sp.GetRequiredService<ILogger<Linq2DbAuditTrail>>());
    });
    return services;
}
```
