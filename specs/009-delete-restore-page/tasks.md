---
description: "Task list for Page Delete and Restore (feature 009)"
---

# Tasks: Page Delete and Restore

**Input**: Design documents from `/specs/009-delete-restore-page/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline,
NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and
integration tests in `tests/Buildout.IntegrationTests`. Tests are written RED before any
implementation. No tests are deleted, disabled, or skipped to make a build pass.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no open dependencies on incomplete tasks)
- **[Story]**: User story label — [US1], [US2], [US3]
- Every task includes an exact file path

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Core types and WireMock infrastructure that every user story depends on. No new
projects and no new NuGet packages — this feature extends existing projects only.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 Create `src/Buildout.Core/PageLifecycle/PageLifecycleOutcome.cs` — full `PageLifecycleOutcome` sealed record with all five fields (`PageId`, `Archived`, `Changed`, `FailureClass?`, `UnderlyingException?`), using statement for `Buildout.Core.Markdown.Authoring.FailureClass`, and state-invariant XML doc comments per `data-model.md` (also establishes the `PageLifecycle/` namespace directory)
- [X] T002 Create `src/Buildout.Core/PageLifecycle/IPageLifecycle.cs` — full `IPageLifecycle` interface with `DeleteAsync(string pageId, CancellationToken)` and `RestoreAsync(string pageId, CancellationToken)` signatures and XML doc per `contracts/core-lifecycle.md` (depends on T001 for `PageLifecycleOutcome` type)
- [X] T003 [P] Add `RegisterGetPageArchived(pageId, archived)` and `RegisterUpdatePageToggleArchived(pageId)` WireMock helpers to `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs` — follow the existing `RegisterGetPage` / `RegisterUpdatePage` builder pattern from spec 008; `RegisterUpdatePageToggleArchived` must assert the inbound PATCH body contains only the `archived` field (no `properties`/`icon`/`cover`) per `contracts/buildin-endpoints.md`

**Checkpoint**: `IPageLifecycle`, `PageLifecycleOutcome`, and toggleable WireMock stubs are ready. User story test files can now be created (red phase).

---

## Phase 2: User Story 1 — Delete (archive) a page (Priority: P1) 🎯 MVP

**Goal**: `DeleteAsync` core service, `DeleteCommand` CLI command, and `DeletePageToolHandler` MCP tool are fully implemented and all tests pass.

**Independent Test**: With a WireMock stub returning `archived: false` on GET then `archived: true` on PATCH, `buildout delete <page_id>` exits 0 with `changed=true`. A second invocation against the same now-archived page exits 0 with `changed=false` and issues zero PATCH calls (observable by asserting the WireMock journal).

### Tests for User Story 1 (RED phase — write before any implementation)

> **Write these tests FIRST. Confirm they compile but FAIL assertions (or throw `NotImplementedException`) before proceeding to implementation.**

- [X] T004 [US1] Create `tests/Buildout.UnitTests/PageLifecycle/PageLifecycleTests.cs` — failing unit tests for `DeleteAsync`: (a) happy-path state change asserts one `GetPageAsync` + one `UpdatePageAsync` call; (b) no-op short-circuit on already-archived page asserts zero `UpdatePageAsync` calls via NSubstitute `DidNotReceive`; (c) PATCH body shape — `NSubstitute.Arg.Is<UpdatePageRequest>` asserts `Archived == true`, `Properties == null`, `Icon == null`, `Cover == null` (P3 in `contracts/core-lifecycle.md`); (d) FailureClass mapping for 401, 403, 404, 5xx, and `TransportError` per P5; (e) `OperationRecorder` invoked with operation name `page_delete` and `changed` tag per P4
- [X] T005 [P] [US1] Create `tests/Buildout.IntegrationTests/Cli/DeleteCommandTests.cs` — failing CLI integration tests via WireMock: happy path exits 0 with `--print summary` output matching `"Deleted page <id>: archived=true (changed=true)"`; idempotent no-op exits 0 with `(changed=false, no-op)`; `page_not_found` exits 3; `permission_denied` exits 4; transport error exits 5; `--print json` output matches `{"pageId":"...","archived":true,"changed":true}` per `contracts/cli-delete-restore.md`
- [X] T006 [P] [US1] Create `tests/Buildout.IntegrationTests/Mcp/DeletePageToolTests.cs` — failing MCP integration tests: success path returns `CallToolResult` with `ResourceLinkBlock` (`buildin://<page_id>`) and `TextContentBlock` JSON `{"page_id":"...","archived":true,"changed":true}` per `contracts/mcp-delete-restore.md`; no-op returns `changed: false`; `NotFound` FailureClass → `McpErrorCode.ResourceNotFound`; `Auth`/`Transport`/`Unexpected` → `McpErrorCode.InternalError` with correct message prefix; instrumentation tags `tool=delete_page`, `outcome=success|failure`

