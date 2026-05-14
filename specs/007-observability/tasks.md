# Tasks: Observability — Logs & Metrics

**Input**: Design documents from `/specs/007-observability/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Core library**: `src/Buildout.Core/`
- **MCP server**: `src/Buildout.Mcp/`
- **Unit tests**: `tests/Buildout.UnitTests/`
- **Integration tests**: `tests/Buildout.IntegrationTests/`
- **Observability infra**: `observability/`

---

## Phase 1: Setup

**Purpose**: Add OpenTelemetry NuGet packages to the solution

- [ ] T001 Add OpenTelemetry NuGet packages to `src/Buildout.Mcp/Buildout.Mcp.csproj`: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.HttpClient`, `OpenTelemetry.Instrumentation.Runtime`
- [ ] T002 Run `dotnet restore` and `dotnet build` to verify packages resolve and project compiles

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core observability infrastructure that MUST be complete before ANY user story instrumentation can begin

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests for Foundation

- [ ] T003 [P] Unit test: `BuildoutMeter` creates all instruments with correct names, types, and units in `tests/Buildout.UnitTests/Diagnostics/BuildoutMeterTests.cs`
- [ ] T004 [P] Unit test: `OperationRecorder` records duration, logs on succeed/fail, and guards against forgotten disposal in `tests/Buildout.UnitTests/Diagnostics/OperationRecorderTests.cs`

### Implementation for Foundation

- [ ] T005 Create `BuildoutMeter` static class in `src/Buildout.Core/Diagnostics/BuildoutMeter.cs` — define meter `"Buildout"` v1.0.0 with all 13 instruments per `contracts/metrics-registry.md` (OperationsTotal, OperationDuration, ApiCallsTotal, ApiCallDuration, BlocksProcessedTotal, SearchResultsTotal, PagesCreatedTotal, DatabaseViewRendersTotal, McpToolInvocationsTotal, McpToolDuration, McpResourceReadsTotal)
- [ ] T006 Create `OperationRecorder` in `src/Buildout.Core/Diagnostics/OperationRecorder.cs` — disposable helper that combines Stopwatch timing, `ILogger` structured log calls, and `BuildoutMeter` instrument recording. Methods: `Start()`, `SetTag()`, `Succeed()`, `Fail()`, `Dispose()`
- [ ] T007 Register `BuildoutMeter` references in DI via `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` — no new registrations needed (instruments are static), but add `AddLogging()` if not already present
- [ ] T008 Configure OpenTelemetry provider in `src/Buildout.Mcp/Program.cs` — call `builder.Services.AddOpenTelemetry()` with `.ConfigureResource()` (service.name=`buildout-mcp`), `.WithMetrics()` (add meter `Buildout`, HttpClient instrumentation, Runtime instrumentation, OTLP HTTP exporter), `.WithLogging()` (OTLP exporter). Read endpoint from `OTEL_EXPORTER_OTLP_ENDPOINT` env var (default `http://localhost:4318`). Guard with `BUILDOUT_TELEMETRY_ENABLED` env var.
- [ ] T009 Run `dotnet test` — verify foundational tests pass

**Checkpoint**: Foundation ready — meter, recorder, and OTel provider are wired. User story instrumentation can begin.

---

## Phase 3: User Story 1 + 2 — Structured Logs & Business Metrics (Priority: P1) 🎯 MVP

**Goal**: Every major operation (page read, search, page create, database view, API call) emits structured log entries and business metrics through the OTel pipeline.

**Independent Test**: Run unit tests verifying each service records correct metrics and log fields. Run MCP server with a test OTel exporter, invoke a tool, verify data reaches the exporter.

### Tests for US1+US2

- [ ] T010 [P] [US1] Unit test: `BotBuildinClient.WrapAsync` logs method name, outcome, duration, and error_type on API call success and failure, and records `buildout.api.calls.total` + `buildout.api.call.duration` in `tests/Buildout.UnitTests/Buildin/BotBuildinClientLoggingTests.cs`
- [ ] T011 [P] [US1] Unit test: `PageMarkdownRenderer.RenderAsync` logs page_id, block_count, duration, and records `buildout.operations.total{operation=page_read}` + `buildout.blocks.processed.total{operation=page_read}` in `tests/Buildout.UnitTests/Markdown/PageMarkdownRendererLoggingTests.cs`
- [ ] T012 [P] [US1] Unit test: `SearchService.SearchAsync` logs query, result_count, duration, and records `buildout.operations.total{operation=search}` + `buildout.search.results.total` in `tests/Buildout.UnitTests/Search/SearchServiceLoggingTests.cs`
- [ ] T013 [P] [US1] Unit test: `PageCreator.CreateAsync` logs parent_kind, block_count, duration, and records `buildout.operations.total{operation=page_create}` + `buildout.pages.created.total` + `buildout.blocks.processed.total{operation=page_create}` in `tests/Buildout.UnitTests/Markdown/Authoring/PageCreatorLoggingTests.cs`
- [ ] T014 [P] [US1] Unit test: `DatabaseViewRenderer.RenderAsync` logs database_id, style, row_count, duration, and records `buildout.operations.total{operation=database_view}` + `buildout.database.view.renders.total{style}` in `tests/Buildout.UnitTests/DatabaseViews/DatabaseViewRendererLoggingTests.cs`

