# Research: Observability — Logs & Metrics

**Feature**: 007-observability
**Date**: 2026-05-14

## R1: OpenTelemetry .NET SDK Package Selection

**Decision**: Use the following NuGet packages:

| Package | Purpose | Project |
|---------|---------|---------|
| `OpenTelemetry.Extensions.Hosting` | Generic host integration (`AddOpenTelemetry()`) | `Buildout.Mcp` |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP gRPC/HTTP exporter (logs + metrics) | `Buildout.Mcp` |
| `OpenTelemetry.Instrumentation.HttpClient` | Automatic `http.client.duration`, `http.client.request.count` | `Buildout.Mcp` |
| `OpenTelemetry.Instrumentation.Runtime` | .NET runtime metrics (GC, thread pool, allocations) | `Buildout.Mcp` |
| `System.Diagnostics.DiagnosticSource` | `Meter`, `Counter<T>`, `Histogram<T>` APIs | `Buildout.Core` (already part of .NET runtime, no extra NuGet needed) |

**Rationale**: These are the official OpenTelemetry .NET packages. `System.Diagnostics.Metrics` is built into .NET 10, so `Buildout.Core` only needs the .NET runtime — no extra OTel package dependency for creating instruments. Only `Buildout.Mcp` needs the OTel SDK and exporters.

**Alternatives considered**:
- Serilog + Serilog sinks: Would add a second logging framework alongside `ILogger`. OTel SDK integrates natively with `Microsoft.Extensions.Logging` via `WithLogging()`.
- Prometheus .NET direct exporter: Would bypass OTel standard; OTLP + OTel Collector is more flexible.
- AppInsights: Cloud-vendor specific; spec requires OTel + Prometheus + Grafana.

## R2: Metrics Architecture — Meter Location

**Decision**: Create a single `BuildoutMeter` class in `Buildout.Core/Diagnostics/` that holds a static `Meter` instance and exposes factory-created instruments (`Counter<T>`, `Histogram<T>`).

```csharp
namespace Buildout.Core.Diagnostics;

public sealed class BuildoutMeter
{
    public const string Name = "Buildout";
    public const string Version = "1.0.0";

    private static readonly Meter Meter = new(Name, Version);

    // Business operations
    public static readonly Counter<long> OperationsTotal = Meter.CreateCounter<long>(
        "buildout.operations.total", "{operation}", "Total buildout operations");

    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "buildout.operation.duration", "s", "Operation duration");

    // ... etc
}
```

**Rationale**: A single meter keeps all buildout metrics under one namespace (`Buildout`). Static readonly instruments are the recommended pattern — they're thread-safe and allocation-free after creation. The MCP host registers this meter name with `AddMeter(BuildoutMeter.Name)`.

**Alternatives considered**:
- Per-class meters: Would scatter meter definitions; harder to discover all metrics.
- Instance-based instruments: Unnecessary allocation; `Counter<T>` and `Histogram<T>` are thread-safe singletons.

## R3: Structured Logging Approach

**Decision**: Use the existing `ILogger<T>` injections in `BotBuildinClient`, `SearchService`, MCP handlers, and add `ILogger<T>` to `PageMarkdownRenderer`, `PageCreator`, and `DatabaseViewRenderer`. Add structured log calls using `LoggerMessage.Define` or `logger.LogInformation` with `Log` overloads and structured placeholders.

OpenTelemetry SDK's `WithLogging()` intercepts `ILogger` output and exports it via OTLP. No Serilog or custom provider needed.

**Rationale**: `ILogger` is already injected (but unused) in several classes. The OTel SDK's logging bridge (`OpenTelemetry.Extensions.Hosting` → `WithLogging()`) captures `ILogger` output and exports it as OTLP log records. This avoids introducing a second logging system.

**Alternatives considered**:
- Serilog with OTel sink: Adds a dependency when `ILogger` + OTel bridge achieves the same.
- Manual OTel Logs API (`LogRecord`): Lower-level; `ILogger` integration is the recommended path.

## R4: OTLP Transport — gRPC vs HTTP

**Decision**: Use OTLP HTTP on port 4318 as the default transport.

**Rationale**: The MCP server communicates over stdio. OTLP gRPC uses HTTP/2 which can have compatibility issues in some environments. OTLP HTTP is simpler and works behind any HTTP proxy. The Docker Compose OTel Collector will expose port 4318 (OTLP HTTP) and 4317 (OTLP gRPC) — the app defaults to HTTP but is configurable.

**Alternatives considered**:
- gRPC (port 4317): Slightly more efficient but adds HTTP/2 dependency. Not worth the complexity.
- Both configurable: Unnecessary; HTTP works everywhere.

