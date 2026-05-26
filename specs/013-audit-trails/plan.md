# Implementation Plan: MCP Audit Trails

**Branch**: `013-audit-trails` | **Date**: 2025-05-25 | **Spec**: `specs/013-audit-trails/spec.md`
**Input**: Feature specification from `/specs/013-audit-trails/spec.md`

## Summary

Add opt-in audit trail recording for every MCP tool invocation over HTTP transport. When enabled, each tool call is persisted as an `AuditEntry` record to either SQLite or PostgreSQL, capturing tool name, session ID, timestamp, parameters, outcome, duration, and error details. The feature is implemented as a cross-cutting MCP request filter (`AddCallToolFilter`) that intercepts all `tools/call` requests — zero impact when disabled (no-op `IAuditTrail` registered by default).

## Technical Context

**Language/Version**: C# 13 / .NET 10 (SDK-style projects, `net10.0`)
**Primary Dependencies**: `ModelContextProtocol` 1.2.0 (MCP SDK), `ModelContextProtocol.AspNetCore` (HTTP transport), `linq2db` (data access), `linq2db.SQLite` (SQLite provider), `linq2db.PostgreSQL` (PostgreSQL provider), `FluentMigrator` (schema migrations), `FluentMigrator.Runner.SQLite`, `FluentMigrator.Runner.Postgres`, `Microsoft.Extensions.Configuration` / `Options` / `DependencyInjection`
**Storage**: SQLite (via `linq2db.SQLite`) and PostgreSQL (via `linq2db.PostgreSQL`), schema managed by `FluentMigrator`
**Testing**: xUnit v3, NSubstitute, WireMock.Net (existing stack), `Testcontainers.PostgreSql` (PostgreSQL integration tests)
**Target Platform**: Server-side .NET (MCP server over HTTP transport)
**Project Type**: Library + MCP server extension (cross-cutting filter)
**Performance Goals**: <5ms average added latency per tool call when enabled; zero overhead when disabled
**Constraints**: Fire-and-forget async writes; audit failures must never block tool calls; HTTP transport only; PostgreSQL integration tests require Docker (Testcontainers)
**Scale/Scope**: Single-workspace audit trail; no querying/export UI in scope

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Core/Presentation Separation | PASS | `IAuditTrail` interface defined in `Buildout.Core`; concrete implementations (`Linq2DbAuditTrail`, `NullAuditTrail`) and data access layer (`AuditDataConnection`, `AuditEntryRecord`, migrations) live in `Buildout.Mcp`. Audit options/validator in `Buildout.Core`. |
| II. LLM-Friendly Output Fidelity | N/A | Audit trails are not part of LLM output; no Markdown conversion involved. |
| III. Bidirectional Round-Trip Testing | N/A | No block/Markdown conversion. |
| IV. Test-First Discipline | PASS | Unit tests for options validation, audit entry construction, parameter truncation. Integration tests for SQLite and PostgreSQL persistence. Migration verification tests. Filter integration test exercising the full MCP pipeline. |
| V. Buildin API Abstraction | N/A | Audit trails do not call buildin.ai. |
| VI. Non-Destructive Editing | N/A | Audit trails are read-only observers of tool calls. |
| VII. Dual-Channel Configuration | PASS | All audit options (`Audit:Enabled`, `Audit:Provider`, `Audit:SqlitePath`, `Audit:ConnectionString`, `Audit:MaxParameterLength`) exposed via both JSON config and `Buildout__Audit__*` env vars. `docs/configuration.md` updated. `IValidateOptions<AuditOptions>` for fail-fast. |
| VIII. Skills & Prompts Parity | N/A | No new CLI commands or MCP tools added. |

**Gate result**: PASS — no violations. All non-N/A principles satisfied by the design.

## Project Structure

### Documentation (this feature)

```text
specs/013-audit-trails/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    Audit/
      IAuditTrail.cs                    # Interface: RecordEntryAsync(AuditEntry, CancellationToken)
      AuditEntry.cs                     # Immutable record: tool name, session ID, timestamp, params, outcome, duration, error
      AuditOptions.cs                   # Options class: Enabled, Provider, SqlitePath, ConnectionString, MaxParameterLength
      AuditOptionsValidator.cs          # IValidateOptions<AuditOptions>
  Buildout.Mcp/
    Audit/
      AuditDataConnection.cs            # linq2db DataConnection with AuditEntry table mapping
      AuditEntryRecord.cs               # linq2db [Table] POCO mapping to audit_entries table
      NullAuditTrail.cs                 # No-op IAuditTrail (used when disabled)
      Linq2DbAuditTrail.cs             # linq2db-backed IAuditTrail (shared by both providers)
      AuditTrailFilter.cs              # MCP CallToolFilter that wraps tool calls with audit recording
      Migrations/
        Migration_001_CreateAuditEntries.cs  # FluentMigrator migration: audit_entries table + indexes
      AuditMcpServiceExtensions.cs     # DI registration: linq2db context, FluentMigrator runner, IAuditTrail, filter wiring
    Program.cs                          # Updated: conditional HTTP transport, audit filter registration
  Buildout.Configuration/               # No changes (existing loader handles Audit: section)
tests/
  Buildout.UnitTests/
    Audit/
      AuditOptionsValidatorTests.cs     # Options validation tests
      AuditEntryTests.cs                # AuditEntry construction, parameter truncation
  Buildout.IntegrationTests/
    Audit/
      AuditTestFixture.cs                # Shared Testcontainers PostgreSQL fixture + SQLite temp-file helper
      SqliteAuditTrailTests.cs          # SQLite persistence integration tests
      PostgresAuditTrailTests.cs        # PostgreSQL persistence integration tests (Testcontainers)
      AuditTrailFilterTests.cs          # Full MCP pipeline filter integration test
      MigrationTests.cs                 # FluentMigrator migration verification tests (SQLite + PostgreSQL via Testcontainers)
```

**Structure Decision**: Follows the existing project layout exactly. Interface + options in `Buildout.Core` (per Principle I), implementations in `Buildout.Mcp`. Test directories mirror source structure.

## Complexity Tracking

> No violations — table left empty.
