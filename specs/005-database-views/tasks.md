---

description: "Task list for 005-database-views"
---

# Tasks: Database Views (Read-Only)

**Input**: Design documents from `/specs/005-database-views/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: MANDATORY per constitution Principle IV (Test-First Discipline, NON-NEGOTIABLE). Every behavioral task ships with the failing test that justifies it; every change crossing an external boundary (buildin client, CLI dispatcher, MCP transport) ships with an integration test against the existing WireMock buildin fixture. Round-trip tests are not applicable — this feature is read-only and adds no Markdown→block conversion.

**Organization**: Tasks are grouped by user story so each can be implemented and shipped independently after the foundational phase.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe (different files, no in-flight dependency)
- **[Story]**: which user story (US1, US2, US3, US4)
- File paths are absolute-from-repo-root

## Path Conventions

This is the existing five-project .NET solution. Production code under `src/`, tests under `tests/`. New code lands under existing project trees as documented in `plan.md` § Project Structure.

---

## Phase 1: Setup

**Purpose**: Establish a clean baseline before changes.

- [x] T001 Verify the solution builds clean and existing tests pass: `dotnet build` and `dotnet test` from the repository root must both succeed before any task in this feature is started. Record the baseline pass count for later regression comparison.- [x] T002 [P] Create the new namespace directories under `src/Buildout.Core/DatabaseViews/` (`Styles/`, `Properties/`, `Rendering/`) and the matching test directories under `tests/Buildout.UnitTests/DatabaseViews/` (`Styles/`, `Properties/`, `Rendering/`). On .NET SDK-style projects, directories materialize when the first file is added; this task is satisfied when the first foundational task creates the first file in each location.
---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types, DI scaffolding, and test helpers that EVERY user story depends on. No user story may begin until this phase is complete.

**⚠️ CRITICAL**: Tests in this phase are written first and MUST fail before the corresponding implementation task is started.

- [x] T003 [P] Create `DatabaseViewStyle` enum with members `Table, Board, Gallery, List, Calendar, Timeline` at `src/Buildout.Core/DatabaseViews/DatabaseViewStyle.cs`.- [x] T004 [P] Create `CellBudget` record (`MaxCharacters`, `EllipsisMarker`) plus a `Truncate(string)` method implementing the rule in `data-model.md` (truncate at `MaxCharacters`, append marker; trailing whitespace counts) at `src/Buildout.Core/DatabaseViews/Rendering/CellBudget.cs`.- [x] T005 [P] Create `DatabaseViewValidationException` (extends `ArgumentException`) with `OffendingField` and `ValidAlternatives` properties at `src/Buildout.Core/DatabaseViews/DatabaseViewValidationException.cs`.- [x] T006 Create `DatabaseViewRequest` record (`DatabaseId`, `Style`, `GroupByProperty?`, `DateProperty?`) at `src/Buildout.Core/DatabaseViews/DatabaseViewRequest.cs`. Depends on T003.- [x] T007 [P] Create `IPropertyValueFormatter` interface (`string Format(PropertyValue value, CellBudget budget)`) at `src/Buildout.Core/DatabaseViews/Properties/IPropertyValueFormatter.cs`. Depends on T004.- [x] T008 [P] Create `IDatabaseViewStyle` strategy interface (carries the `DatabaseViewStyle Key` and `string Render(Database, IReadOnlyList<DatabaseRow>, DatabaseViewRequest, IPropertyValueFormatter, CellBudget)`) and supporting `DatabaseRow` record at `src/Buildout.Core/DatabaseViews/Styles/IDatabaseViewStyle.cs`. Depends on T003, T004, T006, T007.- [x] T009 [P] Write `CellBudgetTests` — truncation at boundary, no-op when shorter than budget, marker appended exactly once, whitespace counts toward length — at `tests/Buildout.UnitTests/DatabaseViews/Rendering/CellBudgetTests.cs`. Tests must fail (no implementation yet) when run after T004 created a stub but before truncation logic exists.- [x] T010 [P] Write `DatabaseViewRequestValidationTests` — empty database id rejected, unknown style rejected, board without group-by rejected, calendar/timeline without date-property rejected, valid combinations accepted — at `tests/Buildout.UnitTests/DatabaseViews/DatabaseViewRequestValidationTests.cs`. (Validation that depends on the database schema is exercised in T012; this test file covers static validation only.)- [x] T011 [P] Write `PropertyValueFormatterTests` covering all 13 `PropertyValue` subclasses (`TitlePropertyValue`, `RichTextPropertyValue`, `NumberPropertyValue`, `SelectPropertyValue`, `MultiSelectPropertyValue`, `DatePropertyValue`, `FormulaPropertyValue`, `RelationPropertyValue`, `RollupPropertyValue`, `PeoplePropertyValue`, `FilesPropertyValue`, `CheckboxPropertyValue`, `UrlPropertyValue`) per the mapping in `research.md` R4, plus null/empty/truncation edge cases, plus a reflection-driven test that asserts every concrete `PropertyValue` subclass under `Buildout.Core/Buildin/Models/PropertyValue.cs` is enumerated. File: `tests/Buildout.UnitTests/DatabaseViews/Properties/PropertyValueFormatterTests.cs`.- [x] T012 Implement `PropertyValueFormatter` (constructor takes nothing; dispatches on `PropertyValue` subclass) at `src/Buildout.Core/DatabaseViews/Properties/PropertyValueFormatter.cs`. Recursive dispatch for `RollupPropertyValue` and `FormulaPropertyValue` per R4. Depends on T011.- [x] T013 Create `IDatabaseViewRenderer` interface with both `RenderAsync(DatabaseViewRequest, CancellationToken)` and `RenderInlineAsync(string databaseId, CancellationToken)` per `contracts/core-renderer.md` at `src/Buildout.Core/DatabaseViews/IDatabaseViewRenderer.cs`. Depends on T006.- [x] T014 Write `DatabaseViewRendererTests` covering: static validation rejection (rejects before any `IBuildinClient` call); successful single-page query path with mocked style strategy returning a sentinel string; multi-page pagination follow-through (cursor honored, all rows accumulated, no duplicates); transport error from query loop propagated unchanged; cancellation propagated; schema-based group-by/date-property validation against a faked `Database`; dispatch-by-style routes to the correct strategy. Use NSubstitute for `IBuildinClient` and `IDatabaseViewStyle`. File: `tests/Buildout.UnitTests/DatabaseViews/DatabaseViewRendererTests.cs`. Depends on T013.- [x] T015 Implement `DatabaseViewRenderer` (constructor: `IBuildinClient`, `IPropertyValueFormatter`, `IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>`, `CellBudget`). Owns: validation, `GetDatabaseAsync`, paginated `QueryDatabaseAsync` loop, dispatch to strategy, metadata-header prepending. `RenderInlineAsync` delegates to a private `RenderCore` with `Style = Table` and inline-heading flag set. File: `src/Buildout.Core/DatabaseViews/DatabaseViewRenderer.cs`. Depends on T014.- [x] T016 [P] Implement `DatabaseViewMetadataHeader` (pure-function helper that builds either the standalone `# <title> — <style> view (<params>)` line or the inline `## <title>` line based on a flag) at `src/Buildout.Core/DatabaseViews/Rendering/DatabaseViewMetadataHeader.cs`. Tested transitively via per-style golden-file tests in later phases.- [x] T017 Update `AddBuildoutCore` to register `IDatabaseViewRenderer` → `DatabaseViewRenderer`, `IPropertyValueFormatter` → `PropertyValueFormatter`, and `CellBudget` (singleton with the values from R5: 24 chars, `…` marker), plus an empty `IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>` factory that the per-style registration tasks will populate. File: `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`. Depends on T012, T015, T016.- [x] T018 [P] Add `RegisterGetDatabase(WireMockServer, string databaseId, Database body)` and `RegisterQueryDatabase(WireMockServer, string databaseId, params QueryDatabaseResult[] pages)` helpers to the existing `BuildinStubs.cs` (multi-page variant matches the empty-cursor first call, then the previous page's `NextCursor` for each subsequent call; sets last page's `HasMore = false`). File: `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs`. Depends on T003 (for the `Database` and `QueryDatabaseResult` model use).
**Checkpoint**: Foundational phase complete. The renderer compiles, runs in DI, paginates correctly with a mock style, and the WireMock helpers exist. No view styles are implemented yet; user stories begin now.