## R5: MCP stdio + OTLP Coexistence

**Decision**: OTLP exporter uses an HTTP connection to `http://localhost:4318`. The MCP server's stdio transport is only used for MCP protocol messages. The OTel SDK opens its own HTTP client for export — it never touches stdin/stdout.

**Rationale**: OpenTelemetry SDK's OTLP exporter creates its own `HttpClient` internally. It does not use the process's stdin/stdout. The MCP protocol messages flow through stdio while telemetry flows through an HTTP connection to the collector. No conflict.

**Alternatives considered**:
- File-based export: Unnecessary complexity; HTTP works fine alongside stdio.
- In-process collector: Not a standard pattern.

## R6: Local Stack — Grafana Loki vs Elasticsearch for Logs

**Decision**: Use Grafana Loki as the log backend in the OTel Collector pipeline.

**Rationale**: Loki is lightweight, purpose-built for Grafana integration, and pairs well with Prometheus (same ecosystem, same label-based querying). Docker image is small (~50MB). No cluster needed.

**Alternatives considered**:
- Elasticsearch + Kibana: Heavyweight (~2GB+ memory). Overkill for local dev.
- Grafana Tempo: Requires traces (excluded from this feature).

## R7: Docker Compose Layout

**Decision**: Place all observability infrastructure in `observability/` at repo root.

```
observability/
├── docker-compose.yml          # Grafana, Prometheus, Loki, OTel Collector
├── otel-collector/config.yaml  # Receiver: OTLP; Exporters: Prometheus, Loki
├── prometheus/prometheus.yml   # Scrape config: OTel Collector
└── grafana/provisioning/       # Auto-provisioned datasources + dashboards
```

**Rationale**: Separate from `src/` since these are infrastructure, not buildable code. Developers run `docker compose -f observability/docker-compose.yml up` or `cd observability && docker compose up`.

**Alternatives considered**:
- Root-level `docker-compose.yml`: Clutters repo root; conflicts with potential future Docker Compose files.
- Inside `src/Buildout.Mcp/`: Mixing infrastructure with application code violates project conventions.

## R8: Error Handling — OTLP Endpoint Unavailable

**Decision**: Configure the OTLP exporter with `ExportProcessorType = Simple` for logs (fire-and-forget) and `PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds` for metrics. The OTel SDK already handles retries and batching internally. When the endpoint is unreachable, the SDK logs internally at debug level and buffers/retries.

**Rationale**: OpenTelemetry .NET SDK is designed for this. The `OtlpExporterOptions` handle failures gracefully by default. No custom circuit-breaker needed.

**Alternatives considered**:
- Custom retry policy: Reinventing what OTel SDK already provides.
- `ExportProcessorType = Batch`: Better for production but adds complexity for local dev where immediate feedback is preferred. Can be made configurable later.

## R9: Instrumentation Points — Where to Add Metrics/Logs

**Decision**: Two layers of instrumentation:

1. **Core layer** (`Buildout.Core`):
   - `BotBuildinClient.WrapAsync`: Add log + `buildout_api_calls_total` / `buildout_api_call_duration_seconds` per API method.
   - `PageMarkdownRenderer.RenderAsync`: Add log + `buildout_operations_total{operation=page_read}` / `buildout_operation_duration_seconds` / `buildout_blocks_processed_total`.
   - `SearchService.SearchAsync`: Add log + `buildout_operations_total{operation=search}` / `buildout_operation_duration_seconds` / `buildout_search_results_total`.
   - `PageCreator.CreateAsync`: Add log + `buildout_operations_total{operation=page_create}` / `buildout_pages_created_total{parent_kind}` / `buildout_blocks_processed_total`.
   - `DatabaseViewRenderer.RenderAsync`: Add log + `buildout_operations_total{operation=database_view}` / `buildout_database_view_renders_total{style}`.

2. **Presentation layer** (`Buildout.Mcp`):
   - Each MCP tool handler: Wrap tool invocation with `buildout_mcp_tool_invocations_total` / `buildout_mcp_tool_duration_seconds`.
   - `PageResourceHandler`: Add `buildout_mcp_resource_reads_total`.

**Rationale**: Core layer captures domain-level operations regardless of surface (MCP today, CLI or other tomorrow). Presentation layer captures transport-specific metrics (tool names, resource URIs). This aligns with Principle I (core/presentation separation).

**Alternatives considered**:
- Only presentation-layer metrics: Would miss API client metrics and make core operations unobservable from other surfaces.
- Middleware/interceptor pattern: .NET doesn't have a built-in decorator for `ILogger`-based timing; explicit calls are clearer and more maintainable.
