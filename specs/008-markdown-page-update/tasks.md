# Tasks: Markdown Page Update via Patch Operations

**Input**: Design documents from `/specs/008-markdown-page-update/`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ contracts/ ✅

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First
Discipline, NON-NEGOTIABLE). Unit tests in `tests/Buildout.UnitTests`; integration tests in
`tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them.

**Organization**: Phases follow user story priority order. Within each phase: tests written
first (must FAIL before implementation begins), then implementation, then integration tests.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared-state dependencies)
- **[Story]**: User story this task belongs to (US1–US5)

---

## Phase 1: Foundation Types (Blocking Prerequisites)

**Purpose**: Core types, interfaces, and DI registration that ALL user stories depend on.
Must be complete before any user story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T001 [P] Create `PatchOperation.cs` abstract record base + 5 concrete subclasses (`ReplaceBlockOperation`, `ReplaceSectionOperation`, `SearchReplaceOperation`, `AppendSectionOperation`, `InsertAfterBlockOperation`) + `JsonConverter<PatchOperation>` discriminant via `"op"` field in `src/Buildout.Core/Markdown/Editing/PatchOperations/`
- [ ] T002 [P] Create `PatchRejectedException.cs` base class + 10 patch-error subclasses (`StaleRevisionException`, `AmbiguousMatchException`, `NoMatchException`, `UnknownAnchorException`, `SectionAnchorNotHeadingException`, `AnchorNotContainerException`, `ReorderNotSupportedException`, `UnsupportedBlockTouchedException`, `LargeDeleteException`, `PartialPatchException`) in `src/Buildout.Core/Markdown/Editing/`
- [ ] T003 [P] Create `AnchoredPageSnapshot.cs`, `FetchForEditInput.cs`, `UpdatePageInput.cs`, `ReconciliationSummary.cs` in `src/Buildout.Core/Markdown/Editing/`
- [ ] T004 [P] Create `BlockSubtreeWithAnchor.cs` and `AnchorKind.cs` enum (`Root`, `Block`, `Opaque`) in `src/Buildout.Core/Markdown/Editing/Internal/`
- [ ] T005 Create `IPageEditor.cs` interface (`FetchForEditAsync` + `UpdateAsync` signatures) in `src/Buildout.Core/Markdown/Editing/`
- [ ] T006 Create `PageEditorOptions.cs` (with `LargeDeleteThreshold = 10`) and register `IPageEditor` + `IOptions<PageEditorOptions>` in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` (stub `PageEditor` may throw `NotImplementedException` until Phase 3)

**Checkpoint**: Foundation types compile — all user story phases can now proceed.

---

## Phase 2: User Story 1 — Fetch Editable Markdown Projection (Priority: P1) 🎯 MVP

**Goal**: `buildout get <id> --editing` and `get_page_markdown` return anchored Markdown,
revision token, and `unknown_block_ids`. The `get` command without `--editing` remains
byte-for-byte identical to spec 002.

**Independent Test**: With a mocked buildin client and a fixture page containing every
supported block type, assert the three outputs described in US1 acceptance scenario 1. Assert
`buildout get <id>` (without `--editing`) produces the spec 002 unanchored Markdown. Assert
two successive fetches of the same page state produce byte-identical revision tokens.

### Tests for User Story 1

> **Write these tests FIRST — they must FAIL before any implementation**

- [ ] T007 [P] [US1] Unit tests `AnchoredMarkdownRendererTests.cs` in `tests/Buildout.UnitTests/Markdown/Editing/` — (a) every supported block type emits a `<!-- buildin:block:<id> -->` anchor; (b) root sentinel `<!-- buildin:root -->` is the very first line; (c) unsupported blocks emit `<!-- buildin:opaque:<id> -->` + placeholder paragraph; (d) body stripped of anchors equals `PageMarkdownRenderer` output for the same fixture (SC-001)
- [ ] T008 [P] [US1] Unit tests `RevisionTokenComputerTests.cs` in `tests/Buildout.UnitTests/Markdown/Editing/` — (a) output is exactly 8 lowercase hex chars; (b) same input → same output; (c) different input → different output; (d) metadata-only mock state change (no Markdown change) → same token; (e) payload change → different token (SC-002, SC-002a)
- [ ] T009 [P] [US1] Unit tests `AnchoredMarkdownParserTests.cs` in `tests/Buildout.UnitTests/Markdown/Editing/` — (a) parse anchored Markdown → `BlockSubtreeWithAnchor[]` with correct `AnchorId` and `AnchorKind`; (b) root sentinel maps to `AnchorKind.Root`; (c) opaque anchors map to `AnchorKind.Opaque`; (d) nested block anchors attach to the correct tree level