---

## Phase 3: User Story 1 — Render a Database as a Table View (Priority: P1) 🎯 MVP

**Goal**: A user types `buildout db view <database_id>` in a terminal and sees the database rendered as a markdown pipe-table, with all rows present (paginated to exhaustion), no terminal escape codes when piped, and the same exit codes already used by `get` and `search` for buildin errors.

**Independent Test**: Run `buildout db view <fixture_database_id>` against a WireMock-backed buildin fixture with three rows and three properties. Output starts with `# <title> — table view`, then a markdown pipe-table whose columns are the database's properties (title first) and whose rows match the fixture. Piping the command shows zero ANSI escape codes. A nonexistent id exits 3 with the documented error message.

### Tests for User Story 1 (write first; MUST fail)

- [x] T019 [P] [US1] Write `TableViewStyleTests` with golden-string assertions: a 3-row fixture renders to an exact pipe-table (header line, separator line, three body rows); a wide fixture (>6 columns) switches to the stacked layout per R5; an empty fixture renders the metadata header followed by `(no rows)`; a fixture with only the title property renders a single-column table. File: `tests/Buildout.UnitTests/DatabaseViews/Styles/TableViewStyleTests.cs`.- [x] T020 [P] [US1] Write `DbViewCommandTests` integration test: spins up the WireMock buildin fixture (feature 004), registers `RegisterGetDatabase` + `RegisterQueryDatabase` for a fixture id, invokes the CLI `db view <id>` command end-to-end (no `--style` → table by default), asserts stdout matches a golden file byte-for-byte under non-TTY output, asserts exit code 0; plus three negative cases (404 → exit 3, 401/403 → exit 4, transport → exit 5). File: `tests/Buildout.IntegrationTests/Cli/DbViewCommandTests.cs`.- [x] T021 [P] [US1] Write `DatabaseViewReadOnlyTests` (cross): runs the `db view` command against a WireMock server with **only** `GET /v1/databases/{id}` and `POST /v1/databases/{id}/query` stubs registered. Any other endpoint hit returns 500. Test passes iff no 500 is observed. File: `tests/Buildout.IntegrationTests/Cross/DatabaseViewReadOnlyTests.cs`.
### Implementation for User Story 1