### Implementation for US1+US2

- [ ] T015 [P] [US1] Instrument `BotBuildinClient.WrapAsync` in `src/Buildout.Core/Buildin/BotBuildinClient.cs` — add `OperationRecorder` or direct `BuildoutMeter.ApiCallsTotal`/`ApiCallDuration` recording around each API call. Add structured log entries with `method`, `duration_ms`, `outcome`, `error_type`, `status_code`. Pass caller method name via `[CallerMemberName]` or explicit parameter.
- [ ] T016 [P] [US1] Instrument `PageMarkdownRenderer.RenderAsync` in `src/Buildout.Core/Markdown/PageMarkdownRenderer.cs` — add `ILogger<PageMarkdownRenderer>` injection (currently missing). Wrap `RenderAsync` with `OperationRecorder` for `page_read` operation. Record `block_count` after tree traversal.
- [ ] T017 [P] [US1] Instrument `SearchService.SearchAsync` in `src/Buildout.Core/Search/SearchService.cs` — wrap with `OperationRecorder` for `search` operation. Record `query`, `result_count`. Keep existing cycle-detection debug log.
- [ ] T018 [P] [US1] Instrument `PageCreator.CreateAsync` in `src/Buildout.Core/Markdown/Authoring/PageCreator.cs` — add `ILogger<PageCreator>` injection (currently missing). Wrap with `OperationRecorder` for `page_create` operation. Record `parent_kind`, `block_count`.
- [ ] T019 [P] [US1] Instrument `DatabaseViewRenderer.RenderAsync` in `src/Buildout.Core/DatabaseViews/DatabaseViewRenderer.cs` — add `ILogger<DatabaseViewRenderer>` injection (currently missing). Wrap with `OperationRecorder` for `database_view` operation. Record `style`, row_count.
- [ ] T020 [US1] Add `AddLogging()` call to `src/Buildout.Mcp/Program.cs` (or verify the Generic Host already provides it via `Host.CreateApplicationBuilder`). Ensure `ILogger<T>` is available for all newly-injected loggers.
- [ ] T021 Run `dotnet test` — verify all US1+US2 tests pass

**Checkpoint**: Core operations emit structured logs + business metrics. Verifiable via unit tests and mock OTel exporter.

---

## Phase 4: User Story 5 — MCP Tool & Resource Metrics (Priority: P2)

**Goal**: MCP tool invocations and resource reads are instrumented with per-tool and per-resource metrics.

**Independent Test**: Invoke MCP tools via integration test, verify per-tool counters and duration histograms are recorded.

### Tests for US5

- [ ] T022 [P] [US5] Integration test: invoke each MCP tool (search, database_view, create_page) and verify `buildout.mcp.tool.invocations.total` and `buildout.mcp.tool.duration` are recorded with correct `tool` and `outcome` labels in `tests/Buildout.IntegrationTests/Diagnostics/McpToolMetricsTests.cs`
- [ ] T023 [P] [US5] Integration test: read MCP resource `buildin://{pageId}` and verify `buildout.mcp.resource.reads.total` is recorded with correct `outcome` label in `tests/Buildout.IntegrationTests/Diagnostics/McpResourceMetricsTests.cs`

### Implementation for US5

- [ ] T024 [P] [US5] Add MCP tool metrics wrapper to `SearchToolHandler.SearchAsync` in `src/Buildout.Mcp/Tools/SearchToolHandler.cs` — record `buildout.mcp.tool.invocations.total{tool=search}` and `buildout.mcp.tool.duration{tool=search}` using `BuildoutMeter` directly. Use try/catch to record `outcome=failure` on `McpProtocolException`.
- [ ] T025 [P] [US5] Add MCP tool metrics wrapper to `DatabaseViewToolHandler.RenderAsync` in `src/Buildout.Mcp/Tools/DatabaseViewToolHandler.cs` — same pattern as T024 with `tool=database_view`.
- [ ] T026 [P] [US5] Add MCP tool metrics wrapper to `CreatePageToolHandler.CreatePageAsync` in `src/Buildout.Mcp/Tools/CreatePageToolHandler.cs` — same pattern as T024 with `tool=create_page`.
- [ ] T027 [P] [US5] Add MCP resource metrics to `PageResourceHandler.GetPageMarkdownAsync` in `src/Buildout.Mcp/Resources/PageResourceHandler.cs` — record `buildout.mcp.resource.reads.total{outcome}` using `BuildoutMeter`.
- [ ] T028 Run `dotnet test` — verify all US5 tests pass