### Implementation for User Story 1

- [X] T007 [US1] Create `src/Buildout.Core/PageLifecycle/PageLifecycle.cs` — implement `PageLifecycle : IPageLifecycle` with `DeleteAsync` only: (1) start `OperationRecorder` with operation name `page_delete`; (2) call `IBuildinClient.GetPageAsync`; (3) if `page.Archived == true`, set `changed=false` tag, call `recorder.Succeed()`, return no-op outcome; (4) otherwise call `IBuildinClient.UpdatePageAsync` with `new UpdatePageRequest { Archived = true }` (all other fields unset); (5) set `changed=true` tag, call `recorder.Succeed()` with `status_code=200`, return success outcome; (6) catch `BuildinApiException` variants and map to `FailureClass` per P5 in `contracts/core-lifecycle.md`; `RestoreAsync` body throws `NotImplementedException` (stub, filled in Phase 3)
- [X] T008 [US1] Register `IPageLifecycle → PageLifecycle` as singleton in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` via `services.AddSingleton<IPageLifecycle, PageLifecycle>()` — place after the existing page-service registrations (depends on T007)
- [X] T009 [P] [US1] Create `src/Buildout.Cli/Commands/DeleteSettings.cs` — `DeleteSettings : CommandSettings` with `[CommandArgument(0, "<page_id>")]` positional string and `[CommandOption("--print")]` option defaulting to `"summary"`, following `UpdateSettings.cs` as reference
- [X] T010 [US1] Create `src/Buildout.Cli/Commands/DeleteCommand.cs` — `DeleteCommand : AsyncCommand<DeleteSettings>` that resolves `page_id` from settings, calls `IPageLifecycle.DeleteAsync`, renders `summary` or `json` output to stdout, returns exit code per the mapping in `contracts/cli-delete-restore.md` (0 for success including no-op, 3/4/5/6 for error classes); follow `UpdateCommand.cs` for DI constructor and exit-code pattern (depends on T009, T008)
- [X] T011 [US1] Register `DeleteCommand` in `src/Buildout.Cli/Program.cs` via `config.AddCommand<DeleteCommand>("delete")` alongside the existing commands (depends on T010)
- [X] T012 [P] [US1] Create `src/Buildout.Mcp/Tools/DeletePageToolHandler.cs` — `[McpServerToolType]` sealed class; constructor injects `IPageLifecycle`; `[McpServerTool(Name = "delete_page")]` method accepting `[Description("Buildin page id to archive.")] string page_id` and returning `Task<CallToolResult>`; on success build `CallToolResult` with `ResourceLinkBlock` + `TextContentBlock` JSON per `contracts/mcp-delete-restore.md`; on `FailureClass != null` throw `McpProtocolException` per error-mapping table; emit `BuildoutMeter.McpToolInvocationsTotal` and `McpToolDuration` via `Stopwatch` following `CreatePageToolHandler` pattern (depends on T008)
- [X] T013 [US1] Register `DeletePageToolHandler` in `src/Buildout.Mcp/Program.cs` via `.WithTools<DeletePageToolHandler>()` in the existing `.AddMcpServer()` chain (depends on T012)

**Checkpoint**: All T004–T006 tests pass. `buildout delete <page_id>` and `delete_page` MCP tool are fully functional. US1 MVP is deliverable.

---

## Phase 3: User Story 2 — Restore (un-archive) a previously deleted page (Priority: P1)

**Goal**: `RestoreAsync` core service, `RestoreCommand` CLI command, and `RestorePageToolHandler` MCP tool are fully implemented — symmetric to US1.

**Independent Test**: With a WireMock stub returning `archived: true` on GET then `archived: false` on PATCH, `buildout restore <page_id>` exits 0 with `changed=true`. A second invocation on the now-active page exits 0 with `changed=false` and issues zero PATCH calls.

### Tests for User Story 2 (RED phase — write before any implementation)

> **Write these tests FIRST. Confirm they FAIL before proceeding to implementation.**

- [X] T014 [US2] Add `RestoreAsync` failing unit tests to `tests/Buildout.UnitTests/PageLifecycle/PageLifecycleTests.cs` — symmetric mirror of T004: happy-path state change (one GET + one PATCH, `Archived = false`); no-op short-circuit on already-active page (zero PATCH calls); PATCH body asserts `Archived == false` and all other fields null; FailureClass mapping for all error classes; `OperationRecorder` with operation name `page_restore` and `changed` tag
- [X] T015 [P] [US2] Create `tests/Buildout.IntegrationTests/Cli/RestoreCommandTests.cs` — symmetric to `DeleteCommandTests.cs` (T005): happy path exits 0 with `"Restored page <id>: archived=false (changed=true)"`; no-op exits 0 with `(changed=false, no-op)`; all error class exit codes; `--print json` matches `{"pageId":"...","archived":false,"changed":true}` per `contracts/cli-delete-restore.md`
- [X] T016 [P] [US2] Create `tests/Buildout.IntegrationTests/Mcp/RestorePageToolTests.cs` — symmetric to `DeletePageToolTests.cs` (T006): success path `TextContentBlock` JSON has `archived: false`; no-op returns `changed: false`; each error class → correct `McpErrorCode`; instrumentation tags `tool=restore_page`

### Implementation for User Story 2

- [X] T017 [US2] Implement `RestoreAsync` in `src/Buildout.Core/PageLifecycle/PageLifecycle.cs` — replace the T007 `NotImplementedException` stub with the symmetric implementation: `OperationRecorder` with operation name `page_restore`; pre-read; no-op short-circuit if `page.Archived == false`; PATCH with `new UpdatePageRequest { Archived = false }`; FailureClass mapping identical to `DeleteAsync`; `changed` and `status_code` tags identical pattern
- [X] T018 [P] [US2] Create `src/Buildout.Cli/Commands/RestoreSettings.cs` — symmetric to `DeleteSettings.cs` (T009): positional `<page_id>` + `--print summary|json` flag
- [X] T019 [US2] Create `src/Buildout.Cli/Commands/RestoreCommand.cs` — symmetric to `DeleteCommand.cs` (T010): calls `IPageLifecycle.RestoreAsync`, renders output per `contracts/cli-delete-restore.md`, same exit-code mapping (depends on T018, T008)
- [X] T020 [US2] Register `RestoreCommand` in `src/Buildout.Cli/Program.cs` via `config.AddCommand<RestoreCommand>("restore")` alongside the T011 delete registration (depends on T019)
- [X] T021 [P] [US2] Create `src/Buildout.Mcp/Tools/RestorePageToolHandler.cs` — symmetric to `DeletePageToolHandler.cs` (T012): `[McpServerTool(Name = "restore_page")]`; description cross-references `delete_page` and states the operation un-archives per FR-005; `TextContentBlock` JSON has `archived: false`; same FailureClass → `McpProtocolException` mapping; same `BuildoutMeter` recording (depends on T008)
- [X] T022 [US2] Register `RestorePageToolHandler` in `src/Buildout.Mcp/Program.cs` via `.WithTools<RestorePageToolHandler>()` after the T013 delete registration (depends on T021)

**Checkpoint**: All T014–T016 tests pass. `buildout restore <page_id>` and `restore_page` MCP tool are fully functional. Delete+restore round-trip works end-to-end.

---

## Phase 4: User Story 3 — Consistent shell scripting and chaining (Priority: P2)

**Goal**: Cross-cutting tests that verify the full delete→restore round-trip (SC-004), CLI↔MCP output symmetry (SC-005), and LLM tool-selection accuracy across 10 phrasings (SC-006).

**Independent Test**: `DeleteRestoreSymmetryTests` asserts a delete→restore cycle leaves the page in its original archived state with both commands exiting 0. `ToolSelectionWithCheapLlmTests` asserts ≥ 9/10 correct selections across the R7 prompt benchmark.

### Tests for User Story 3 (RED phase)

> **These tests depend on US1 + US2 being implemented. Write them after Phase 3 completes; confirm they FAIL before the implementation (verification) step.**

- [X] T023 [US3] Create `tests/Buildout.IntegrationTests/Cross/DeleteRestoreSymmetryTests.cs` — SC-004: WireMock round-trip `buildout delete <id>` then `buildout restore <id>`, both exit 0, page ends up in original archived state; SC-005: assert `--print json` output from `DeleteCommand` is byte-identical to the `TextContentBlock` JSON from `DeletePageToolHandler` for the same WireMock state; assert `error_class` strings from CLI stderr match `McpProtocolException.Message` prefix pattern for the same error type (NotFound case at minimum)
- [X] T024 [P] [US3] Extend or create `tests/Buildout.IntegrationTests/Mcp/ToolSelectionWithCheapLlmTests.cs` — SC-006: 10-prompt benchmark per `research.md R7` (5 delete phrasings: "delete the page", "archive this page", "remove this page from the workspace", "trash this page", "soft-delete this page"; 5 restore phrasings: "restore the deleted page", "undo the delete", "un-archive the page", "bring this page back from trash", "recover the archived page"); assert ≥ 9/10 correct `delete_page`/`restore_page` selections using the cheap LLM configured in spec 007/008; if the file already exists, add the 10 new cases to the existing fixture rather than creating a duplicate

### Verification for User Story 3

No new production code required — if T023–T024 reveal any output-format divergence between the CLI and MCP layers, fix the discrepancy as a defect in the relevant Phase 2 or Phase 3 task (T010/T012 or T019/T021).

**Checkpoint**: All T023–T024 tests pass. Full SC-001 through SC-006 coverage achieved.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Record invariant tests, serialization round-trip, and final full-suite verification.

- [X] T025 [P] Create `tests/Buildout.UnitTests/PageLifecycle/PageLifecycleOutcomeTests.cs` — record equality (two outcomes with identical field values are equal); `System.Text.Json` round-trip (serialize then deserialize preserves all non-null fields using camelCase policy); state-invariant assertions from `data-model.md`: `FailureClass == null ⟹ Archived != null`, `Changed == true ⟹ FailureClass == null`, `FailureClass != null ⟹ UnderlyingException != null`
- [X] T026 Run `dotnet test` from the solution root and confirm zero failures, zero skipped tests, and that new test classes appear under `Buildout.UnitTests.PageLifecycle`, `Buildout.IntegrationTests.Cli`, `Buildout.IntegrationTests.Mcp`, and `Buildout.IntegrationTests.Cross` namespaces
- [X] T027 [P] Review `specs/009-delete-restore-page/plan.md` Complexity Tracking section and update it if any implementation complexity was encountered (scope creep, unexpected API behaviour, non-trivial workarounds); leave it as "No violations" if nothing unexpected arose

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — start immediately
- **Phase 2 (US1)**: Depends on Phase 1 completion — blocks all US1 test compilation
- **Phase 3 (US2)**: Depends on Phase 1 completion; T017 edits `PageLifecycle.cs` created by T007 so US2 implementation is sequential with US1 implementation; US2 test files (T014–T016) are distinct from US1's and can be written as soon as Phase 1 is done
- **Phase 4 (US3)**: Depends on Phase 2 + Phase 3 completion — cross-tests require both operations to be working
- **Phase 5 (Polish)**: Depends on all prior phases

### User Story Dependencies

- **US1 tests (T004–T006)**: Start after T001–T003
- **US1 implementation (T007–T013)**: Start after T004–T006 are confirmed failing
- **US2 tests (T014–T016)**: Can start after T001–T003 (parallel with US1 implementation)
- **US2 implementation (T017–T022)**: Start after T014–T016 are confirmed failing; T017 edits the same `PageLifecycle.cs` as T007 — must be sequential
- **US3 tests (T023–T024)**: Start after T022 is complete

### Within Each User Story

1. Tests written and confirmed FAILING before any implementation (Constitution Principle IV)
2. Core service (`PageLifecycle.cs`) before DI registration and presentation layers
3. DI registration (`ServiceCollectionExtensions.cs`) before CLI and MCP layers consume the interface
4. Settings class before its Command class
5. Tool handler before its `Program.cs` registration

---

## Parallel Execution Examples

### Phase 1

```text
Sequential: T001 → T002
Parallel:   T003 (independent — different file)
```

### Phase 2 (US1) — after Phase 1 complete

```text
RED phase (run together):
  T004 [US1]        PageLifecycleTests.cs (delete cases)
  T005 [P] [US1]    DeleteCommandTests.cs
  T006 [P] [US1]    DeletePageToolTests.cs