- [x] T022 [US1] Implement `TableViewStyle` (`IDatabaseViewStyle`, `Key = Table`) at `src/Buildout.Core/DatabaseViews/Styles/TableViewStyle.cs`: build a markdown pipe-table from the rows, place title property first, apply `CellBudget.Truncate` to every cell, switch to stacked layout when the row would exceed the threshold from R5, render `(no rows)` when the row list is empty. Depends on T019.- [x] T023 [P] [US1] Create `DbSettings` branch container (no options of its own; required by `AddBranch<T>`) at `src/Buildout.Cli/Commands/DbSettings.cs`.- [x] T024 [P] [US1] Create `DbViewSettings` per `contracts/cli-command.md`: positional `DatabaseId`, `[CommandOption("-s|--style")] DatabaseViewStyle Style` with `[DefaultValue(DatabaseViewStyle.Table)]`, `[CommandOption("-g|--group-by")] string? GroupByProperty`, `[CommandOption("-d|--date-property")] string? DateProperty`. File: `src/Buildout.Cli/Commands/DbViewSettings.cs`.- [x] T025 [US1] Create `DbViewCommand : AsyncCommand<DbViewSettings>` with constructor injection of `IDatabaseViewRenderer`, `IAnsiConsole`, `TerminalCapabilities`, `MarkdownTerminalRenderer`. On execute: build a `DatabaseViewRequest`, call `RenderAsync`, then plain-output via `_console.Write(new Text(rendered))` unless `IsStyledStdout && Style == Table` (then `_terminalRenderer.Render(rendered)`). Map `DatabaseViewValidationException` → exit 2; map `BuildinApiException` to the existing exit-code table (3/4/5/6) per `contracts/cli-command.md`. File: `src/Buildout.Cli/Commands/DbViewCommand.cs`. Depends on T022, T023, T024.- [x] T026 [US1] Wire the `db view` branch in `Program.cs`: add `config.AddBranch<DbSettings>("db", db => db.AddCommand<DbViewCommand>("view"));`. File: `src/Buildout.Cli/Program.cs`. Depends on T025.- [x] T027 [US1] Register `TableViewStyle` as `IDatabaseViewStyle` keyed by `DatabaseViewStyle.Table` in the DI registry created by T017. File: `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`. Depends on T022, T017.
**Checkpoint**: After T027, `dotnet run --project src/Buildout.Cli -- db view <id>` renders the fixture database as a table, all foundational tests are green, and the read-only contract test (T021) holds. **MVP shippable.**