### Implementation for User Story 1

- [ ] T010 [US1] Implement `RevisionTokenComputer.cs` in `src/Buildout.Core/Markdown/Editing/Internal/` using `System.IO.Hashing.Crc32.HashToUInt32(Encoding.UTF8.GetBytes(markdown)).ToString("x8")`
- [ ] T011 [US1] Implement `AnchoredMarkdownRenderer.cs` in `src/Buildout.Core/Markdown/Editing/Internal/` — re-uses `PageMarkdownRenderer`'s fetch-and-recurse pipeline; weaves anchor comments at correct nesting indentation before each block; collects `UnknownBlockIds` for opaque anchors; wraps call in `OperationRecorder.Start(logger, "page_read_editing")`
- [ ] T012 [US1] Implement `AnchoredMarkdownParser.cs` in `src/Buildout.Core/Markdown/Editing/Internal/` — Markdig parse of anchored Markdown string; walks AST using the same visitor pattern as feature 006's `MarkdownToBlocksParser`; detects preceding `HtmlBlock` siblings matching anchor patterns; produces `BlockSubtreeWithAnchor[]`
- [ ] T013 [US1] Implement `PageEditor.FetchForEditAsync` in `src/Buildout.Core/Markdown/Editing/PageEditor.cs` — calls `AnchoredMarkdownRenderer`, calls `RevisionTokenComputer.Compute`, returns `AnchoredPageSnapshot`
- [ ] T014 [P] [US1] Extend `GetCommand.cs` + `GetCommand.Settings` in `src/Buildout.Cli/Commands/GetCommand.cs` with `--editing` (bool flag) and `--print <markdown|json>` (enum, only valid with `--editing`); stdout/stderr split per `contracts/cli-update.md` matrix; `--print json` without `--editing` → exit 2
- [ ] T015 [P] [US1] Implement `GetPageMarkdownToolHandler.cs` in `src/Buildout.Mcp/Tools/` with `[McpServerTool(Name = "get_page_markdown")]`; return JSON-serialized `AnchoredPageSnapshot`; register with `.WithTools<GetPageMarkdownToolHandler>()` in `src/Buildout.Mcp/Program.cs`

### Integration Tests for User Story 1

- [ ] T016 [US1] Integration tests `GetCommandEditingTests.cs` in `tests/Buildout.IntegrationTests/Cli/` — (a) `--editing` flag + `--print json` returns structured triple; (b) `--editing` default mode writes anchored Markdown to stdout + revision to stderr; (c) `buildout get <id>` without `--editing` produces spec 002 unanchored Markdown byte-for-byte (no regression); (d) `--print json` without `--editing` exits 2
- [ ] T017 [P] [US1] Integration tests `GetPageMarkdownToolTests.cs` in `tests/Buildout.IntegrationTests/Mcp/` — (a) structured triple returned; (b) `unknown_block_ids` populated for unsupported blocks; (c) same page state → same revision as CLI `--editing` call (SC-007 partial)

**Checkpoint**: US1 complete — edit-mode fetch functional end-to-end on both surfaces.

---

## Phase 3: User Story 2 — Patch a Page with Semantic Operations (Priority: P1)

**Goal**: Five patch operation types applied in order; minimal buildin write calls; block IDs
preserved for surviving blocks. All 10 `patch.*` error classes surfaced correctly.

**Independent Test**: For each operation type, run the happy-path fixture through `buildout
update` against the WireMock fixture and assert the recorded write calls match the
reconciliation summary. For each error class, assert the call fails with exit 7 and the
correct `patch.*` class name in stderr.