GREEN phase (after T004–T006 confirmed failing):
  T007 [US1]        PageLifecycle.DeleteAsync
  T008 [US1]        DI registration (depends on T007)

  T009 [P] [US1]    DeleteSettings.cs         ─┐ (after T008)
  T012 [P] [US1]    DeletePageToolHandler.cs  ─┘

  T010 [US1]        DeleteCommand.cs          (after T009)
  T011 [US1]        CLI Program.cs            (after T010)
  T013 [US1]        MCP Program.cs            (after T012)
```

### Phase 3 (US2) — after Phase 2 complete

```text
RED phase (run together):
  T014 [US2]        PageLifecycleTests.cs restore cases
  T015 [P] [US2]    RestoreCommandTests.cs
  T016 [P] [US2]    RestorePageToolTests.cs

GREEN phase (after T014–T016 confirmed failing):
  T017 [US2]        PageLifecycle.RestoreAsync (edits T007's file)

  T018 [P] [US2]    RestoreSettings.cs        ─┐ (after T017/T008)
  T021 [P] [US2]    RestorePageToolHandler.cs ─┘

  T019 [US2]        RestoreCommand.cs         (after T018)
  T020 [US2]        CLI Program.cs            (after T019)
  T022 [US2]        MCP Program.cs            (after T021)
```

### Phase 4 (US3) — after Phase 3 complete

```text
  T023 [US3]        DeleteRestoreSymmetryTests.cs
  T024 [P] [US3]    ToolSelectionWithCheapLlmTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 — Delete only)