---

## Phase 4: User Story 2 — Choose Among Multiple View Styles (Priority: P2)

**Goal**: The same CLI command, with `--style <board|gallery|list|calendar|timeline>` and the right group-by / date-property argument, renders the database under the chosen style. Output remains plain-text-only when redirected.

**Independent Test**: For each non-table style, `buildout db view <id> --style <style>` against a WireMock fixture suited to that style produces the documented rendering (per `design-sketches.md` and per-style tests). An unknown `--style` exits 2 with a message listing valid styles.

### Tests for User Story 2 (write first; MUST fail)

- [x] T028 [P] [US2] Write `BoardViewStyleTests` with golden assertions: ≤3 non-empty groups → side-by-side ASCII columns; >3 groups → stacked sections with group counts; a row whose group-by value is missing lands under `(none)`; multi-select group key is the comma-joined option names (per R6). File: `tests/Buildout.UnitTests/DatabaseViews/Styles/BoardViewStyleTests.cs`.
- [x] T029 [P] [US2] Write `GalleryViewStyleTests` with golden assertions: each row is a card block, cover image is `[cover: image]` or `[cover: none]` placeholder, at most three secondary properties shown. File: `tests/Buildout.UnitTests/DatabaseViews/Styles/GalleryViewStyleTests.cs`.
- [x] T030 [P] [US2] Write `ListViewStyleTests` with golden assertions: one bulleted line per row with title and parenthesized property summary; degenerate fixture (no non-title props) renders title-only bullets. File: `tests/Buildout.UnitTests/DatabaseViews/Styles/ListViewStyleTests.cs`.
- [x] T031 [P] [US2] Write `CalendarViewStyleTests` with golden assertions: rows grouped under `## YYYY-MM-DD (Day)` headings, ascending order, rows missing the date property under `(undated)` at the end; date property of types `date`, `created_time`, `last_edited_time` all accepted (per R7). File: `tests/Buildout.UnitTests/DatabaseViews/Styles/CalendarViewStyleTests.cs`.
- [x] T032 [P] [US2] Write `TimelineViewStyleTests` with golden assertions: rows grouped by `## YYYY-MM` headings, each entry rendered as `start → end (Nd)` when end set, `start (1d)` when only start, ascending order. File: `tests/Buildout.UnitTests/DatabaseViews/Styles/TimelineViewStyleTests.cs`.
- [x] T033 [P] [US2] Write `DbViewCommandStylesTests` integration: covers `--style board --group-by Status`, `--style gallery`, `--style list`, `--style calendar --date-property Due`, `--style timeline --date-property Phase` end-to-end through the CLI dispatcher; plus `--style board` (no `--group-by`) → exit 2. File: `tests/Buildout.IntegrationTests/Cli/DbViewCommandStylesTests.cs`.

### Implementation for User Story 2

