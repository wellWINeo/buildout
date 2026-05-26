# Research: MCP Audit Trails

**Feature**: `013-audit-trails` | **Date**: 2025-05-25

## R1: Cross-cutting Audit Mechanism

**Decision**: Use the C# MCP SDK's `AddCallToolFilter` request filter mechanism to intercept all `tools/call` invocations.

**Rationale**:
- The `ModelContextProtocol` NuGet package (v1.2.0) exposes `AddCallToolFilter` on `IMcpRequestFilterBuilder`, which accepts an `McpRequestFilter<CallToolRequestParams, CallToolResult>` delegate.
- This filter wraps every tool call — no need to modify individual tool handlers (7 existing handlers).
- The filter receives `CallToolRequestParams` (tool name + arguments dict) and can observe the result or exception.
- This is the idiomatic, SDK-supported way to add cross-cutting behavior to MCP tool calls.
- Registration via `builder.Services.AddMcpServer().WithRequestFilters(b => b.AddCallToolFilter(...))`.

**Alternatives considered**:
- **Per-handler wrapping**: Inject `IAuditTrail` into every tool handler. Rejected because it requires modifying all 7 handlers and any future handlers, violating DRY and increasing maintenance.
- **Custom middleware/ASP.NET Core middleware**: Rejected because it couples audit logic to ASP.NET Core pipeline rather than MCP-level semantics. The MCP filter is transport-agnostic within the HTTP boundary.
- **Decorator pattern on tool handlers**: Rejected because `McpServerToolType` attribute-based discovery doesn't support decorator registration naturally.

## R2: Transport Discrimination (HTTP vs stdio)

**Decision**: Register the audit filter only when HTTP transport is configured. When using stdio transport (current default), no filter is registered.

**Rationale**:
- The spec is explicit: FR-013 states audit trails MUST only record HTTP transport invocations.
- The C# MCP SDK (`ModelContextProtocol` 1.2.0) currently only supports stdio transport via `WithStdioServerTransport()`. HTTP transport support requires the `ModelContextProtocol.AspNetCore` package.
- The current `Program.cs` calls `.WithStdioServerTransport()` unconditionally. Adding HTTP transport is a prerequisite for this feature.
- Transport selection can be driven by configuration (e.g., `Transport:Type` = `stdio` | `http`). When `http` is selected AND audit is enabled, the audit filter is registered.
- The audit filter itself does not need to check transport type at runtime — it's simply not registered for stdio.

**Alternatives considered**:
- **Runtime check inside filter**: Check `McpServer.Transport` type at filter time. Rejected because the filter may not have clean access to transport metadata, and it's simpler to not register it.
- **Separate server entry points**: Two `Program.cs` variants. Rejected as over-engineering; configuration-driven branching is sufficient.

## R3: Session Identification (Mcp-Session-Id)

**Decision**: Extract `Mcp-Session-Id` from the MCP server's session context within the filter.

**Rationale**:
- Per the MCP Streamable HTTP transport spec, the server assigns a session ID during initialization and the client echoes it on all subsequent requests via the `Mcp-Session-Id` HTTP header.
- The C# MCP SDK's `McpServer` object exposes session metadata. The filter receives the `McpServer` instance via the filter context, which provides access to `SessionId`.
- This is the protocol-native identifier — no custom session mechanism needed (per spec clarification).

**Alternatives considered**:
- **HTTP header extraction via HttpContext**: Rejected because it couples audit to ASP.NET Core internals and may not be available in the MCP filter context.
- **Custom session tracking**: Rejected per spec clarification — the protocol-native header is sufficient.

## R4: Persistence — linq2db

**Decision**: Use `linq2db` as the data access layer for both SQLite and PostgreSQL.

**Rationale**:
- `linq2db` is a fast, lightweight, type-safe LINQ-based database access library supporting multiple providers via a single abstraction.
- A single `DataConnection` subclass per provider (`UseSQLite`, `UsePostgreSQL`) unifies the persistence code — no provider-specific raw SQL for inserts.
- `InsertAsync` on `ITable<AuditEntry>` gives async writes with compile-time type safety.
- Future features will expand schema and database usage (multiple tables, queries, joins). `linq2db` scales to that without a framework migration.
- POCO mapping via `[Table]`, `[Column]` attributes — no code-gen step required.

**Alternatives considered**:
- **Raw ADO.NET (`Microsoft.Data.Sqlite` + `Npgsql`)**: Rejected — duplicated provider-specific SQL for each backend. Future multi-table features would amplify the boilerplate.
- **EF Core**: Rejected — heavier abstraction, change-tracking overhead unnecessary for append-only writes, and slower bulk operations.
- **Dapper**: Rejected — still requires handwritten SQL per provider; adds a micro-ORM without solving the multi-provider SQL duplication problem.

