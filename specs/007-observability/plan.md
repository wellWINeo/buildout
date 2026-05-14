# Implementation Plan: Observability — Logs & Metrics

**Branch**: `007-observability` | **Date**: 2026-05-14 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/007-observability/spec.md`

## Summary

Add structured logging and metrics to Buildout's MCP server using OpenTelemetry. Emit logs for all major operations (page reads, searches, page creation, database views, API calls) and expose business + technical metrics via a `Meter` in `Buildout.Core`. Provide a Docker Compose–based local observability stack (Grafana + Loki, Prometheus, OTel Collector) with pre-provisioned dashboards. No traces, no CLI instrumentation.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK-style `.csproj`, nullable enabled, warnings-as-errors)
**Primary Dependencies**: OpenTelemetry SDK (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.HttpClient`, `OpenTelemetry.Instrumentation.Runtime`), existing `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Hosting` (MCP host)
**Storage**: N/A (telemetry is exported, not stored locally)
**Testing**: xUnit v3 (`Buildout.UnitTests`, `Buildout.IntegrationTests`), NSubstitute for mocking
**Target Platform**: Server-side .NET (MCP server via `Host.CreateApplicationBuilder`), Docker Compose for local dev tooling
**Project Type**: Library + server (Buildout.Core library, Buildout.Mcp server)
**Performance Goals**: <5% overhead when telemetry enabled; no blocking on export failures
**Constraints**: OTLP export must not interfere with MCP stdio transport; no per-block log entries; metric cardinality bounded by operation/tool/style names
**Scale/Scope**: Single-instance MCP server; development-time observability; 27 functional requirements

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Core/Presentation Separation | PASS | Meter + log enrichment live in `Buildout.Core`. MCP handlers call core services unchanged. Only DI registration of OTel provider lives in `Buildout.Mcp/Program.cs`. |
| II. LLM-Friendly Output Fidelity | N/A | No changes to Markdown rendering or block conversion. |
| III. Bidirectional Round-Trip Testing | N/A | No changes to converters. |
| IV. Test-First Discipline | PASS | Unit tests for metrics recording and structured log fields. Integration test: run MCP tool → verify OTel SDK received data via mock exporter. |
| V. Buildin API Abstraction | PASS | Metrics/log instrumentation is added to `BotBuildinClient` (an implementation of `IBuildinClient`). No presentation code touches the API client directly. |
| VI. Non-Destructive Editing | N/A | No editing behavior changes. |
| Technology: .NET 10, SDK-style | PASS | All new packages are .NET 10 compatible. |
| Technology: MCP via ModelContextProtocol SDK | PASS | No changes to MCP transport. OTel uses OTLP HTTP/gRPC on a separate port. |
| Solution layout | PASS | No new projects. Changes only in `Buildout.Core` (meter + logging) and `Buildout.Mcp` (provider registration). Docker Compose files go in repo root `observability/`. |
| Secrets | PASS | OTLP endpoint URL is configuration, not a secret. No tokens or keys in Docker Compose (local dev only). |

**Gate result**: PASS — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/007-observability/
├── plan.md
├── spec.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── metrics-registry.md
├── checklists/
│   └── requirements.md
└── tasks.md              # Created by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Buildout.Core/
│   ├── Diagnostics/
│   │   ├── BuildoutMeter.cs           # Meter definition + factory methods for all instruments
│   │   └── OperationRecorder.cs       # Combines log entry + metric recording for an operation
│   ├── Buildin/
│   │   └── BotBuildinClient.cs        # Modified: add log calls + API metrics to WrapAsync
│   ├── Search/
│   │   └── SearchService.cs           # Modified: add operation log + metrics
│   ├── Markdown/
│   │   ├── PageMarkdownRenderer.cs    # Modified: add page-read log + metrics
│   │   └── Authoring/
│   │       └── PageCreator.cs         # Modified: add page-create log + metrics
│   ├── DatabaseViews/
│   │   └── DatabaseViewRenderer.cs    # Modified: add database-view log + metrics
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs  # Modified: register BuildoutMeter, OperationRecorder
├── Buildout.Mcp/
│   ├── Program.cs                     # Modified: call AddOpenTelemetry(), configure OTLP exporter
│   ├── Tools/
│   │   ├── SearchToolHandler.cs       # Modified: add MCP tool metrics wrapper
│   │   ├── DatabaseViewToolHandler.cs # Modified: add MCP tool metrics wrapper
│   │   └── CreatePageToolHandler.cs   # Modified: add MCP tool metrics wrapper
│   └── Resources/
│       └── PageResourceHandler.cs     # Modified: add MCP resource metrics wrapper
observability/
├── docker-compose.yml
├── otel-collector/
│   └── config.yaml
├── prometheus/
│   └── prometheus.yml
└── grafana/
    ├── provisioning/
    │   ├── dashboards/
    │   │   ├── dashboard.yml
    │   │   ├── operations-overview.json
    │   │   ├── api-client-health.json
    │   │   └── mcp-tool-usage.json
    │   └── datasources/
    │       └── datasources.yml
tests/
├── Buildout.UnitTests/
│   └── Diagnostics/
│       ├── BuildoutMeterTests.cs
│       └── OperationRecorderTests.cs
└── Buildout.IntegrationTests/
    └── Diagnostics/
        └── McpToolMetricsTests.cs
```

**Structure Decision**: No new projects. `Buildout.Core` gains a `Diagnostics/` namespace for the meter and operation recorder. `Buildout.Mcp` gains OTel provider registration in `Program.cs` and per-handler metric wrappers. All Docker/observability infrastructure lives in a top-level `observability/` directory, not in `src/`.

## Complexity Tracking

> No violations — table intentionally empty.