- [x] T034 [P] [US2] Implement `BoardViewStyle` at `src/Buildout.Core/DatabaseViews/Styles/BoardViewStyle.cs`. Group rows by the resolved `GroupByProperty`'s value; preserve first-seen group order; append `(none)` group last; pick side-by-side vs stacked based on R5 cap. Depends on T028.
- [x] T035 [P] [US2] Implement `GalleryViewStyle` at `src/Buildout.Core/DatabaseViews/Styles/GalleryViewStyle.cs`. Card per row, cover placeholder, ≤3 secondary props after the title, in schema order. Depends on T029.
- [x] T036 [P] [US2] Implement `ListViewStyle` at `src/Buildout.Core/DatabaseViews/Styles/ListViewStyle.cs`. One bullet per row, title plus parenthesized property summary. Also wire as the documented fallback for "no non-title properties" branches in other styles. Depends on T030.
- [x] T037 [P] [US2] Implement `CalendarViewStyle` at `src/Buildout.Core/DatabaseViews/Styles/CalendarViewStyle.cs`. Buckets by `start` date (or the timestamp itself for `created_time`/`last_edited_time`); `(undated)` bucket for missing dates; ascending heading order. Depends on T031.
- [x] T038 [P] [US2] Implement `TimelineViewStyle` at `src/Buildout.Core/DatabaseViews/Styles/TimelineViewStyle.cs`. Year-month headings; date-range entries; duration computed in days; single-date entries render as `(1d)`. Depends on T032.
- [x] T039 [US2] Register the five new styles (`Board`, `Gallery`, `List`, `Calendar`, `Timeline`) as `IDatabaseViewStyle` entries in the DI registry at `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`. Single-file edit, depends on T034–T038 having been added; do not parallelize with T027 (same file).- [x] T040 [US2] Extend the schema-based validation in `DatabaseViewRenderer` (already present from T015) so the validation-error message for `--group-by`/`--date-property` lists the valid alternatives derived from the actual `Database.properties` schema. File: `src/Buildout.Core/DatabaseViews/DatabaseViewRenderer.cs`. Depends on T015. (Already correctly implemented — lists `ValidAlternatives` from schema properties.)

**Checkpoint**: All six styles ship through the CLI. Each has its own golden test; the styles tests file (T033) covers the dispatcher and validation paths.

---

## Phase 5: User Story 3 — Same Operation Available Over MCP (Priority: P2)

**Goal**: An MCP client (e.g., an editor agent) sees a `database_view` tool advertised, calls it with the same arguments the CLI accepts, and receives a body byte-identical to the CLI's plain-mode output.

**Independent Test**: Invoke the MCP `database_view` tool against the WireMock fixture with `(database_id, style?, group_by?, date_property?)`. The body equals the CLI plain-mode output for the same arguments (verified by an automated diff). Buildin error classes map to the same MCP error codes already used by `search` and the page resource.

### Tests for User Story 3 (write first; MUST fail)

- [x] T041 [P] [US3] Write `DatabaseViewToolTests` integration: calls the `database_view` MCP tool against the WireMock fixture (covering table + one of each non-table style), asserts the body matches a golden file; covers the four error classes (404 → `ResourceNotFound`, 401/403 → `InternalError`, transport → `InternalError`, generic → `InternalError`); covers `InvalidParams` for unknown style and missing required argument. File: `tests/Buildout.IntegrationTests/Mcp/DatabaseViewToolTests.cs`.- [x] T042 [P] [US3] Write `DatabaseViewParityTests` (cross): for the same `(database_id, style, group_by, date_property)` and the same WireMock fixture, asserts that the MCP tool body and the CLI plain-mode stdout are byte-identical. Runs across all six styles and a multi-page query. File: `tests/Buildout.IntegrationTests/Cross/DatabaseViewParityTests.cs`.
### Implementation for User Story 3

- [x] T043 [P] [US3] Create `DatabaseViewToolHandler` per `contracts/mcp-tool.md`: `[McpServerToolType]` class with one `[McpServerTool(Name = "database_view")]` async method taking `string database_id, string? style = null, string? group_by = null, string? date_property = null, CancellationToken`. Description text declares the operation read-only and that it follows pagination to exhaustion. Maps `DatabaseViewValidationException` → `McpProtocolException(InvalidParams, ...)`; maps `BuildinApiException` per the table in `contracts/mcp-tool.md`. Constructor takes `IDatabaseViewRenderer`. File: `src/Buildout.Mcp/Tools/DatabaseViewToolHandler.cs`.- [x] T044 [US3] Wire `DatabaseViewToolHandler` into the MCP builder chain: append `.WithTools<DatabaseViewToolHandler>()` after the existing `.WithTools<SearchToolHandler>()` in `src/Buildout.Mcp/Program.cs`. Depends on T043.
**Checkpoint**: The MCP surface is at parity with CLI. Together with US1 + US2, the standalone view feature is fully shipped.

---

## Phase 6: User Story 4 — Embedded Databases Render Inline When Reading a Page (Priority: P3)

**Goal**: When `get <page_id>` (CLI) or the MCP `buildin://<page_id>` resource encounters a `child_database` block in the page contents, the page render substitutes a table-style view of the embedded database at the block's position. Pages without embedded databases produce byte-identical output to today.

