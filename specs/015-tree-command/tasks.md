---

description: "Task list for the tree command feature"
---

# Tasks: Tree Command

**Input**: Design documents from `/specs/015-tree-command/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them (Red-Green-Refactor).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in each description

---

## Phase 1: Setup (Core Value Types & Exceptions)

**Purpose**: Create all immutable data types and exception types that every subsequent phase depends on. No logic, no external dependencies — just types.

- [X] T001 Create directory structure `src/Buildout.Core/PageTree/`, `src/Buildout.Core/PageTree/Rendering/`, `src/Buildout.Core/PageTree/Errors/`, and `tests/Buildout.UnitTests/PageTree/`
- [X] T002 Implement `TreeNode` sealed record with `Name`, `Uri`, `IReadOnlyList<TreeNode> Children` (never null, leaves use `Array.Empty<TreeNode>()`) in `src/Buildout.Core/PageTree/TreeNode.cs`
- [X] T003 [P] Implement `TreeNodeKind` internal enum (`Page`, `Database`, `Unavailable`) in `src/Buildout.Core/PageTree/TreeNodeKind.cs`
- [X] T004 [P] Implement `TreeFormat` public enum (`Ascii`, `Json`) in `src/Buildout.Core/PageTree/TreeFormat.cs`
- [X] T005 Implement `TreeDepth` static class with `Min = 1`, `Max = 7`, `Default = 3`, and `Validate(int depth)` throwing `TreeDepthOutOfRangeException` with message `"depth must be between 1 and 7 (inclusive); got {value}"` in `src/Buildout.Core/PageTree/TreeDepth.cs`
- [X] T006 [P] Implement `TreeDepthOutOfRangeException` with message `"depth must be between 1 and 7 (inclusive); got {value}"` in `src/Buildout.Core/PageTree/Errors/TreeDepthOutOfRangeException.cs`
- [X] T007 [P] Implement `TreeRootNotFoundException` with message `"page or database not found: {id}"` wrapping `BuildinApiException` as inner exception in `src/Buildout.Core/PageTree/Errors/TreeRootNotFoundException.cs`
- [X] T008 [P] Implement `TreeCycleDetectedException` with message `"cycle detected in page hierarchy at node {id}"` in `src/Buildout.Core/PageTree/Errors/TreeCycleDetectedException.cs`

**Checkpoint**: All value types and exceptions compile. No logic yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Declare interfaces, write `TreeDepth` unit tests, and wire DI registration. No user story work can begin until this phase is complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T009 Declare `IPageTreeService` interface with `Task<TreeNode> BuildAsync(string targetId, int depth, CancellationToken cancellationToken = default)` in `src/Buildout.Core/PageTree/IPageTreeService.cs`
- [X] T010 [P] Declare `ITreeRenderer` interface with `TreeFormat Format { get; }` and `string Render(TreeNode root)` in `src/Buildout.Core/PageTree/Rendering/ITreeRenderer.cs`
- [X] T011 Write `TreeDepthTests.cs` verifying `TreeDepth.Validate()` accepts 1–7, throws `TreeDepthOutOfRangeException` for 0 and 8, and that `Min`/`Max`/`Default` are 1/7/3 in `tests/Buildout.UnitTests/PageTree/TreeDepthTests.cs`
- [X] T012 Register `IPageTreeService`, `AsciiTreeRenderer`, `JsonTreeRenderer`, and an `IReadOnlyDictionary<TreeFormat, ITreeRenderer>` in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` (mirroring the `IDatabaseViewStyle` keyed-dictionary pattern already in this file)

**Checkpoint**: Foundation ready — `dotnet build` succeeds, `TreeDepthTests` pass.

---

## Phase 3: User Story 1 — Get a Visual Overview of a Page Hierarchy (Priority: P1) 🎯 MVP

**Goal**: Implement the full ASCII-tree pipeline end-to-end: traversal service, ASCII renderer, and CLI command. Default invocation (`buildout-cli tree <id>`) produces a correct, pasteable ASCII tree.