### Tests for User Story 2

> **Write these tests FIRST — they must FAIL before any implementation**

- [ ] T018 [P] [US2] Unit tests `PatchApplicatorTests.cs` in `tests/Buildout.UnitTests/Markdown/Editing/` — for each of the 5 operation types: (a) happy path modifies the tree correctly; (b) every declared error class is raised for the correct input; (c) `insert_after_block` with `anchor="root"` raises `UnknownAnchorException`; (d) `append_section` against a paragraph raises `AnchorNotContainerException`; (e) operations are applied in order (op N sees state from op N-1)
- [ ] T019 [P] [US2] Unit tests `ReconcilerTests.cs` in `tests/Buildout.UnitTests/Markdown/Editing/` — (a) unchanged blocks produce zero write calls (SC-005); (b) payload-changed blocks produce exactly one `UpdateBlockAsync` call; (c) new blocks produce `AppendBlockChildrenAsync` against correct parent; (d) deleted blocks produce `DeleteBlockAsync`; (e) anchor at different parent/position → `ReorderNotSupportedException`; (f) opaque anchor removed → `UnsupportedBlockTouchedException`; (g) total write calls == `updated + new + deleted` (SC-004)
- [ ] T020 [P] [US2] Unit tests `PageEditorTests.cs` (UpdateAsync orchestration) in `tests/Buildout.UnitTests/Markdown/Editing/` — (a) operations applied in declared order; (b) first failing operation aborts with its index and zero writes; (c) partial-failure mid-reconciliation surfaces `PartialPatchException` with partial revision; (d) `OperationRecorder.Fail` called with the patch error class name for every `PatchRejectedException`

### WireMock Stubs

- [ ] T021 [US2] Add `RegisterUpdateBlock(server, blockId, updatedBlock)`, `RegisterDeleteBlock(server, blockId)`, and `RegisterUpdateBlockFailure(server, blockId, statusCode)` to `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs`

### Implementation for User Story 2

- [ ] T022 [US2] Implement `PatchApplicator.cs` in `src/Buildout.Core/Markdown/Editing/Internal/` — dispatches to per-operation handlers; `replace_block` replaces subtree including `anchor="root"` form; `replace_section` scans `parent.Children` forward from heading (same-parent boundary, heading-level stop); `search_replace` serialises tree → string replace → re-parse; `append_section` checks `BlockToMarkdownRegistry.RecurseChildren` for container predicate; `insert_after_block` rejects `anchor="root"`
- [ ] T023 [US2] Implement `Reconciler.cs` in `src/Buildout.Core/Markdown/Editing/Internal/` — diffs `BlockSubtreeWithAnchor[]` (patched) against cached `BlockSubtree[]` (original); emits minimal write-op list per FR-011 rules; detects reorders (same anchor, different parent or sibling index); counts deletions; enforces opaque-block protection; returns `ReconciliationSummary` counts
- [ ] T024 [US2] Implement `PageEditor.UpdateAsync` in `src/Buildout.Core/Markdown/Editing/PageEditor.cs` — (a) re-fetch current tree + compute current revision; (b) revision check (step 2 in `core-editor.md`); (c) parse anchored Markdown; (d) apply ops via `PatchApplicator`; (e) large-delete check (step 5); (f) dry-run early return (step 6); (g) issue minimal write calls via `Reconciler`; (h) compute `NewRevision`; (i) wrap in `OperationRecorder.Start(logger, "page_update")` with `recorder.Fail(ex.PatchErrorClass)` on rejection
- [ ] T025 [P] [US2] Implement `UpdateCommand.cs` + `UpdateSettings.cs` in `src/Buildout.Cli/Commands/`; register in `src/Buildout.Cli/Program.cs`; deserialise operations from `--ops <path|->` using `JsonSerializer`; map `PatchRejectedException` to exit 7 with class name in stderr; map other `BuildinApiException` classes to existing exit codes
- [ ] T026 [P] [US2] Implement `UpdatePageToolHandler.cs` in `src/Buildout.Mcp/Tools/` with `[McpServerTool(Name = "update_page")]`; return JSON-serialized `ReconciliationSummary`; map `PatchRejectedException` subclasses to `InvalidParams` MCP errors with `patch_error_class` in data payload; register in `src/Buildout.Mcp/Program.cs`