**Independent Test**: Read a fixture page that contains exactly one `child_database` block via both surfaces. Verify each produces output where the block is replaced by `## <database title>` and the embedded database's pipe-table. Read a fixture page with **zero** `child_database` blocks via both surfaces and verify byte-identical output to the no-feature baseline (golden file pinned from T001's baseline). Read a fixture page where the embedded database returns 404/401/transport — page renders successfully with the documented placeholder line at that position.

### Tests for User Story 4 (write first; MUST fail)

- [x] T045 [P] [US4] Write `ChildDatabaseConverterTests` (unit): success path returns `## <title>\n\n` followed by the table rendering returned by a mocked `IDatabaseViewRenderer.RenderInlineAsync`; per-error placeholder cases per the table in `data-model.md` (404 → `[child database: not found — <Title>]`, 401/403 → `[child database: access denied — <Title>]`, transport → `[child database: transport error — <Title>]`, generic → `[child database: not accessible — <Title>]`, malformed (no `DatabaseId`) → `[child database: malformed]`); `<Title>` falls back to `(unknown)` when the block carries no title; `OperationCanceledException` is propagated, not caught. File: `tests/Buildout.UnitTests/Markdown/Blocks/ChildDatabaseConverterTests.cs`.
- [x] T046 [P] [US4] Write `GetCommandChildDatabaseTests` (CLI integration): a fixture page contains one `child_database` block; `RegisterGetPage` returns the page with the block, `RegisterGetDatabase` + `RegisterQueryDatabase` return the embedded data. Run `buildout get <page_id>`, assert stdout matches a golden file byte-for-byte under non-TTY output. File: `tests/Buildout.IntegrationTests/Cli/GetCommandChildDatabaseTests.cs`.
- [x] T047 [P] [US4] Write `PageResourceChildDatabaseTests` (MCP integration): same fixture as T046 but invoked through the MCP `buildin://<page_id>` resource. Assert the body matches the same golden file as T046 byte-for-byte. File: `tests/Buildout.IntegrationTests/Mcp/PageResourceChildDatabaseTests.cs`.
- [x] T048 [P] [US4] Write `ChildDatabasePlaceholderTests` (cross): a fixture page contains two `child_database` blocks; one resolves successfully, the other returns 404 / 401 / transport / generic / is malformed. The page render does NOT abort; the failing block is replaced by the documented placeholder; the surrounding content matches a golden fixture; the overall command still exits 0 (CLI) and returns a successful resource result (MCP). One sub-test per error class, parameterized. File: `tests/Buildout.IntegrationTests/Cross/ChildDatabasePlaceholderTests.cs`.
- [x] T049 [P] [US4] Write `GetCommandNoRegressionTests` (CLI integration): a fixture page with NO `child_database` blocks renders byte-identically to the baseline output captured in T001 from a stable golden file already pinned for feature 002. File: `tests/Buildout.IntegrationTests/Cli/GetCommandNoRegressionTests.cs`.

### Implementation for User Story 4

- [x] T050 [P] [US4] Add `ChildDatabaseBlock` sealed record to the existing `Block` discriminated hierarchy at `src/Buildout.Core/Buildin/Models/Block.cs` per the shape in `data-model.md`: `(Id, CreatedTime, LastEditedTime, Archived, DatabaseId, string? Title)` with discriminator `"child_database"`. Verify the JSON deserializer dispatches on the `Type` field and add a deserialization unit test asserting a buildin payload with `"type": "child_database"` lands in the new record (file `tests/Buildout.UnitTests/Buildin/Models/BlockDeserializationTests.cs` if it exists, otherwise a new test file alongside any existing block-deserialization test).- [x] T051 [US4] Verify `IDatabaseViewRenderer.RenderInlineAsync` (declared by T013, implemented by T015) emits the inline `## <title>` heading rather than the standalone metadata header, and renders the table style with no group-by / date-property. If T015's implementation does not already cover this, finish it now in `src/Buildout.Core/DatabaseViews/DatabaseViewRenderer.cs`. (No new file; this task is here to gate US4 explicitly.) Depends on T015.
- [x] T052 [US4] Implement `ChildDatabaseConverter : IBlockToMarkdownConverter` at `src/Buildout.Core/Markdown/Conversion/Blocks/ChildDatabaseConverter.cs`. Constructor takes `IDatabaseViewRenderer`. On a `ChildDatabaseBlock`: if `DatabaseId` is null/empty emit malformed placeholder; else call `RenderInlineAsync(DatabaseId, ct)`, catch `BuildinApiException` and `DatabaseViewValidationException` and emit the documented placeholder per error class; let `OperationCanceledException` propagate. Use `block.Title ?? "(unknown)"` in placeholders. Depends on T045, T050, T051.
- [x] T053 [US4] Register `ChildDatabaseConverter` in `AddBuildoutCore` alongside the existing `IBlockToMarkdownConverter` registrations. File: `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`. Depends on T052.- [x] T054 [US4] Verify that the existing block-to-markdown dispatch in `PageMarkdownRenderer` (or its registry) routes `ChildDatabaseBlock` to `ChildDatabaseConverter`. If dispatch is type-keyed via the `BlockToMarkdownRegistry` (per the existing code), no further change is needed and this task is satisfied by re-running T046+T047+T048 and observing them pass. If dispatch is via discriminator-string lookup, add the `"child_database"` mapping. File (if changes needed): `src/Buildout.Core/Markdown/BlockToMarkdownRegistry.cs` or its equivalent.