**Independent Test**: Invoke `buildout-cli tree <known-page-with-sub-pages>` with no options and verify the output is a valid ASCII tree with `├──`, `└──`, `│` connectors, each node as a `[Name](<URI>)` markdown link, only sub-pages and databases included (no paragraphs/lists).

### Tests for User Story 1 ⚠️ Write FIRST, verify they FAIL before implementation

- [X] T013 [P] [US1] Write `PageTreeServiceTests.cs` covering: depth=1/3/7 traversal depths; mixed page+database children; database root; `(untitled)` for empty names; `(unavailable)` for descendant read failure; cycle detection aborts with `TreeCycleDetectedException`; root failure aborts with `TreeRootNotFoundException`; sibling order matches API order; `child_page`/`child_database` blocks included, all other block types excluded in `tests/Buildout.UnitTests/PageTree/PageTreeServiceTests.cs`
- [X] T014 [P] [US1] Write `AsciiTreeRendererTests.cs` covering: `├──` and `└──` glyphs; `│   ` continuation glyph; four-space closed-branch gutter; single-node tree (no connectors); `]` `[` `\` escaped in name; angle-bracket URI form; `(untitled)` rendered correctly; `(unavailable)` rendered correctly; multi-level nesting produces correct glyph columns in `tests/Buildout.UnitTests/PageTree/AsciiTreeRendererTests.cs`
- [X] T015 [US1] Write `TreeCommandTests.cs` covering: exit code 0 on success; exit code 2 for `--depth 0` and `--depth 8` with message naming range; exit code 3 for `TreeRootNotFoundException`; exit code 4 for auth failure; exit code 5 for transport failure; exit code 6 for unexpected buildin error; exit code 7 for `TreeCycleDetectedException`; rendered output goes to stdout; error messages go to stderr; default `--format ascii`; `--format json` selects JSON renderer in `tests/Buildout.UnitTests/Cli/TreeCommandTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Implement `PageTreeService.BuildAsync`: call `TreeDepth.Validate(depth)` first; try `GetPageAsync` on root (fallback to `GetDatabaseAsync` on 404); DFS traversal with `HashSet<string>` visited-set; filter block children to `ChildPageBlock`/`ChildDatabaseBlock`; database nodes use `QueryDatabaseAsync`; descendant failures caught and rendered as `(unavailable)` leaf with `LogWarning`; stop descending when `currentDepth == depth` in `src/Buildout.Core/PageTree/PageTreeService.cs`
- [X] T017 [P] [US1] Implement `AsciiTreeRenderer.Render`: iterative stack of `isLastChild` booleans; `├── ` / `└── ` for child prefix; `│   ` / `    ` for ancestor continuation columns; escape `]`, `[`, `\` in name; angle-bracket URI form `[Name](<URL>)`; normalize newlines in names to a single space; no trailing newline after final line in `src/Buildout.Core/PageTree/Rendering/AsciiTreeRenderer.cs`
- [X] T018 [P] [US1] Create `TreeSettings` class with required `<page_id>` positional argument, `--format` option (default `ascii`, enum `ascii|json`), `--depth` option (default `3`), inheriting from `BuildoutCommandSettings` in `src/Buildout.Cli/Commands/TreeSettings.cs`
- [X] T019 [US1] Create `TreeCommand : AsyncCommand<TreeSettings>` that resolves `IPageTreeService` and `IReadOnlyDictionary<TreeFormat, ITreeRenderer>`, calls `BuildAsync`, picks renderer by format, writes output to stdout; maps `TreeDepthOutOfRangeException`→2, `TreeRootNotFoundException`→3, auth→4, transport→5, other buildin→6, `TreeCycleDetectedException`→7 in `src/Buildout.Cli/Commands/TreeCommand.cs`
- [X] T020 [US1] Register `TreeCommand` under the name `"tree"` in `src/Buildout.Cli/Program.cs`

**Checkpoint**: `buildout-cli tree <id>` returns a correct ASCII tree. All Phase 3 tests pass.

---

## Phase 4: User Story 2 — Consume the Tree Programmatically (Priority: P2)

**Goal**: Add the JSON renderer and expose the `tree` MCP tool. The same `IPageTreeService` and `IReadOnlyDictionary<TreeFormat, ITreeRenderer>` are reused; `--format json` on CLI and `format="json"` on MCP both work identically.

**Independent Test**: Invoke the MCP `tree` tool with `format="json"` against a page with sub-pages; parse the output as JSON; verify root contains `name`, `uri`, `children`; verify leaf nodes have `children: []` (never absent); verify the same node set as the ASCII output for the same request.

### Tests for User Story 2 ⚠️ Write FIRST, verify they FAIL before implementation

- [X] T021 [P] [US2] Write `JsonTreeRendererTests.cs` covering: property order `name`/`uri`/`children`; leaf nodes have `children: []` not missing; recursive nesting; camelCase field names; UTF-8 names preserved verbatim (no HTML or markdown escaping); pretty-printed (`WriteIndented`); trailing newline present in `tests/Buildout.UnitTests/PageTree/JsonTreeRendererTests.cs`
- [X] T022 [US2] Write `TreeToolTests.cs` cheap-LLM MCP integration test: verifies tool name `"tree"` is registered; description contains `ascii`, `json`, depth range `1`–`7`; ASCII output matches expected tree shape; JSON output parses correctly; `InvalidParams` for out-of-range depth; `InvalidParams` for unknown root; `InternalError` for cycle; telemetry counters incremented in `tests/Buildout.IntegrationTests/Mcp/TreeToolTests.cs`

### Implementation for User Story 2

- [X] T023 [US2] Implement `JsonTreeRenderer.Render` using `System.Text.Json` with `JsonNamingPolicy.CamelCase`, `WriteIndented = true`; serialize properties in fixed order `name`/`uri`/`children`; `children` always present as `[]` on leaves; trailing `\n` at end of output in `src/Buildout.Core/PageTree/Rendering/JsonTreeRenderer.cs`
- [X] T024 [US2] Create `TreeToolHandler` with `[McpServerTool(Name = "tree")]`; parameters `page_id` (string, required), `format` (string, default `"ascii"`), `depth` (int, default `3`); validate format string against `TreeFormat` enum values; call `TreeDepth.Validate`; call `IPageTreeService.BuildAsync`; pick renderer from keyed dict; map exceptions to `McpProtocolException` per the MCP contract (`InvalidParams` for not-found/depth/format, `InternalError` for auth/transport/cycle/other); record `BuildoutMeter.McpToolInvocationsTotal` and `McpToolDuration` with `{ "tool": "tree" }` tags in `src/Buildout.Mcp/Tools/TreeToolHandler.cs`
- [X] T025 [US2] Register `.WithTools<TreeToolHandler>()` in `src/Buildout.Mcp/Program.cs`

**Checkpoint**: `--format json` on CLI and `format="json"` on MCP both return valid JSON. `TreeToolTests` pass (MCP merge gate satisfied). All Phase 4 tests pass.

---

## Phase 5: User Story 3 — Control Traversal Depth (Priority: P3)

**Goal**: Verify depth control works correctly at all boundaries on both surfaces. The traversal depth logic was implemented in Phase 3 (`PageTreeService`) and plumbed through CLI (`TreeSettings`) and MCP (`TreeToolHandler`). This phase adds explicit boundary-condition tests and confirms error messages match spec exactly.

**Independent Test**: Invoke `buildout-cli tree <7-level-deep-page> --depth 1` and verify only root + direct children appear. Invoke with `--depth 8` and verify exit code 2 with message `"depth must be between 1 and 7 (inclusive); got 8"`. Invoke with `--depth 7` and verify traversal proceeds to 7 levels.

### Tests for User Story 3 ⚠️ Write FIRST, verify they FAIL before implementation

- [X] T026 [P] [US3] Extend `PageTreeServiceTests.cs` with depth boundary cases: `depth=1` returns root + direct children only (no grandchildren); `depth=7` traverses all 7 levels; `depth=0` throws `TreeDepthOutOfRangeException` before any network call; `depth=8` throws `TreeDepthOutOfRangeException` before any network call; `depth=3` on a 5-level hierarchy returns exactly 3 levels in `tests/Buildout.UnitTests/PageTree/PageTreeServiceTests.cs`
- [X] T027 [P] [US3] Extend `TreeCommandTests.cs` with: `--depth 0` exits 2 with message containing `"depth must be between 1 and 7 (inclusive); got 0"`; `--depth 8` exits 2 with message containing `"depth must be between 1 and 7 (inclusive); got 8"`; omitting `--depth` behaves identically to `--depth 3`; `--depth 7` exits 0 in `tests/Buildout.UnitTests/Cli/TreeCommandTests.cs`

### Implementation for User Story 3

- [X] T028 [US3] Review `PageTreeService.BuildAsync` in `src/Buildout.Core/PageTree/PageTreeService.cs` to confirm: `TreeDepth.Validate(depth)` is the first statement (before any `IBuildinClient` call); the recursion/loop stops descending when `currentDepth == depth`; no off-by-one error (depth=1 yields root + one level of children); fix any discovered issues

**Checkpoint**: All depth boundary tests pass on both CLI and MCP. `--depth 0`/`--depth 8` both reject with the exact error message from the spec.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Skill documentation, SKILL.md update, and final validation run.

- [X] T029 [P] Create `tree.md` skill reference documenting syntax `buildout-cli tree <page_id> [--format ascii|json] [--depth N]`, all options with defaults, output format descriptions, exit code table, and usage examples in `src/Buildout.Cli/Skills/tree.md`
- [X] T030 Update `SKILL.md` to add `tree` to the Quick Reference table and add `tree.md` to the Reference Files list in `src/Buildout.Cli/Skills/SKILL.md`
- [X] T031 Run the quickstart.md validation checklist: `buildout-cli tree <id> --depth 1` returns root + direct children only; `buildout-cli tree <id> --format json | jq -e '.children[0].children'` returns an array (never null); `buildout-cli tree <page-with-mixed-blocks>` shows only sub-pages and databases; `buildout-cli tree <database-id>` renders database as root; all `tests/Buildout.UnitTests/PageTree/` tests pass; `tests/Buildout.IntegrationTests/Mcp/TreeToolTests.cs` passes; `docs/configuration.md` is unchanged

---

## Phase 7: Post-Review Fixes

**Purpose**: Address issues identified in the pre-merge code review.

- [X] T032 Fix pagination in `PageTreeService.GetPageChildrenAsync` and `GetDatabaseChildrenAsync` — add `do-while` cursor loops (matching `BlockTreeFetcher` pattern) so pages/databases with >100 children are fully traversed; add regression tests `BuildAsync_PaginatedPageChildren_CollectsAllPages` and `BuildAsync_PaginatedDatabaseChildren_CollectsAllPages` in `tests/Buildout.UnitTests/PageTree/PageTreeServiceTests.cs`
- [X] T033 Fix `TreeToolHandler.cs` — change `TreeRootNotFoundException` mapping from `McpErrorCode.InvalidParams` to `McpErrorCode.ResourceNotFound`; update corresponding assertion in `tests/Buildout.IntegrationTests/Mcp/TreeToolTests.cs`
- [X] T034 Remove dead code from `TreeCommand.cs` — delete the unreachable `catch (TreeDepthOutOfRangeException)` block after `BuildAsync` call (depth is pre-validated before the service is invoked)
- [X] T035 Delete unused `src/Buildout.Core/PageTree/TreeNodeKind.cs` — the enum was planned for logging diagnostics but never referenced
- [X] T036 Fix double trailing newline in CLI JSON output — change `Console.Out.WriteLineAsync(output)` to `Console.Out.WriteAsync(output)` in `TreeCommand.cs` (JSON renderer already appends `\n`)
- [X] T037 Add `<` and `>` escaping to `AsciiTreeRenderer.EscapeName` per spec Edge Cases; add tests `NameWithLessThan_IsEscaped` and `NameWithGreaterThan_IsEscaped` in `tests/Buildout.UnitTests/PageTree/AsciiTreeRendererTests.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately. All T001–T008 can start in parallel after T001 creates directories.
- **Foundational (Phase 2)**: Depends on Phase 1 completion (T009–T012 need T002, T005, T006–T008).
- **User Stories (Phase 3–5)**: All depend on Foundational phase. Stories depend on Phase 2 completion.
  - US1 (Phase 3) can start alone after Phase 2.
  - US2 (Phase 4) depends on Phase 3 completion (JSON renderer extends the renderer pattern; MCP tool reuses the service and keyed dict).
  - US3 (Phase 5) depends on Phase 3 and Phase 4 being complete (extends existing test files).