## R5: Schema Migrations — FluentMigrator

**Decision**: Use `FluentMigrator` for database schema creation and versioned migrations.

**Rationale**:
- `FluentMigrator` provides a C# fluent API for defining schema changes as versioned migration classes — no raw DDL strings.
- Supports both SQLite (`FluentMigrator.Runner.SQLite`) and PostgreSQL (`FluentMigrator.Runner.Postgres`) via a single migration codebase.
- `VersionInfo` table tracks applied migrations automatically — no custom version-check logic needed.
- Future features can add new migrations incrementally without modifying existing schema-creation code.
- Run at startup via `IMigrationRunner.MigrateUp()` in the DI pipeline.

**Alternatives considered**:
- **Manual `ExecuteNonQuery` at startup**: Rejected — requires custom version-check logic, doesn't scale to multiple migrations, and duplicates DDL between SQLite and PostgreSQL.
- **EF Core migrations**: Rejected — couples migrations to the EF Core ORM, which isn't being used.
- **DbUp**: Viable alternative, but FluentMigrator's fluent C# API is more maintainable than embedded SQL scripts for a code-first team.

## R6: Async Write Model (Fire-and-Forget)

**Decision**: Audit writes are `Task.Run`-based fire-and-forget with error logging.

**Rationale**:
- FR-014 requires async writes relative to tool execution.
- The filter awaits the tool call, constructs the `AuditEntry`, then dispatches the persistence call via `Task.Run` (or `ConfigureAwait(false)` with `ContinueWith` for error handling).
- Audit write failures are caught and logged via `ILogger`; they never propagate to the tool caller (FR-006).
- A `Channel<AuditEntry>` with a single background consumer task is an alternative for backpressure, but adds complexity. Start with fire-and-forget; escalate to channel if needed.

**Alternatives considered**:
- **Channel<AuditEntry> with background consumer**: More robust for high-throughput scenarios. Deferred — fire-and-forget is simpler and sufficient for initial implementation.
- **Synchronous write**: Rejected per FR-014.

## R7: Schema Versioning

**Decision**: Use FluentMigrator's built-in `VersionInfo` table for schema version tracking. Each schema change is a versioned `[Migration(###)]` class.

**Rationale**:
- FluentMigrator maintains a `VersionInfo` table automatically — no custom version column or startup check logic.
- Migrations are applied via `IMigrationRunner.MigrateUp()` at startup, which skips already-applied migrations.
- Future features add new `[Migration]` classes without touching existing code.

## R8: Configuration Integration

**Decision**: `AuditOptions` bound to `Audit` section in configuration, following the existing pattern (`CacheOptions`, `TelemetryOptions`).

**Rationale**:
- Principle VII (Dual-Channel Configuration) requires all options on both JSON and env-var channels.
- Existing pattern: `services.AddOptions<AuditOptions>().Bind(configuration.GetSection("Audit")).ValidateOnStart()`.
- `AuditOptionsValidator` (IValidateOptions) fails fast at startup if `Enabled=true` but provider/path/connection is missing or invalid.
- `linq2db` context registered via `services.AddLinqToDBContext<AuditDataConnection>((sp, opts) => ...)` with provider-specific configuration.
- FluentMigrator runner registered and `MigrateUp()` called during startup to ensure schema exists.

**Configuration keys**:

| Key | Type | Default | Required | Validation | Env Var |
|-----|------|---------|----------|------------|---------|
| `Audit:Enabled` | `bool` | `false` | no | — | `Buildout__Audit__Enabled` |
| `Audit:Provider` | `string` | — | yes if enabled | `sqlite` or `postgresql` | `Buildout__Audit__Provider` |
| `Audit:SqlitePath` | `string` | — | yes if sqlite | non-empty, valid path | `Buildout__Audit__SqlitePath` |
| `Audit:ConnectionString` | `string` | — | yes if postgresql | non-empty | `Buildout__Audit__ConnectionString` |
| `Audit:MaxParameterLength` | `int` | `10000` | no | `> 0` | `Buildout__Audit__MaxParameterLength` |

## R9: HTTP Transport Addition (Prerequisite)

**Decision**: Add `ModelContextProtocol.AspNetCore` package and HTTP transport support to `Buildout.Mcp`, controlled by configuration.

**Rationale**:
- The current server only supports stdio (`WithStdioServerTransport()`). HTTP transport is required for audit trails per the spec.
- This is a prerequisite infrastructure change, not audit-specific logic. It adds the ability to select transport type at startup.
- Configuration key: `Transport:Type` = `stdio` (default) | `http`.

**Note**: This prerequisite may warrant a separate, smaller feature. For now, the minimal HTTP transport plumbing is included in this feature's scope as it's a hard dependency.
