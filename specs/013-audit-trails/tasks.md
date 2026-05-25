# Tasks: MCP Audit Trails

**Input**: Design documents from `/specs/013-audit-trails/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup

**Purpose**: Add required NuGet package dependencies

- [ ] T001 Add `linq2db`, `linq2db.SQLite`, `linq2db.PostgreSQL`, `FluentMigrator`, `FluentMigrator.Runner.SQLite`, `FluentMigrator.Runner.Postgres`, and `ModelContextProtocol.AspNetCore` NuGet packages to `src/Buildout.Mcp/Buildout.Mcp.csproj`; add `Testcontainers.PostgreSql` to `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types, interface, options, linq2db data layer, and HTTP transport infrastructure that ALL user stories depend on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 [P] Create `AuditEntry` record and `AuditOutcome` enum in `src/Buildout.Core/Audit/AuditEntry.cs`
- [ ] T003 [P] Create `IAuditTrail` interface in `src/Buildout.Core/Audit/IAuditTrail.cs`
- [ ] T004 [P] Create `AuditOptions` configuration class in `src/Buildout.Core/Audit/AuditOptions.cs`
- [ ] T005 [P] Create `AuditOptionsValidator` (`IValidateOptions<AuditOptions>`) in `src/Buildout.Core/Audit/AuditOptionsValidator.cs`
- [ ] T006 [P] Create `NullAuditTrail` (no-op `IAuditTrail`) in `src/Buildout.Mcp/Audit/NullAuditTrail.cs`
- [ ] T007 [P] Create `AuditEntryRecord` linq2db `[Table]` POCO mapping to `audit_entries` table in `src/Buildout.Mcp/Audit/AuditEntryRecord.cs`
- [ ] T008 [P] Create `AuditDataConnection` (linq2db `DataConnection` subclass exposing `AuditEntries` table) in `src/Buildout.Mcp/Audit/AuditDataConnection.cs`
- [ ] T009 [P] Create FluentMigrator migration `Migration_001_CreateAuditEntries` defining `audit_entries` table and indexes per data-model.md schema V1 in `src/Buildout.Mcp/Audit/Migrations/Migration_001_CreateAuditEntries.cs` — FluentMigrator's `VersionInfo` table tracks applied migrations implicitly, satisfying schema version detection for future evolution
- [ ] T010 [P] Write unit tests for `AuditEntry` construction and `Truncate` helper in `tests/Buildout.UnitTests/Audit/AuditEntryTests.cs`
- [ ] T011 [P] Write unit tests for `AuditOptionsValidator` validation rules in `tests/Buildout.UnitTests/Audit/AuditOptionsValidatorTests.cs`
- [ ] T012 [P] Create `AuditTestFixture` — xUnit `IAsyncLifetime` fixture spinning up Testcontainers PostgreSQL via `Testcontainers.PostgreSql`, plus SQLite temp-file helper for shared test setup in `tests/Buildout.IntegrationTests/Audit/AuditTestFixture.cs`
- [ ] T013 [P] Write integration test verifying FluentMigrator `Migration_001` creates correct schema in SQLite in `tests/Buildout.IntegrationTests/Audit/MigrationTests.cs`
- [ ] T014 Add HTTP transport support to `src/Buildout.Mcp/Program.cs` — conditional `WithHttpTransport` based on `Transport:Type` config, `ModelContextProtocol.AspNetCore` integration

**Checkpoint**: Foundation ready — all core types exist, options validate, linq2db context and POCO mapping compile, migration class compiles, Testcontainers fixture compiles, HTTP transport available.

---

## Phase 3: User Story 1 — Record Tool Invocations for Compliance (Priority: P1) MVP

**Goal**: Every MCP tool invocation over HTTP transport is persisted as an `AuditEntry` record to SQLite, capturing tool name, session ID, timestamp, parameters, outcome, duration, and error details.

**Independent Test**: Enable audit trails with SQLite, invoke any MCP tool, verify a record appears in the database with correct fields.

### Tests for User Story 1

- [ ] T015 [P] [US1] Write integration test for SQLite audit trail persistence (write `AuditEntry` via `Linq2DbAuditTrail`, read back via linq2db query) using SQLite in-memory connection in `tests/Buildout.IntegrationTests/Audit/SqliteAuditTrailTests.cs`
- [ ] T016 [P] [US1] Write integration test for audit trail filter exercising full MCP pipeline (invoke tool, verify audit entry in database) in `tests/Buildout.IntegrationTests/Audit/AuditTrailFilterTests.cs`

### Implementation for User Story 1

- [ ] T017 [US1] Implement `Linq2DbAuditTrail` using `AuditDataConnection.InsertAsync` with fire-and-forget writes via `Task.Run` in `src/Buildout.Mcp/Audit/Linq2DbAuditTrail.cs` — configure SQLite `BusyTimeout` (e.g., 5 s) on the connection to handle concurrent write contention gracefully
- [ ] T018 [US1] Implement `AuditTrailFilter` as MCP `CallToolFilter` — capture tool name, session ID, parameters, stopwatch duration, outcome, error details; fire-and-forget `IAuditTrail.RecordEntryAsync` in `src/Buildout.Mcp/Audit/AuditTrailFilter.cs`
- [ ] T019 [US1] Create `AuditMcpServiceExtensions.AddAuditTrail` DI registration method — options binding, validator, linq2db `DataConnection` with SQLite provider, FluentMigrator runner with `MigrateUp()`, `IAuditTrail` factory, filter wiring in `src/Buildout.Mcp/Audit/AuditMcpServiceExtensions.cs`
- [ ] T020 [US1] Update `src/Buildout.Mcp/Program.cs` to call `AddAuditTrail` and register `AddCallToolFilter` when audit is enabled and HTTP transport is selected