**Checkpoint**: Page reads (CLI and MCP) now expand `child_database` blocks inline; pages without embedded databases regress nothing; failing embedded databases never abort the page render.

---

## Phase 7: Polish & Cross-Cutting

**Purpose**: Final correctness sweeps; no new behavior.

- [x] T055 [P] Run the `quickstart.md` examples end-to-end against the WireMock fixture (or a local sandbox) and confirm each produces the documented output. Where a command produces output that differs from `quickstart.md`'s description, update `quickstart.md` (not the code) to match the actual behavior — quickstart documents reality, not aspiration.- [x] T056 [P] Confirm `buildout db view --help` lists supported styles, the required-vs-optional matrix for `--group-by` / `--date-property`, and one example per style per `contracts/cli-command.md`. If Spectre.Console.Cli's auto-generated help omits any of these, supplement with `[Description]` attributes on `DbViewSettings` properties or via the command's `Description` override. File: `src/Buildout.Cli/Commands/DbViewSettings.cs` and/or `src/Buildout.Cli/Commands/DbViewCommand.cs`.- [x] T057 [P] Re-run the entire `dotnet test` suite from a clean checkout of the branch and confirm: (a) baseline test count from T001 has grown by exactly the count of new tests (no silent skips), (b) zero tests are marked `[Fact(Skip = …)]` or `[Theory(Skip = …)]`, (c) the `DatabaseViewReadOnlyTests` and `ChildDatabasePlaceholderTests` are green.- [ ] T058 [P] Manual sanity: pipe `buildout db view <id> --style table` to a UTF-8 file and verify with `cat -v` that no escape sequences appear; pipe to `diff` against the equivalent MCP-tool output captured to a file and confirm no differences.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no upstream dependency.
- **Phase 2 (Foundational)**: depends on Phase 1; **blocks every user story**.
- **Phase 3 (US1)**: depends on Phase 2.
- **Phase 4 (US2)**: depends on Phase 2 (NOT on Phase 3 for compilation; US1 and US2 share the renderer/formatter but ship independent style strategies).
- **Phase 5 (US3)**: depends on Phase 2 + Phase 3 minimally (MCP needs the renderer + at least one style to make the parity test meaningful) — in practice, after Phase 4 lands so the parity test (T042) runs across all six styles.
- **Phase 6 (US4)**: depends on Phase 2 (renderer + `RenderInlineAsync`) and on the existing page-read pipeline from feature 002. Independent of Phases 4–5.
- **Phase 7 (Polish)**: depends on every desired user-story phase being complete.

### Within Each Phase

- Setup → Foundational types → Foundational tests → Foundational implementations → DI scaffold + WireMock helpers.
- Inside each user-story phase: tests written first, then per-style implementation, then DI registration, then any wiring (CLI Program.cs / MCP Program.cs).

### Parallel Opportunities