### Integration Tests for User Story 2

- [ ] T027 [US2] Integration tests `UpdateCommandTests.cs` in `tests/Buildout.IntegrationTests/Cli/` — (a) all 5 operation types happy-path using WireMock fixtures; (b) every `patch.*` error class surfaces as exit 7 with class name in stderr; (c) partial-failure (mid-reconciliation `UpdateBlockAsync` → 500) exits 6 with partial page state in stderr; (d) `--print json` returns full `ReconciliationSummary`
- [ ] T028 [P] [US2] Integration tests `UpdatePageToolTests.cs` in `tests/Buildout.IntegrationTests/Mcp/` — (a) all 5 operation types happy-path; (b) all `patch.*` error classes return `InvalidParams` with `patch_error_class`; (c) `patch.partial` returns `InternalError` with partial revision
- [ ] T029 [P] [US2] Contract test `UpdateReadOnlyOnOtherPagesTests.cs` in `tests/Buildout.IntegrationTests/Cross/` — across this feature's entire integration suite, WireMock receives no `POST /v1/pages`, `PATCH /v1/pages/{id}`, `POST /v1/databases`, or `PATCH /v1/databases/{id}` requests (SC-012)

**Checkpoint**: US2 complete — all 5 operation types patching and reconciling end-to-end.

---

## Phase 4: User Story 3 — Optimistic Concurrency: Stale Revisions Rejected (Priority: P1)

**Goal**: Any `update_page` call carrying a revision that no longer matches the current page
state is rejected before any write call, with the current revision in the error payload.

**Independent Test**: Fetch page (US1) → record revision `r0` → mutate the mock page state
so its revision becomes `r1` → submit a patch against `r0` → assert `patch.stale_revision`,
`current_revision = r1` in error payload, zero write calls recorded.

### Tests for User Story 3

> **Write these tests FIRST — they must FAIL before the UpdateAsync revision check is wired**

- [ ] T030 [US3] Unit tests (stale-revision path) in `tests/Buildout.UnitTests/Markdown/Editing/PageEditorTests.cs` — (a) mismatched revision → `StaleRevisionException` with `current_revision` in Details; (b) zero write calls issued; (c) dry-run with stale revision also fails (FR-015); (d) missing revision field → validation exception before any fetch

- [ ] T031 [P] [US3] Integration test scenario in `tests/Buildout.IntegrationTests/Cli/UpdateCommandTests.cs` — stale revision: CLI exit 7, stderr contains `patch.stale_revision` + `current_revision`; missing `--revision` flag: CLI exit 2

- [ ] T032 [P] [US3] Integration test scenario in `tests/Buildout.IntegrationTests/Mcp/UpdatePageToolTests.cs` — stale revision: `InvalidParams` MCP error, `patch_error_class = "patch.stale_revision"`, current revision present in data payload

**Checkpoint**: US3 complete — concurrent edits safely rejected with recovery information.

---

## Phase 5: User Story 4 — Dry-Run Preview Before Commit (Priority: P2)

**Goal**: `dry_run: true` performs the full parse→apply→diff pipeline and returns the
reconciliation summary + `post_edit_markdown` without issuing any buildin write call.

**Independent Test**: For each operation type from US2's happy-path fixtures, run with
`dry_run: true` and assert: (a) zero write calls recorded; (b) `ReconciliationSummary`
counts match the non-dry-run equivalent; (c) `post_edit_markdown` reflects the expected
post-edit state; (d) re-issuing the same call without `dry_run` produces write calls whose
count matches the dry-run preview.

### Tests for User Story 4

> **Write these tests FIRST — dry-run path must fail before it is gated in PageEditor**

- [ ] T033 [US4] Unit tests (dry-run path) in `tests/Buildout.UnitTests/Markdown/Editing/PageEditorTests.cs` — (a) `DryRun=true` → zero write calls; (b) `ReconciliationSummary` counts equal the committing-run counts; (c) `PostEditMarkdown` non-null and equals expected anchored Markdown; (d) error-class patches (`patch.unknown_anchor`, etc.) surface identically under dry-run (FR-014)