**Checkpoint**: MCP tools and resources emit per-tool/per-resource metrics. All three MCP surfaces instrumented.

---

## Phase 5: User Story 3 — Local Observability Stack (Priority: P1)

**Goal**: A developer runs `docker compose up` and gets Grafana + Prometheus + Loki + OTel Collector with pre-provisioned dashboards.

**Independent Test**: Run `docker compose up`, start MCP server with OTLP endpoint pointing at collector, invoke a tool, verify data appears in Grafana.

### Implementation for US3

- [ ] T029 Create `observability/docker-compose.yml` — services: `otel-collector` (image: `otel/opentelemetry-collector-contrib:latest`, ports 4317:4317, 4318:4318, 8889:8889), `prometheus` (image: `prom/prometheus:latest`, port 9090:9090), `loki` (image: `grafana/loki:latest`, port 3100:3100), `grafana` (image: `grafana/grafana:latest`, port 3000:3000). Configure volumes for configs and provisioning.
- [ ] T030 Create `observability/otel-collector/config.yaml` — receivers: OTLP HTTP (4318) + OTLP gRPC (4317). Processors: batch. Exporters: Prometheus (port 8889), Loki (http://loki:3100/loki/api/v1/push). Service pipelines: metrics (OTLP → Prometheus), logs (OTLP → Loki).
- [ ] T031 Create `observability/prometheus/prometheus.yml` — scrape config targeting `otel-collector:8889` with 15s interval.
- [ ] T032 Create `observability/grafana/provisioning/datasources/datasources.yml` — auto-provision Prometheus datasource (http://prometheus:9090) and Loki datasource (http://loki:3100).
- [ ] T033 Create `observability/grafana/provisioning/dashboards/dashboard.yml` — dashboard provider pointing to `/var/lib/grafana/dashboards/`.
- [ ] T034 [P] Create `observability/grafana/provisioning/dashboards/operations-overview.json` — Grafana dashboard with panels: operation rate by type, duration p50/p95/p99, error rate, blocks processed, search results, pages created, database views by style. Queries use `buildout_operations_total`, `buildout_operation_duration_seconds`, etc.
- [ ] T035 [P] Create `observability/grafana/provisioning/dashboards/api-client-health.json` — Grafana dashboard with panels: API call rate by method, duration p50/p95/p99, error rate by method, error type distribution. Queries use `buildout_api_calls_total`, `buildout_api_call_duration_seconds`.
- [ ] T036 [P] Create `observability/grafana/provisioning/dashboards/mcp-tool-usage.json` — Grafana dashboard with panels: tool invocation rate, duration p50/p95/p99, error rate by tool, resource read rate. Queries use `buildout_mcp_tool_invocations_total`, `buildout_mcp_tool_duration_seconds`, `buildout_mcp_resource_reads_total`.
- [ ] T037 Validate: run `docker compose -f observability/docker-compose.yml up -d`, verify all containers start, Grafana is accessible at http://localhost:3000, dashboards are provisioned, datasources are configured. Run `docker compose -f observability/docker-compose.yml down` after.

**Checkpoint**: Local observability stack runs with a single command. Dashboards are pre-provisioned.

---

## Phase 6: User Story 4 — Latency Diagnosis (Priority: P2)

**Goal**: Pagination progress is logged, dashboards display latency percentiles per operation enabling drill-down to logs.

**Independent Test**: Read a page with many blocks, verify pagination log entries appear, verify dashboard shows latency percentiles.

### Tests for US4

- [ ] T038 [US4] Unit test: `PageMarkdownRenderer.FetchChildrenAsync` emits pagination log entries with `pagination_page` and `pagination_items` in `tests/Buildout.UnitTests/Markdown/PageMarkdownRendererLoggingTests.cs`

### Implementation for US4

- [ ] T039 [US4] Add pagination logging to `PageMarkdownRenderer.FetchChildrenAsync` in `src/Buildout.Core/Markdown/PageMarkdownRenderer.cs` — emit a debug log for each pagination round with `page_number` and `items_fetched`.
- [ ] T040 [US4] Add pagination logging to `SearchService.SearchAsync` in `src/Buildout.Core/Search/SearchService.cs` — emit a debug log for each pagination round with `page_number` and `items_fetched`.
- [ ] T041 [US4] Add pagination logging to `DatabaseViewRenderer.PaginateRowsAsync` in `src/Buildout.Core/DatabaseViews/DatabaseViewRenderer.cs` — emit a debug log for each pagination round with `page_number` and `items_fetched`.
- [ ] T042 Verify dashboard JSON files (`operations-overview.json`) include latency percentile panels (p50, p95, p99) per operation type using `histogram_quantile` PromQL.
- [ ] T043 Run `dotnet test` — verify US4 tests pass

**Checkpoint**: Pagination is logged. Latency breakdowns visible in dashboards.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Integration verification and cleanup

- [ ] T044 Run full solution build (`dotnet build`) — verify zero warnings (warnings-as-errors is enabled)
- [ ] T045 Run full test suite (`dotnet test`) — verify all unit + integration tests pass
- [ ] T046 Run `quickstart.md` validation: start Docker Compose stack, start MCP server with telemetry, invoke a tool, verify data in Grafana dashboards, tear down
- [ ] T047 Verify OTLP endpoint unavailability does not crash or block the MCP server — start MCP server without Docker Compose running, invoke tools, confirm normal operation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1+US2 (Phase 3)**: Depends on Foundational — core logs + metrics
- **US5 (Phase 4)**: Depends on Foundational — MCP metrics (can parallel with Phase 3)
- **US3 (Phase 5)**: Depends on Phase 3 and Phase 4 for meaningful dashboard data — Docker/OTel/Prometheus/Grafana
- **US4 (Phase 6)**: Depends on Phase 3 (pagination logs) and Phase 5 (dashboards)
- **Polish (Phase 7)**: Depends on all phases

### User Story Dependencies

- **US1+US2 (P1)**: Foundation → core instrumentation. No other story dependencies.
- **US3 (P1)**: Can build the Docker stack independently (T029–T036) but meaningful validation needs US1+US2 + US5 data.
- **US5 (P2)**: Depends on Foundational. Can run in parallel with US1+US2.
- **US4 (P2)**: Depends on US1+US2 (pagination logs) + US3 (dashboards).

### Parallel Opportunities

- **Phase 2**: T003 || T004 (different test files)
- **Phase 3**: T010 || T011 || T012 || T013 || T014 (all test files independent); T015 || T016 || T017 || T018 || T019 (all source files independent)
- **Phase 4**: T022 || T023 (different test files); T024 || T025 || T026 || T027 (different source files)
- **Phase 3 + Phase 4**: Can run entirely in parallel — different source files, different test files
- **Phase 5**: T034 || T035 || T036 (different dashboard JSON files)

---

## Parallel Example: Phase 3 + Phase 4 (max parallelism)

```
# Foundation complete — launch all instrumentation in parallel:
Agent A: T010 → T015 (BotBuildinClient logs + metrics + test)
Agent B: T011 → T016 (PageMarkdownRenderer logs + metrics + test)
Agent C: T012 → T017 (SearchService logs + metrics + test)
Agent D: T013 → T018 (PageCreator logs + metrics + test)
Agent E: T014 → T019 (DatabaseViewRenderer logs + metrics + test)
Agent F: T022 → T024 (SearchToolHandler MCP metrics + test)
Agent G: T023 → T027 (PageResourceHandler MCP metrics + test)
Agent H: T025 (DatabaseViewToolHandler MCP metrics)
Agent I: T026 (CreatePageToolHandler MCP metrics)
```

---

## Implementation Strategy

### MVP First (Phase 1–3)

1. Complete Phase 1: Setup (add packages)
2. Complete Phase 2: Foundational (meter, recorder, OTel provider)
3. Complete Phase 3: US1+US2 (core logs + metrics)
4. **STOP and VALIDATE**: Run unit tests, verify mock OTel exporter receives data
5. Core operations observable — can demo with a test exporter

### Incremental Delivery

1. Setup + Foundation → Meter and OTel wired
2. Add US1+US2 → Core operations emit logs + metrics (MVP!)
3. Add US5 → MCP tools emit per-tool metrics
4. Add US3 → Local dev stack with Grafana dashboards
5. Add US4 → Pagination logging + latency drill-down
6. Polish → Full build/test pass, quickstart validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- US1 and US2 are combined (Phase 3) because `OperationRecorder` produces both logs and metrics atomically — separating them would duplicate instrumentation points
- All `ILogger<T>` injections already exist in `BotBuildinClient` and `SearchService` — they just need actual log calls
- `PageMarkdownRenderer`, `PageCreator`, `DatabaseViewRenderer` need `ILogger<T>` added to their constructors
- Dashboard JSON files are hand-crafted Grafana dashboard model JSON — not generated code
- The OTel Collector config uses the `loki` exporter from `otel-collector-contrib` to send logs to Loki