- Phase 2: T003, T004, T005 can run in parallel; T009, T010, T011 (test files) in parallel; T007/T008 in parallel; T018 in parallel with everything else (test-only file).
- Phase 3: T019, T020, T021 in parallel; T023, T024 in parallel; T022 sequential after T019; T025 sequential after T022/T023/T024.
- Phase 4: T028–T033 (six test files) in parallel; T034–T038 (five style files) in parallel; T039, T040 sequential after styles land.
- Phase 5: T041, T042 in parallel; T043 sequential; T044 final.
- Phase 6: T045–T049 (five test files) in parallel; T050 in parallel; T052 sequential after T050+T051; T053, T054 sequential after T052.
- Phase 7: all four polish tasks in parallel.

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (different files, all expected to fail until impl):
Task: "T019 Write TableViewStyleTests at tests/Buildout.UnitTests/DatabaseViews/Styles/TableViewStyleTests.cs"
Task: "T020 Write DbViewCommandTests at tests/Buildout.IntegrationTests/Cli/DbViewCommandTests.cs"
Task: "T021 Write DatabaseViewReadOnlyTests at tests/Buildout.IntegrationTests/Cross/DatabaseViewReadOnlyTests.cs"

# Then in parallel — settings shells (no logic dependency on TableViewStyle):
Task: "T023 Create DbSettings at src/Buildout.Cli/Commands/DbSettings.cs"
Task: "T024 Create DbViewSettings at src/Buildout.Cli/Commands/DbViewSettings.cs"

# Then sequentially:
Task: "T022 Implement TableViewStyle (turns T019 GREEN)"
Task: "T025 Implement DbViewCommand (turns T020 GREEN)"
Task: "T026 Wire branch in Program.cs"
Task: "T027 Register TableViewStyle in DI"
```

---

## Parallel Example: User Story 2

```bash
# Six golden-file test files in parallel (one per style; T028–T032 plus the dispatcher test T033):
Task: "T028 BoardViewStyleTests"
Task: "T029 GalleryViewStyleTests"
Task: "T030 ListViewStyleTests"
Task: "T031 CalendarViewStyleTests"
Task: "T032 TimelineViewStyleTests"
Task: "T033 DbViewCommandStylesTests"

# Five style implementations in parallel (T034–T038); each turns its sibling test GREEN:
Task: "T034 BoardViewStyle"
Task: "T035 GalleryViewStyle"
Task: "T036 ListViewStyle"
Task: "T037 CalendarViewStyle"
Task: "T038 TimelineViewStyle"

# Then DI registration + validation polish (single file each):
Task: "T039 Register the five styles in DI"
Task: "T040 Extend schema-validation messages"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup (T001–T002).
2. Phase 2: Foundational (T003–T018) — renderer scaffold + property formatter + WireMock helpers, all tests green.
3. Phase 3: US1 (T019–T027) — table style + `db view` CLI command.
4. **Validate**: `dotnet test` green, manual `buildout db view <id>` against WireMock-backed sandbox returns expected output, output piped contains no escape codes.
5. **Ship.** Standalone CLI table view is the MVP increment.

### Incremental Delivery

- After MVP: ship Phase 4 (US2 — full style palette).
- Then Phase 5 (US3 — MCP parity), with the parity test running across all six styles.
- Then Phase 6 (US4 — inline expansion in page reads).
- Phase 7 (polish) before the final merge.

Each increment is independently mergeable: US1 ships table-only; US2 adds five styles without touching MCP or page-read; US3 adds the MCP surface without touching CLI behavior; US4 adds page-read integration without touching the standalone surfaces.

### Parallel Team Strategy

Foundational phase is small enough that one developer can do it in a single session. Once Phase 2 is in, US2 (six golden tests + five styles) and US3 (two tests + tool handler) can be assigned to two developers in parallel. US4 is best done after US2 lands since its placeholder fixtures rely on the same WireMock shapes; it can be picked up by the developer that finished US3.

---

## Notes

- Constitution Principle IV is in force: every test file in this list is written before its corresponding implementation file, and must fail before the implementation lands.
- The renderer is the single source of truth for all six styles and the inline path. Neither CLI nor MCP nor `ChildDatabaseConverter` re-implements rendering (Constitution Principle I).
- The read-only invariant is enforced by T021 and T048 — any future task that introduces a new buildin endpoint to the rendering code path will need to update those tests, with explicit justification in the spec.
- Commit after each task or after each logical group (e.g., after all Phase 2 foundational types land; after each style; after each phase).
- No task in this list should require a real buildin.ai network call.
