# Data Model: Observability — Logs & Metrics

**Feature**: 007-observability
**Date**: 2026-05-14

## Entities

### BuildoutMeter

Central meter definition. Static class holding all instrument references.

| Field | Type | Description |
|-------|------|-------------|
| Name | `const string` | `"Buildout"` — the meter name registered with OTel SDK |
| Version | `const string` | `"1.0.0"` |
| OperationsTotal | `Counter<long>` | `buildout.operations.total` — labels: `operation`, `outcome` |
| OperationDuration | `Histogram<double>` | `buildout.operation.duration` (unit: `s`) — labels: `operation`, `outcome` |
| ApiCallsTotal | `Counter<long>` | `buildout.api.calls.total` — labels: `method`, `outcome`, `error_type` |
| ApiCallDuration | `Histogram<double>` | `buildout.api.call.duration` (unit: `s`) — labels: `method`, `outcome` |
| BlocksProcessedTotal | `Counter<long>` | `buildout.blocks.processed.total` — labels: `operation` |
| SearchResultsTotal | `Counter<long>` | `buildout.search.results.total` |
| PagesCreatedTotal | `Counter<long>` | `buildout.pages.created.total` — labels: `parent_kind` |
| DatabaseViewRendersTotal | `Counter<long>` | `buildout.database.view.renders.total` — labels: `style` |
| McpToolInvocationsTotal | `Counter<long>` | `buildout.mcp.tool.invocations.total` — labels: `tool`, `outcome` |
| McpToolDuration | `Histogram<double>` | `buildout.mcp.tool.duration` (unit: `s`) — labels: `tool`, `outcome` |
| McpResourceReadsTotal | `Counter<long>` | `buildout.mcp.resource.reads.total` — labels: `outcome` |

### OperationRecorder

Helper that combines timing, logging, and metric recording for a single operation. Disposable pattern for `using` blocks.

| Field | Type | Description |
|-------|------|-------------|
| Meter | `BuildoutMeter` | Reference to meter (static) |
| Logger | `ILogger` | Logger for structured output |
| OperationName | `string` | e.g., `"page_read"`, `"search"`, `"page_create"`, `"database_view"` |
| StartTime | `ValueStopwatch` | Captured at construction |
| Tags | `KeyValuePair<string,object?>[]` | Accumulated labels |

| Method | Behavior |
|--------|----------|
| `Start(logger, operationName, tags)` | Returns new `OperationRecorder`. Logs "Operation started" at debug level. |
| `SetTag(key, value)` | Adds/replaces a tag after start (e.g., block count not known upfront). |
| `Succeed()` | Logs "Operation completed" at info level with `duration_ms` and `outcome=success`. Records to `OperationsTotal` and `OperationDuration`. |
| `Fail(errorType, statusCode?)` | Logs "Operation failed" at error level with `duration_ms`, `outcome=failure`, `error_type`. Records to `OperationsTotal{outcome=failure}` and `OperationDuration`. |
| `Dispose()` | If neither `Succeed()` nor `Fail()` was called, records as failure (guards against forgotten completion). |

### Structured Log Entry Shape

All log entries follow a consistent shape. Fields are logged as structured parameters (not interpolated strings).

| Field | Type | Always Present | Description |
|-------|------|----------------|-------------|
| `operation` | `string` | Yes | Operation name (e.g., `page_read`, `search`, `api_call`) |
| `duration_ms` | `double` | Yes | Wall-clock duration in milliseconds |
| `outcome` | `string` | Yes | `"success"` or `"failure"` |
| `error_type` | `string` | No (failure only) | `"transport"`, `"api"`, `"unknown"` |
| `status_code` | `int?` | No (API errors only) | HTTP status code from buildin API |
| `method` | `string` | No (API calls only) | Buildin API method name (e.g., `GetPageAsync`) |
| `page_id` | `string` | No | Page ID for page-read operations |
| `database_id` | `string` | No | Database ID for database view operations |
| `block_count` | `int` | No | Number of blocks processed |
| `query` | `string` | No | Search query (truncated if >100 chars) |
| `result_count` | `int` | No | Number of search results |
| `style` | `string` | No | Database view style |
| `parent_kind` | `string` | No | `"page"` or `"database"` for page creation |
| `tool` | `string` | No | MCP tool name |
| `pagination_page` | `int` | No | Current pagination page number |
| `pagination_items` | `int` | No | Items fetched in current pagination round |

### OpenTelemetry Resource Attributes

Applied to all telemetry via `ConfigureResource()`.

| Attribute | Value | Source |
|-----------|-------|--------|
| `service.name` | `"buildout-mcp"` | Hardcoded |
| `service.version` | Assembly version | `Assembly.GetEntryAssembly()` |
| `service.instance.id` | Machine name + PID | `Environment.MachineName` + `Environment.ProcessId` |

### LocalObservabilityStack

Docker Compose managed services.

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| `otel-collector` | `otel/opentelemetry-collector-contrib:latest` | 4318 (OTLP HTTP), 4317 (OTLP gRPC), 8889 (Prometheus export) | Receives OTLP from app, exports to Loki + Prometheus |
| `prometheus` | `prom/prometheus:latest` | 9090 | Scrapes OTel Collector, stores metrics |
| `loki` | `grafana/loki:latest` | 3100 | Stores log data |
| `grafana` | `grafana/grafana:latest` | 3000 | Dashboards, queries Loki + Prometheus |

### Grafana Dashboards

| Dashboard | Panels | Data Source |
|-----------|--------|-------------|
| Buildout Operations Overview | Operation rate, duration p50/p95/p99, error rate, blocks processed, search results, pages created, database views by style | Prometheus |
| Buildin API Client Health | API call rate, duration p50/p95/p99, error rate by method, error type distribution, HTTP client built-in metrics | Prometheus |
| MCP Tool Usage | Tool invocation rate, duration p50/p95/p99, error rate by tool, resource read rate | Prometheus |

All dashboards also link to Loki for log drill-down via Grafana's Explore.

## Validation Rules

- `operation` label values MUST be from a fixed set: `page_read`, `search`, `page_create`, `database_view`.
- `outcome` label values MUST be `"success"` or `"failure"`.
- `error_type` label values MUST be `"transport"`, `"api"`, `"unknown"`, or `""` (empty for successes).
- `tool` label values MUST be from a fixed set: `search`, `database_view`, `create_page`.
- `style` label values MUST be from a fixed set: `table`, `board`, `gallery`, `list`, `calendar`, `timeline`.
- `parent_kind` label values MUST be `"page"` or `"database"`.
- No per-block or per-row metric labels — cardinality is bounded by the enums above.
- `duration_ms` MUST always be recorded as a positive number.

## State Transitions

None — all metrics are counters/histograms (monotonically increasing or point observations). No stateful instruments.
