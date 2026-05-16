# Feature Specification: Observability — Logs & Metrics

**Feature Branch**: `007-observability`
**Created**: 2026-05-14
**Status**: Draft
**Input**: User description: "Add logs and metrics (no traces), use OpenTelemetry, for development prepare local environment (grafana, prometheus, otel collector). Look for specs and code and infer required metrics (both technical and business)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Developer inspects application behavior via structured logs (Priority: P1)

A developer working on buildout runs the MCP server locally and wants to understand what the application is doing without reading source code. Structured logs emitted by all major operations (page reads, searches, page creation, database views, API calls, errors) flow through OpenTelemetry to the local observability stack. The developer opens Grafana, queries logs by operation name, correlation ID, or error type, and can trace the full lifecycle of any request.

**Why this priority**: Without structured logs, debugging and understanding application behavior requires reading source code or attaching a debugger. This is the foundation all other observability builds on.

**Independent Test**: Run any MCP tool invocation, open Grafana Explore, query logs for that operation — see structured entries with timestamps, operation names, and contextual fields.

**Acceptance Scenarios**:

1. **Given** the local observability stack is running, **When** a developer invokes a page read via the MCP server, **Then** structured log entries appear in Grafana for the page-read operation containing page ID, block count, duration, and outcome (success/failure).
2. **Given** the local observability stack is running, **When** a buildin API call fails with a transport error, **Then** a structured error log is emitted containing the error type, target endpoint, and request context.
3. **Given** the local observability stack is running, **When** a developer queries logs by operation name, **Then** results are returned with consistent field names across all operations (operation, duration_ms, outcome, error_type).

---

### User Story 2 — Developer monitors business and technical metrics in dashboards (Priority: P1)

A developer opens Grafana and sees pre-built dashboards showing both business metrics (page reads, searches, page creations, database view renders, error rates by operation) and technical metrics (HTTP request rates/latencies to buildin.ai, connection pool usage, operation duration histograms). Metrics are collected via OpenTelemetry and scraped by Prometheus.

**Why this priority**: Metrics complement logs by providing aggregate views and trend analysis. Together with logs (P1), they give a complete observability picture. Without metrics, the developer can only inspect individual events, not understand patterns.

**Independent Test**: Run several operations via MCP, open the pre-built Grafana dashboard — see counters incrementing, histograms populating, and error rates reflecting the operations performed.

**Acceptance Scenarios**:

1. **Given** the local observability stack is running, **When** a developer executes multiple search operations, **Then** the `buildout_search_total` counter increments and the `buildout_search_duration` histogram records observed durations in the Grafana dashboard.
2. **Given** the local observability stack is running, **When** a buildin API call returns an error, **Then** the `buildout_api_errors_total` counter increments with labels for operation name and error type.
3. **Given** the local observability stack is running, **When** a developer views the operations dashboard, **Then** panels display request rates, latency percentiles (p50, p95, p99), and error rates for each operation type.

---

### User Story 3 — Developer starts the full local observability stack with a single command (Priority: P1)

A developer runs a single command (e.g., `docker compose up`) to start Grafana, Prometheus, and the OpenTelemetry Collector. The application is configured to export logs and metrics to the collector. Pre-built Grafana dashboards are automatically provisioned. The developer can tear everything down cleanly.

**Why this priority**: Friction in setting up observability tooling is the primary reason teams skip it. A one-command local environment removes this barrier and makes observability part of the standard development workflow.

**Independent Test**: Run `docker compose up`, invoke an MCP tool, open Grafana on localhost — see logs and metrics without any manual configuration.

**Acceptance Scenarios**:

1. **Given** Docker is available, **When** a developer runs `docker compose up`, **Then** Grafana, Prometheus, and the OpenTelemetry Collector start and become accessible on their standard local ports.
2. **Given** the local observability stack is running, **When** a developer opens Grafana for the first time, **Then** pre-built dashboards for buildout operations and buildin API client metrics are already provisioned and displaying data.
3. **Given** the local observability stack is running, **When** a developer runs `docker compose down`, **Then** all containers stop and named volumes are preserved for the next session.

---

### User Story 4 — Developer diagnoses slow operations using latency breakdowns (Priority: P2)

A developer notices that some page reads or search operations are slower than expected. Using the metrics dashboards, they identify which operations are slow, see latency percentiles, and correlate with log entries to find the root cause (e.g., large page depth, slow buildin API response, many pagination rounds).

**Why this priority**: Latency diagnosis is a high-value use case that requires both metrics and logs working together, but it depends on the foundation from P1 stories.