**Checkpoint**: US1 complete — tool invocations over HTTP are audit-recorded to SQLite via linq2db. Disabled path returns NullAuditTrail. Schema managed by FluentMigrator.

---

## Phase 4: User Story 2 — Choose Persistence Backend (Priority: P2)

**Goal**: Operators can choose SQLite or PostgreSQL via configuration. No code changes or rebuilds required to switch backends.

**Independent Test**: Configure each backend, invoke tools, confirm records stored in correct database with correct schema.

### Tests for User Story 2

- [ ] T021 [P] [US2] Write integration test for PostgreSQL audit trail persistence (write `AuditEntry` via `Linq2DbAuditTrail`, read back via linq2db query) using Testcontainers PostgreSQL fixture from `AuditTestFixture` in `tests/Buildout.IntegrationTests/Audit/PostgresAuditTrailTests.cs`
- [ ] T022 [P] [US2] Write integration test verifying FluentMigrator `Migration_001` creates correct schema in PostgreSQL using Testcontainers in `tests/Buildout.IntegrationTests/Audit/MigrationTests.cs`

### Implementation for User Story 2

- [ ] T023 [US2] Update `AuditMcpServiceExtensions.AddAuditTrail` in `src/Buildout.Mcp/Audit/AuditMcpServiceExtensions.cs` to register linq2db `DataConnection` with PostgreSQL provider (`UsePostgreSQL`) and FluentMigrator PostgreSQL runner when `Provider=postgresql`

**Checkpoint**: US2 complete — both SQLite and PostgreSQL backends selectable via configuration. Single `Linq2DbAuditTrail` implementation works with both providers.

---

## Phase 5: User Story 3 — Zero Impact When Disabled (Priority: P3)

**Goal**: When audit trails are disabled (default), no database connections opened, no files created, no overhead incurred. Tool invocations succeed even when audit writes fail.

**Independent Test**: Run with `Audit:Enabled=false` (or no Audit section), verify no database/file artifacts, measure latency parity.

### Tests for User Story 3

- [ ] T024 [P] [US3] Write unit test verifying `NullAuditTrail.RecordEntryAsync` completes synchronously with no side effects in `tests/Buildout.UnitTests/Audit/NullAuditTrailTests.cs`
- [ ] T025 [US3] Write integration test verifying no audit filter is registered when `Audit:Enabled=false` in `tests/Buildout.IntegrationTests/Audit/DisabledAuditTests.cs`
- [ ] T026 [US3] Write integration test verifying tool call succeeds when `IAuditTrail.RecordEntryAsync` throws (mock audit trail, verify tool result unmodified) in `tests/Buildout.IntegrationTests/Audit/AuditTrailFilterTests.cs`
- [ ] T029 [P] [US1] Write integration test benchmarking tool call latency with audit enabled vs disabled, asserting <5ms average overhead (SC-002) in `tests/Buildout.IntegrationTests/Audit/AuditLatencyTests.cs`

**Checkpoint**: US3 complete — disabled path is verified zero-overhead, audit failures never block tool calls.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T027 [P] Update `docs/configuration.md` with all 5 `Audit:*` configuration keys, types, defaults, validation rules, and `Buildout__Audit__*` env var forms
- [ ] T028 Validate all 4 quickstart.md scenarios against the implemented feature (documentation verification — confirms quickstart accuracy, not a functional requirement)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2
- **US2 (Phase 4)**: Depends on Phase 2 + US1 (extends DI registration from T019)
- **US3 (Phase 5)**: Depends on Phase 2 + US1 (adds tests alongside existing filter tests)
- **Polish (Phase 6)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: After foundational — no dependencies on other stories
- **US2 (P2)**: After foundational + US1 (extends `AuditMcpServiceExtensions` from T019)
- **US3 (P3)**: After foundational + US1 (adds tests alongside existing filter tests)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models/interfaces before data layer
- Data layer before DI registration
- DI registration before Program.cs wiring
- Story complete before moving to next priority

### Parallel Opportunities

- T002–T013: All foundational types, data layer, migration, fixture, and tests can run in parallel (different files)
- T015 + T016: US1 integration tests can run in parallel
- T021 + T022: US2 integration tests can run in parallel
- T024 + T025 + T029: US3 tests + latency benchmark can run in parallel

---

## Parallel Example: Phase 2 (Foundational)

```text
# Launch all foundational type creation in parallel:
Task T002: "Create AuditEntry record and AuditOutcome enum"
Task T003: "Create IAuditTrail interface"
Task T004: "Create AuditOptions configuration class"
Task T005: "Create AuditOptionsValidator"
Task T006: "Create NullAuditTrail"
Task T007: "Create AuditEntryRecord linq2db POCO"
Task T008: "Create AuditDataConnection"
Task T009: "Create FluentMigrator migration"
Task T010: "Write unit tests for AuditEntry"
Task T011: "Write unit tests for AuditOptionsValidator"
Task T012: "Create AuditTestFixture (Testcontainers PostgreSQL + SQLite helper)"
Task T013: "Write migration integration test (SQLite)"
```

## Parallel Example: Phase 3 (US1)

```text
# Launch US1 tests in parallel:
Task T015: "Write SQLite persistence integration test"
Task T016: "Write audit filter pipeline integration test"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T014)
3. Complete Phase 3: US1 (T015–T020)
4. **STOP and VALIDATE**: Run quickstart Scenario 1 (SQLite audit) independently
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Audit recording works with SQLite (MVP!)
3. Add US2 → Test independently → PostgreSQL backend available
4. Add US3 → Test independently → Zero-impact verified
5. Polish → Documentation complete, quickstart validated

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