- [ ] T034 [P] [US4] Integration test scenario in `tests/Buildout.IntegrationTests/Cli/UpdateCommandTests.cs` — `--dry-run --print json` response includes `post_edit_markdown`; zero WireMock write calls; re-issuing without `--dry-run` produces matching write-call count

- [ ] T035 [P] [US4] Integration test scenario in `tests/Buildout.IntegrationTests/Mcp/UpdatePageToolTests.cs` — `dry_run=true` response includes `post_edit_markdown`; zero write calls; subsequent commit matches dry-run counts (SC-009)

**Checkpoint**: US4 complete — dry-run preview usable as a safe pre-check before any commit.

---

## Phase 6: User Story 5 — Large-Delete Safety Guard (Priority: P2)

**Goal**: Patches that would delete more than the configured threshold of blocks are rejected
unless `allow_large_delete: true` is set, preventing accidental bulk content destruction.

**Independent Test**: 20-block fixture page; `replace_block(root)` with a single paragraph →
asserts `patch.large_delete` with `would_delete = 19`, zero write calls. Re-issue with
`allow_large_delete: true` → asserts success, 19 `deleteBlock` calls recorded.

### Tests for User Story 5

> **Write these tests FIRST — large-delete guard must fail before it is gated in the Reconciler**

- [ ] T036 [US5] Unit tests (large-delete guard) in `tests/Buildout.UnitTests/Markdown/Editing/PageEditorTests.cs` — (a) deletion count > threshold without `allow_large_delete` → `LargeDeleteException` with `would_delete` and `threshold` in Details; (b) same patch with `AllowLargeDelete=true` → commits; (c) deletion count ≤ threshold → never blocked; (d) `dry_run=true` + `allow_large_delete=true` → summary returned, zero writes (SC-009 / US5 scenario 4)

- [ ] T037 [P] [US5] Integration test scenario in `tests/Buildout.IntegrationTests/Cli/UpdateCommandTests.cs` — large-delete without `--allow-large-delete` exits 7 with `patch.large_delete`; with `--allow-large-delete` commits and `deleteBlock` count matches summary (SC-008)

- [ ] T038 [P] [US5] Integration test scenario in `tests/Buildout.IntegrationTests/Mcp/UpdatePageToolTests.cs` — `patch.large_delete` MCP error; `allow_large_delete=true` commits and response counts match (SC-008)

**Checkpoint**: US5 complete — runaway patches blocked; all 5 user stories fully exercised.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Parity verification, LLM integration, observability docs, suite-wide health check.