1. Complete Phase 1 (Foundational)
2. Complete Phase 2 (US1 Delete) — `buildout delete` and `delete_page` fully working
3. **STOP and VALIDATE**: run `quickstart.md` Scenario 1 manually; check `buildout.operations.total{operation="page_delete"}` in spec 007 dashboard
4. Ship if restore is not yet urgently required

### Incremental Delivery

1. Phase 1 → foundation ready
2. Phase 2 → Delete MVP, deployable, spec 007 shows `page_delete`
3. Phase 3 → Restore deployed, spec 007 shows `page_restore`
4. Phase 4 → SC-004/SC-005/SC-006 cross-tests green
5. Phase 5 → Full suite green, plan.md finalized

---

## Task → Success Criteria Mapping

| Task(s) | Success Criterion |
|---------|------------------|
| T004 (no-op sub-test) | SC-003 |
| T005 | SC-004 (CLI chain) |
| T006 | SC-001, SC-002, SC-005 |
| T023 | SC-004, SC-005 |
| T024 | SC-006 |
| T005 + T006 + T015 + T016 | SC-001, SC-002 (end-to-end) |

---

## Notes

- All integration tests use WireMock stubs — no real buildin network calls (Constitution Principle IV)
- `[P]` means the task touches a different file with no incomplete dependencies in the same phase
- If any failing test can't be resolved, investigate the cause — never delete, skip, or disable it
- Commit after each phase checkpoint: optional git hook is available (`/speckit-git-commit`)