- **Polish (Phase 6)**: Depends on all story phases complete.

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2 only. Delivers the full ASCII pipeline.
- **US2 (P2)**: Depends on US1 (the keyed `IReadOnlyDictionary<TreeFormat, ITreeRenderer>` must exist; `JsonTreeRenderer` is registered alongside `AsciiTreeRenderer`). Delivers JSON format and MCP surface.
- **US3 (P3)**: Depends on US1 and US2 (extends their test files and verifies all surfaces). Delivers depth control verification.

### Within Each Phase

- Tests MUST be written and FAIL before implementation tasks in the same phase begin
- Types and interfaces before service implementation
- Service before renderers in Phase 3 (renderer tests reference `TreeNode` from Phase 1)
- CLI command after service and renderer are implemented

### Parallel Opportunities

- T003, T004, T006, T007, T008 — all in Phase 1 with no inter-dependency (different files)
- T009 and T010 — both interface files, no dependency on each other
- T013, T014 — renderer and service test files, independent
- T017 and T018 — renderer and settings files, independent
- T021 and T022 — JSON renderer test and MCP integration test, independent
- T026 and T027 — extend different test files

---

## Parallel Example: User Story 1

```bash
# Phase 3 tests — launch together (all write to different files):
Task: T013 PageTreeServiceTests.cs
Task: T014 AsciiTreeRendererTests.cs
Task: T015 TreeCommandTests.cs

# After tests fail as expected:
# Phase 3 implementation — T017 and T018 are independent:
Task: T017 AsciiTreeRenderer.cs
Task: T018 TreeSettings.cs
# Then T016 (service), then T019 (command), then T020 (registration)
```