- [ ] T039 [P] Integration tests `EditModeParityTests.cs` in `tests/Buildout.IntegrationTests/Cross/` — (a) `buildout get <id> --editing --print json` and `get_page_markdown` return the same triple for the same page state (SC-007); (b) `buildout update --print json` and `update_page` return the same `ReconciliationSummary` shape; (c) `buildout get <id>` (no `--editing`) remains byte-identical to `buildin://{page_id}` resource body (spec 002 SC-003 non-regression)
- [ ] T040 [P] Integration tests `UpdatePageRoundTripWithCheapLlmTests.cs` in `tests/Buildout.IntegrationTests/Mcp/` — LLM chains `get_page_markdown` → (derives `replace_section` op) → `update_page` → `get_page_markdown`; assert targeted block's Markdown changed AND every other block's anchor ID is preserved bit-for-bit (FR-025 / SC-010)
- [ ] T041 Update `specs/007-observability/contracts/metrics-registry.md` to add new `operation` label values (`page_read_editing`, `page_update`), new `tool` label values (`get_page_markdown`, `update_page`), and enumerate `patch.*` error class names as valid `error_type` values (contracts/observability.md is the source of truth; this is the upstream registry update)
- [ ] T042 [P] Run `dotnet test` across `Buildout.UnitTests` and `Buildout.IntegrationTests`; verify suite completes under 60 s on a developer laptop with no outbound network (SC-011); fix any failures before marking done

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Foundation) → [all user story phases]
Phase 2 (US1)        → Phase 3 (US2)
Phase 2 (US1)        → Phase 4 (US3)      [US3 builds on UpdateAsync from US2]
Phase 3 (US2)        → Phase 4 (US3)
Phase 3 (US2)        → Phase 5 (US4)      [US4 tests the dry-run path]
Phase 3 (US2)        → Phase 6 (US5)      [US5 tests the large-delete guard]
Phase 5 (US4) + Phase 6 (US5) → Phase 7 (Polish)
```

### Within Each Phase

- Tests tasks MUST be written (and fail) before implementation tasks begin.
- `T023` (`PatchApplicator`) must be complete before `T024` (`Reconciler`) and `T024` before `T025` (`PageEditor.UpdateAsync`).
- `T021` (WireMock stubs) must be in place before integration test tasks `T027`, `T028`.
- `T025` (UpdateAsync) must be complete before Phases 4–6 test tasks can pass.

### Parallel Opportunities Within Phases

**Phase 1**: T001, T002, T003, T004 all touch different files — run in parallel.

**Phase 2 (US1) — tests**:
```
T007 AnchoredMarkdownRendererTests  (parallel)
T008 RevisionTokenComputerTests     (parallel)
T009 AnchoredMarkdownParserTests    (parallel)
```

**Phase 2 (US1) — surfaces** (after T013 + T014):
```
T014  GetCommand --editing extension    (parallel)
T015  GetPageMarkdownToolHandler        (parallel)
```

**Phase 3 (US2) — unit tests**:
```
T018 PatchApplicatorTests  (parallel)
T019 ReconcilerTests        (parallel)
T020 PageEditorTests (UpdateAsync)  (parallel)
```

**Phase 3 (US2) — surfaces** (after T024):
```
T025 UpdateCommand  (parallel)
T026 UpdatePageToolHandler  (parallel)
```

---

## Parallel Example: User Story 1 Unit Tests

```
# Launch all US1 unit test tasks simultaneously (all touch different files):
Agent("T007: Write AnchoredMarkdownRendererTests")
Agent("T008: Write RevisionTokenComputerTests")
Agent("T009: Write AnchoredMarkdownParserTests")
```

## Parallel Example: User Story 2 Unit Tests

```
# Launch all US2 unit test tasks simultaneously:
Agent("T018: Write PatchApplicatorTests")
Agent("T019: Write ReconcilerTests")
Agent("T020: Write PageEditorTests (UpdateAsync orchestration)")
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 only — all P1)

1. Complete Phase 1: Foundation types.
2. Complete Phase 2 (US1): Fetch editable Markdown → end-to-end.
3. Complete Phase 3 (US2): Patch operations → end-to-end.
4. Complete Phase 4 (US3): Stale-revision guard → revision check confirmed.
5. **STOP and VALIDATE**: Run `dotnet test`; verify all three P1 user stories pass.
6. Demo/deploy MVP — all core edit operations available.

### Incremental Delivery

1. Foundation → US1 (edit-mode fetch) → US2 (patch) → US3 (concurrency) → **P1 MVP**
2. Add US4 (dry-run) → verify independently.
3. Add US5 (large-delete guard) → verify independently.
4. Polish phase → parity + LLM integration + observability docs.

### Parallel Team Strategy

With multiple developers (after Phase 1 complete):

- Developer A: US1 (T007–T017)
- Developer B: US2 unit tests + WireMock stubs (T018–T021) in parallel with Developer A
- Developer B continues: US2 implementation (T022–T029) once US1 complete (US2 re-uses AnchoredMarkdownRenderer from US1)

---

## Notes

- `[P]` tasks touch distinct files with no shared in-progress dependencies.
- Each user story checkpoint is a verifiable `dotnet test` run against the WireMock fixture.
- TDD order is enforced within each phase: test tasks → implementation tasks → integration test tasks.
- US3/US4/US5 test phases add focused coverage on top of the `UpdateAsync` implementation from US2; their tests will fail until `PageEditor.UpdateAsync` is complete.
- No real buildin.ai API calls in any test (Constitution Principle IV).
- Do not commit secrets, `.env` files, or test API tokens.