**Independent Test**: Execute a page read on a large page, open the operations dashboard — see the elevated latency in the histogram, drill into logs for that operation to find block count and pagination details.

**Acceptance Scenarios**:

1. **Given** a page with 500+ blocks, **When** a developer reads that page, **Then** the duration histogram captures the latency and log entries show the block count and pagination iterations.
2. **Given** the operations dashboard, **When** a developer filters by operation type, **Then** latency percentiles (p50, p95, p99) are displayed per operation.

---

### User Story 5 — Developer monitors MCP server tool and resource usage (Priority: P2)

A developer running the MCP server wants to see which tools are called most often, how long each takes, and whether any are failing. The MCP-specific dashboard shows tool invocation counts, durations, and error rates per tool name (search, database_view, create_page) and resource reads per resource pattern (buildin://{pageId}).

**Why this priority**: MCP is a growing surface; understanding its usage patterns helps prioritize future improvements. Depends on the metrics foundation from P1.

**Independent Test**: Invoke several MCP tools, open the MCP dashboard — see per-tool invocation counts, durations, and error rates.

**Acceptance Scenarios**:

1. **Given** the MCP server is running with observability, **When** a tool is invoked, **Then** a metric is recorded with the tool name, duration, and outcome.
2. **Given** the MCP-specific dashboard, **When** a developer views it, **Then** tool invocation counts are broken down by tool name (search, database_view, create_page) and outcome.

---

### Edge Cases

- What happens when the OpenTelemetry Collector or Prometheus is unavailable? The application should not crash or block; telemetry export should fail silently and buffer/retry where possible.
- What happens when a very large page (1000+ blocks) is read? Log volume and metric cardinality should remain bounded — no per-block log entries, only aggregate counts.
- What happens when the MCP server runs over stdio transport? Telemetry export must not interfere with the MCP protocol stream — use a separate OTLP HTTP endpoint, not stdio.
- What happens with concurrent operations? Metrics must be thread-safe; log correlation must distinguish concurrent operations.

## Requirements *(mandatory)*

### Functional Requirements

#### Structured Logging

- **FR-001**: The system MUST emit structured log entries for every major operation: page read, search, page creation, database view render, and each buildin API client call.
- **FR-002**: Log entries MUST include consistent fields: `operation` (string), `duration_ms` (number), `outcome` (success/failure), and a timestamp.
- **FR-003**: Log entries for failed operations MUST include `error_type` (transport/api/unknown) and, for API errors, `status_code`.
- **FR-004**: The system MUST emit log entries for pagination progress during page reads, searches, and database queries (page number, items fetched).
- **FR-005**: Log entries MUST be exported via OpenTelemetry's logs pipeline to the configured OTLP endpoint.

#### Metrics — Business Operations

- **FR-006**: The system MUST expose a counter metric `buildout_operations_total` with labels `operation` (page_read, search, page_create, database_view) and `outcome` (success, failure).
- **FR-007**: The system MUST expose a histogram metric `buildout_operation_duration_seconds` with labels `operation` and `outcome`, capturing operation latency.
- **FR-008**: The system MUST expose a counter metric `buildout_api_calls_total` with labels `method` (the buildin API method name), `outcome`, and `error_type`.
- **FR-009**: The system MUST expose a histogram metric `buildout_api_call_duration_seconds` with labels `method` and `outcome`.
- **FR-010**: The system MUST expose a counter metric `buildout_blocks_processed_total` with labels `operation` (page_read, page_create), capturing the number of blocks read or written.
- **FR-011**: The system MUST expose a counter metric `buildout_search_results_total` capturing the number of search results returned.
- **FR-012**: The system MUST expose a counter metric `buildout_pages_created_total` with label `parent_kind` (page, database), capturing page creations by parent type.
- **FR-013**: The system MUST expose a counter metric `buildout_database_view_renders_total` with label `style` (table, board, gallery, list, calendar, timeline), capturing database view renders by style.

#### Metrics — MCP Surface

- **FR-014**: The system MUST expose a counter metric `buildout_mcp_tool_invocations_total` with labels `tool` (search, database_view, create_page) and `outcome`.
- **FR-015**: The system MUST expose a histogram metric `buildout_mcp_tool_duration_seconds` with labels `tool` and `outcome`.
- **FR-016**: The system MUST expose a counter metric `buildout_mcp_resource_reads_total` with label `outcome`, capturing resource reads.

#### Metrics — Technical / Infrastructure

- **FR-017**: The system MUST expose OpenTelemetry's built-in HTTP client metrics including `http.client.duration` and `http.client.request.count` for outbound calls to the buildin API.
- **FR-018**: The system MUST expose process-level runtime metrics: `process.runtime.dotnet.*` (GC, thread pool, allocations) via OpenTelemetry's built-in .NET runtime metrics.

#### Local Development Environment

- **FR-019**: The system MUST provide a Docker Compose configuration that starts Grafana, Prometheus, and the OpenTelemetry Collector with a single `docker compose up` command.
- **FR-020**: The OpenTelemetry Collector MUST be configured to receive OTLP logs and metrics from the application and export logs to Grafana Loki and metrics to Prometheus.
- **FR-021**: Grafana MUST be provisioned with pre-built dashboards for: (a) buildout operations overview, (b) buildin API client health, (c) MCP tool usage.
- **FR-022**: Prometheus MUST be configured to scrape metrics from the OpenTelemetry Collector.
- **FR-023**: The application MUST be configurable via environment variables to set the OTLP endpoint (default: `http://localhost:4317` for gRPC or `http://localhost:4318` for HTTP).
- **FR-024**: Telemetry export MUST NOT block or crash the application if the OTLP endpoint is unavailable. Failures should be logged at debug level and silently retried.

#### Integration

- **FR-025**: Observability MUST be registered in the DI container and configurable (enabled/disabled, endpoint URL) without code changes.
- **FR-026**: The MCP host MUST initialize the OpenTelemetry provider at startup and flush telemetry at shutdown.
- **FR-027**: The OpenTelemetry SDK and provider MUST be added as dependencies to `Buildout.Core` (for the meter and logging enrichment), with OpenTelemetry exporter packages in `Buildout.Mcp`.

### Key Entities

- **Operation**: Represents a top-level buildout action (page_read, search, page_create, database_view) with duration, outcome, and contextual metadata.
- **ApiCall**: Represents an individual call to the buildin API with method name, duration, outcome, and error details.
- **McpToolInvocation**: Represents an MCP tool call with tool name, duration, and outcome.
- **McpResourceRead**: Represents an MCP resource read with resource URI and outcome.
- **LocalObservabilityStack**: Represents the Docker Compose-managed set of services (Grafana, Prometheus, OTel Collector) with their configuration and dashboard provisioning.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every major operation (page read, search, page create, database view) emits at least one structured log entry and at least one metric event per invocation.
- **SC-002**: A developer can start the full observability stack and see live data in Grafana within 2 minutes of running `docker compose up`.
- **SC-003**: Pre-built Grafana dashboards display operation counts, latency percentiles (p50, p95, p99), and error rates without any manual query writing.
- **SC-004**: Application performance degradation from observability is negligible — less than 5% increase in operation duration when telemetry is enabled vs disabled.
- **SC-005**: All metric names follow a consistent `buildout_<domain>_<name>_<unit>` naming convention with appropriate labels.
- **SC-006**: When the OTLP endpoint is unavailable, the application continues to function correctly with no user-visible errors or hangs.

## Assumptions

- Docker and Docker Compose are available on developer machines (standard dev tooling).
- The application targets .NET 10, which has full OpenTelemetry SDK support via NuGet packages.
- The MCP server's stdio transport is used for MCP protocol communication; telemetry uses OTLP HTTP/gRPC on a separate port, so there is no conflict.
- OpenTelemetry .NET SDK packages (OpenTelemetry.Extensions.Hosting, OpenTelemetry.Exporter.OpenTelemetryProtocol, etc.) will be added as new dependencies — this is acceptable per the project's use of standard NuGet packages.
- Grafana Loki is used as the log backend in the local stack (lightweight, pairs well with Prometheus and OTel).
- Runtime metrics (GC, thread pool) are provided by OpenTelemetry's built-in .NET runtime instrumentation — no custom implementation needed.
- The existing `ILogger` injections in `BotBuildinClient`, `SearchService`, and MCP handlers will be activated with actual log calls rather than replaced with a different logging approach.
- HTTP client metrics for outbound calls to buildin.ai are captured via OpenTelemetry's built-in `HttpClient` instrumentation — no custom metric needed.
- CLI observability is out of scope for this feature since it only affects server deployment (the MCP server is the long-running service).
- Traces are explicitly excluded from this feature per user request — no `ActivitySource` or span creation.
- Production-ready deployment of the observability stack (auth, TLS, retention policies) is out of scope; this is a development-focused local environment.