## Parallel Example: User Story 2

```bash
# Phase 4 tests — launch together:
Task: T021 JsonTreeRendererTests.cs
Task: T022 TreeToolTests.cs (integration test scaffold)

# After tests fail as expected:
Task: T023 JsonTreeRenderer.cs  (unblocked)
Task: T024 TreeToolHandler.cs   (unblocked)
# Then T025 Program.cs registration
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T008)
2. Complete Phase 2: Foundational (T009–T012) — **BLOCKS everything**
3. Write Phase 3 tests T013–T015 and verify they FAIL
4. Complete Phase 3 implementation T016–T020
5. **STOP and VALIDATE**: `buildout-cli tree <known-page>` returns a correct ASCII tree

### Incremental Delivery

1. Phase 1 + 2 → All types and interfaces compile, `TreeDepthTests` pass
2. Phase 3 → `buildout-cli tree <id>` works (ASCII MVP, exit code coverage)
3. Phase 4 → `--format json` works on CLI and `tree` MCP tool works (MCP merge gate)
4. Phase 5 → Depth boundary edge cases verified on all surfaces
5. Phase 6 → Skills documented, validation complete

---

## Notes

- `[P]` tasks operate on different files with no shared state — safe to dispatch in parallel
- `[Story]` label maps each task to its user story for traceability
- Tests must FAIL before implementation starts (Principle IV)
- `TreeDepthOutOfRangeException` must be thrown **before** any network call (T028 validates this)
- The `IReadOnlyDictionary<TreeFormat, ITreeRenderer>` DI pattern mirrors `IDatabaseViewStyle` — look at that registration in `ServiceCollectionExtensions.cs` before writing T012
- The MCP integration test (T022) requires `OPENROUTER_API_KEY` — see CLAUDE.md for local vs. CI guidance
- `docs/configuration.md` must remain unchanged (no new config options in this feature)
