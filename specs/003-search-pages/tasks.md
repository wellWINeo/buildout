---

description: "Task list for Page Search"
---

# Tasks: Page Search

**Input**: Design documents from `/specs/003-search-pages/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioural change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written and observed to FAIL before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing. Both US1 (CLI search) and US2 (MCP search tool) are P1 — they share the same foundation (`ISearchService` + `ISearchResultFormatter`) and can be delivered in parallel after the Foundational phase. US3 (P2 — scoped search via `--page` / `page_id`) is a property of the unified search behaviour; the underlying `AncestorScopeFilter` lives in the foundation, and US3's tasks add end-to-end scoping integration tests across both surfaces.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source projects**: `src/Buildout.Core/`, `src/Buildout.Mcp/`, `src/Buildout.Cli/`
- **Test projects**: `tests/Buildout.UnitTests/`, `tests/Buildout.IntegrationTests/`
- **Solution**: `buildout.slnx` at repo root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify no new package dependencies are needed. Search reuses `Spectre.Console` (already referenced via `Spectre.Console.Cli`), `ModelContextProtocol` (already wired in `Buildout.Mcp`), and `Anthropic.SDK` (already in `Buildout.IntegrationTests` from feature 002).

- [X] T001 Confirm no new package references are required by running `dotnet restore buildout.slnx` from repo root and checking that the existing test suite still runs green via `dotnet test buildout.slnx`. No file changes; this is a guardrail.

**Checkpoint**: Solution restores cleanly; existing 185+ tests still pass.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Close the one latent feature-001 scaffold gap (`MapV1SearchResponse` dropping `Title` and `Parent`), then build the search service + formatter that both user stories depend on. Both US1 and US2 consume `ISearchService` + `ISearchResultFormatter` exclusively — neither has search logic of its own.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### A. Page model + BotBuildinClient mapping fix (TDD)

- [X] T002 Add failing test methods to `tests/Buildout.UnitTests/Buildin/BotBuildinClientTests.cs` covering: (a) `SearchPagesAsync` populates `Page.Title` from `V1SearchPageResult.Properties.Title.Title` (a list of `RichTextItem`), routed through the existing `MapRichText` helper; (b) `SearchPagesAsync` populates `Page.Parent` from `V1SearchPageResult.Parent` (composed-type wrapper), routed through the existing `MapParent` helper; (c) `SearchPagesAsync` populates the new `Page.ObjectType` from `V1SearchPageResult.Object` (verbatim string `"page"` / `"database"`); (d) when `Properties.Title` is null on the wire, `Page.Title` is null; when title list is empty, `Page.Title` is empty; (e) when `Parent` is `ParentPageId`, `Page.Parent` is `ParentPage`; when `ParentDatabaseId`, `ParentDatabase`; etc. Existing `Id`/`CreatedAt`/`LastEditedAt`/`Archived` mapping behaviour MUST stay green; existing `MapPage` callers (e.g. tests covering `GetPageAsync`) MUST continue to see `Page.ObjectType == null` (no behavioural change).
- [X] T003 Apply two coordinated changes:
  - Add an additive `ObjectType : string?` property to the existing `Page` record in `src/Buildout.Core/Buildin/Models/Page.cs` per `data-model.md` § Page (existing callers ignore the new field — source-compatible);
  - Apply the mapping fix in `src/Buildout.Core/Buildin/BotBuildinClient.cs` `MapV1SearchResponse`: extend the per-page mapping to set `Title` (sibling to `MapPage.ExtractTitle`, taking `Gen.V1SearchPageResult_properties` as input), `Parent` (via the existing `MapParent` helper), and `ObjectType` (verbatim from `V1SearchPageResult.Object`).
  - Verify T002 tests now pass.

### B. Search domain types

- [X] T004 [P] Create `SearchObjectType` enum (`Page`, `Database`) and `SearchMatch` record (`PageId : string`, `ObjectType : SearchObjectType`, `DisplayTitle : string`, `Parent : Parent?`, `Archived : bool` — all per `data-model.md` § SearchMatch) in `src/Buildout.Core/Search/SearchMatch.cs`

### C. Title renderer (TDD pair)

- [X] T005 [P] Write failing unit tests for `TitleRenderer` covering: null input → `"(untitled)"`; empty list → `"(untitled)"`; list of one `RichText { Type = "text", Content = "Hello" }` → `"Hello"`; multi-segment list concatenates `Content` in order; mention-typed `RichText` uses `Content` (no Markdown markup); tab characters in any `Content` are replaced with single space in output; whitespace-only resulting string still surfaces as `"(untitled)"` in `tests/Buildout.UnitTests/Search/TitleRendererTests.cs`
- [X] T006 Create `ITitleRenderer` interface (`string RenderPlain(IReadOnlyList<RichText>? title)`) and `TitleRenderer` implementation in `src/Buildout.Core/Search/Internal/TitleRenderer.cs` per `data-model.md` § TitleRenderer — verify T005 passes.

### D. Ancestor scope filter (TDD pair)

- [X] T007 [P] Write failing unit tests for `AncestorScopeFilter.IsInScopeAsync` covering: (a) match.PageId == scopePageId → true; (b) match's `Parent` is `ParentPage(scopePageId)` → true; (c) two-hop ancestor via seeded `parentLookup` → true; (d) multi-hop where one ancestor is missing from `parentLookup` → fetched on-demand via `IBuildinClient.GetPageAsync` and added to `parentLookup`; (e) chain terminates on `ParentWorkspace` → false; (f) chain terminates on `ParentDatabase` → false; (g) chain terminates on `null` parent → false; (h) `GetPageAsync` raises 404 → match is out of scope (false), no exception bubbles; (i) `GetPageAsync` raises 403 → out of scope, no exception bubbles; (j) `GetPageAsync` raises transport error → exception bubbles unchanged; (k) cycle in `Parent` chain → false + debug log, no infinite loop, in `tests/Buildout.UnitTests/Search/AncestorScopeFilterTests.cs`. Mock `IBuildinClient` via NSubstitute.
- [X] T008 Implement `AncestorScopeFilter` (constructor takes `IBuildinClient` + `ILogger<AncestorScopeFilter>`; method `IsInScopeAsync(SearchMatch match, string scopePageId, Dictionary<string, Parent?> parentLookup, CancellationToken ct)` walks the parent chain per `data-model.md` § AncestorScopeFilter and `research.md` R3) in `src/Buildout.Core/Search/Internal/AncestorScopeFilter.cs` — verify T007 passes.

### E. Search result formatter (TDD pair)

- [X] T009 [P] Write failing unit tests for `SearchResultFormatter` covering: empty list → `""` (zero-byte); single `SearchMatch` → exactly one line `<page_id>\t<object_type>\t<title>\n`; three matches preserve input order in three lines; `ObjectType.Page` → `"page"`; `ObjectType.Database` → `"database"`; `DisplayTitle == "(untitled)"` (already produced by `TitleRenderer`) renders the placeholder verbatim; output contains zero `\r` characters; called twice with the same input → byte-identical output (determinism), per `contracts/search-result-format.md`, in `tests/Buildout.UnitTests/Search/SearchResultFormatterTests.cs`
- [X] T010 Create `ISearchResultFormatter` interface (`string Format(IReadOnlyList<SearchMatch> matches)`) and `SearchResultFormatter` implementation (zero deps, zero state, deterministic) in `src/Buildout.Core/Search/SearchResultFormatter.cs` and `src/Buildout.Core/Search/ISearchResultFormatter.cs` — verify T009 passes.

### F. Search service public surface

- [X] T011 [P] Create `ISearchService` interface (`Task<IReadOnlyList<SearchMatch>> SearchAsync(string query, string? pageId, CancellationToken cancellationToken = default)`) per `contracts/search-service.md` in `src/Buildout.Core/Search/ISearchService.cs`

### G. Search service orchestrator (TDD pair)

- [X] T012 Write failing unit tests for `SearchService` in `tests/Buildout.UnitTests/Search/SearchServiceTests.cs` covering: (a) empty `query` throws `ArgumentException` and substituted `IBuildinClient` records zero interactions (SC-006 invariant); (b) whitespace-only `query` same behaviour; (c) single-page response returns `SearchMatch` list mapped from each `Page` (PageId via `Guid.ToString("D")`; `ObjectType` mapped from `Page.ObjectType` string — `"page"` → `SearchObjectType.Page`, `"database"` → `SearchObjectType.Database`, null/unrecognised → defensive default `Page`); (d) multi-page response with `HasMore = true` chained by `NextCursor` is fully drained; (e) archived pages excluded from output by default (FR-007); (f) `pageId == null` → no filter applied, every non-archived match returned in arrival order; (g) `pageId` provided + matches A/B/C where B and C have `Parent = ParentPage(A_id)` and an unrelated D → returns A/B/C only, in original order; (h) `pageId` provided where ancestor walk's `GetPageAsync` raises 404 for the scope page itself → no matches survive (filter rejects all); (i) determinism: two consecutive calls with the same mock fixture return equal lists; (j) `CancellationToken` is propagated to every `SearchPagesAsync` and `GetPageAsync` call.
- [X] T013 Implement `SearchService` in `src/Buildout.Core/Search/SearchService.cs` (constructor takes `IBuildinClient`, `ITitleRenderer`, `AncestorScopeFilter`, `ILogger<SearchService>`; validate non-empty query → throw `ArgumentException`; pagination loop draining `NextCursor`; map each `Page` to `SearchMatch` using `TitleRenderer.RenderPlain(page.Title)` for the display title and `page.ObjectType` for the object-type translation; archived filter; if `pageId` non-null, build seeded `parentLookup` and call `AncestorScopeFilter.IsInScopeAsync` per match) — verify T012 passes.

### H. DI registration + smoke test

- [X] T014 Extend `ServiceCollectionExtensions.AddBuildoutCore(...)` in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` to register: `ITitleRenderer` → `TitleRenderer` (singleton); `AncestorScopeFilter` (singleton, no interface); `ISearchService` → `SearchService` (singleton); `ISearchResultFormatter` → `SearchResultFormatter` (singleton). Existing markdown registrations stay unchanged.
- [X] T015 Add unit test in `tests/Buildout.UnitTests/Search/DependencyInjectionTests.cs` confirming the service provider built from `AddBuildinClient(...)` + `AddBuildoutCore()` resolves both `ISearchService` and `ISearchResultFormatter` end-to-end — verify T014's registrations are correct.

**Checkpoint**: `dotnet test buildout.slnx` is green. `ISearchService` + `ISearchResultFormatter` are fully implemented end-to-end with comprehensive unit coverage. Both user stories can now begin in parallel.

---

## Phase 3: User Story 1 — CLI: search across all accessible pages (Priority: P1) 🎯 MVP

**Goal**: A developer runs `buildout search "<query>"` and sees a ranked list of pages whose content matches — styled when stdout is a TTY, plain TSV when piped or redirected. Empty queries are rejected at the CLI boundary with exit 2 before any buildin call. Failure modes map to distinct exit codes (3/4/5/6) consistent with `buildout get`.

**Independent Test**: With a fake `IBuildinClient` returning a fixture page list, run `buildout search "query"` (a) into a real TTY → observe a `Spectre.Console.Table` with three columns; (b) piped to a file → observe TSV bytes that match `ISearchResultFormatter.Format(...)` exactly. Same fake → exit 0 for happy path; exit 2 for empty query; exit 4 for auth failure; exit 5 for transport.

### Tests for User Story 1

> **NOTE: Write these tests FIRST; they MUST compile and FAIL before the corresponding implementation lands.**

- [X] T016 [P] [US1] Write failing unit tests for `SearchResultStyledRenderer` in `tests/Buildout.UnitTests/Cli/SearchResultStyledRendererTests.cs` covering: parses a multi-line TSV body (3 columns each) and emits a `Spectre.Console.Table` with three columns named `ID`, `Type`, `Title`; each match's three columns appear in their respective table columns in input order; empty body → emits a single styled `[dim]No matches.[/]` line, no table; mismatched column count on any line → throws `InvalidOperationException` (defensive — should never happen given the formatter's invariants); output goes to an injected `IAnsiConsole` (use `TestConsole` to capture).
- [X] T017 [P] [US1] Write failing integration tests for `SearchCommand` in `tests/Buildout.IntegrationTests/Cli/SearchCommandTests.cs` covering: (a) happy path non-TTY → exit 0, stdout equals `ISearchResultFormatter.Format(matches)` byte-for-byte for the same fixture; (b) happy path TTY → exit 0, stdout contains an ANSI escape AND each fixture title's text appears in the output; (c) zero-match non-TTY → stdout is `""` (empty); (d) zero-match TTY → stdout contains `No matches.`; (e) plain-mode stdout contains zero `\x1b` bytes; (f) empty positional `<query>` → exit 2 with stderr containing "Query must be non-empty." AND substituted `IBuildinClient` records zero `SearchPagesAsync` calls; (g) whitespace-only `"   "` query → exit 2 same shape; (h) auth failure (401 or 403) from `SearchPagesAsync` → exit 4; (i) transport failure (`TransportError`) → exit 5; (j) unexpected (`UnknownError`) → exit 6; (k) `--page <id>` populated and `GetPageAsync` for that id raises 404 (during ancestor walk) → exit 3; (l) `--page` is passed through to `ISearchService.SearchAsync` (verify via mock); (m) follow the existing `GetCommandTests` pattern for `TypeRegistrar` + DI service collection construction.

### Implementation for User Story 1

- [X] T018 [P] [US1] Implement `SearchResultStyledRenderer` in `src/Buildout.Cli/Rendering/SearchResultStyledRenderer.cs` per `contracts/search-result-format.md` § Styled mode and `research.md` R5: parses the formatter body line-by-line, splits on `\t`, builds a `Spectre.Console.Table` with `ID`/`Type`/`Title` columns, writes to the injected `IAnsiConsole`. Empty body → `[dim]No matches.[/]` line. Verify T016 passes.
- [X] T019 [US1] Implement `SearchCommand` in `src/Buildout.Cli/Commands/SearchCommand.cs` per `contracts/cli-search-command.md`: `AsyncCommand<Settings>`; `Settings` carries `Query` (positional) and `PageId` (`--page` option); validates non-empty query → exit 2 with stderr-routed error; calls `ISearchService.SearchAsync` then `ISearchResultFormatter.Format`; branches on `TerminalCapabilities.IsStyledStdout` between styled-renderer and `Console.Out.WriteAsync`; exception switch maps `BuildinApiException` to exit codes 3/4/5/6 as documented. Constructor injects `ISearchService`, `ISearchResultFormatter`, `IAnsiConsole`, `TerminalCapabilities`, `SearchResultStyledRenderer`.
- [X] T020 [US1] Update `src/Buildout.Cli/Program.cs` to register `SearchResultStyledRenderer` as a singleton and add `config.AddCommand<SearchCommand>("search").WithDescription("Search buildin pages by query.")` inside the existing `app.Configure(...)` block alongside the existing `get` command. Verify all T016 + T017 tests now pass.

**Checkpoint**: `buildout search <query> [--page <id>]` is fully functional. With a real bot token, the CLI demo in `quickstart.md` § "CLI demo" works end-to-end. US1 is independently shippable.

---

## Phase 4: User Story 2 — MCP: expose search as an MCP tool (Priority: P1)

**Goal**: An LLM connected to the buildout MCP server discovers a `search` tool, invokes it with a query (and optionally a `page_id`), and receives a tool-result text body whose bytes match the CLI's plain-mode output exactly. Failures surface as MCP-protocol errors with failure-class messages, never as 200 with an error blob.

**Independent Test**: Start the MCP server in-process against a fake `IBuildinClient` returning a fixture match list. From a test client, list tools → see `search` advertised with the documented schema; invoke `search({ query: "foo" })` → receive a single text content block whose body matches `ISearchResultFormatter.Format(...)` byte-for-byte. Empty query → MCP `InvalidParams` error; substituted client recorded zero `SearchPagesAsync` calls.

### Tests for User Story 2

> **NOTE: Write these tests FIRST; they MUST FAIL before the corresponding implementation lands.**

- [X] T021 [P] [US2] Write failing integration tests for `SearchToolHandler` in `tests/Buildout.IntegrationTests/Mcp/SearchToolTests.cs` covering: (a) `ListToolsAsync` returns one tool whose Name is `search`, Description matches `contracts/mcp-search-tool.md`, input schema declares `query` (required string) and `page_id` (optional string); (b) successful invocation returns one text content block whose body equals `ISearchResultFormatter.Format(matches)` for the same fixture; (c) zero-match invocation returns one text content block with body `""`; (d) empty `query` → handler throws `McpProtocolException` with `McpErrorCode.InvalidParams` AND substituted `IBuildinClient` records zero `SearchPagesAsync` calls; (e) `page_id` whose `GetPageAsync` returns 404 → `McpProtocolException(ResourceNotFound)`; (f) auth failure → `McpProtocolException(InternalError)` with auth message; (g) transport failure → `McpProtocolException(InternalError)` with transport message; (h) multi-page `SearchPagesAsync` chained by `NextCursor` is fully drained; (i) two consecutive invocations against same fixture return byte-identical bodies (determinism); (j) `ToolResultBody_EqualsCliPlainBody` — drive both the CLI command (via `CommandApp.RunAsync(["search", "<q>"])` capturing stdout) and the MCP tool (via in-process MCP client) against the same fake `IBuildinClient` fixture and assert the bodies are byte-identical (the SC-003 assertion). Use the same in-process MCP harness as `PageResourceTests` from feature 002.
- [X] T022 [P] [US2] Add a failing cheap-LLM integration test `LlmCanFindAndReadPage` to `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs` per `research.md` R10: skips if `ANTHROPIC_API_KEY` is unset; constructs an in-process MCP client + the buildout server with a fake `IBuildinClient` whose `SearchPagesAsync(query="quarterly revenue")` returns two matches with known IDs ("Q3 Revenue Report" and "Marketing Plan") and whose `GetPageAsync(<q3_id>)` + `GetBlockChildrenAsync(<q3_id>, …)` returns the same fixture content used by the existing `LlmCanAnswerQuestionsAboutRenderedPage` test; sends ONE prompt to Claude Haiku 4.5 listing the available tools and resources, asking "Which page describes Q3 revenue, and what was the total?"; asserts (1) the LLM invoked `search` with a non-empty query, (2) the LLM read `buildin://<q3_id>` after, (3) the answer contains `4.2`. Cost target: well under one cent per run.

### Implementation for User Story 2

- [X] T023 [P] [US2] Implement `SearchToolHandler` in `src/Buildout.Mcp/Tools/SearchToolHandler.cs` per `contracts/mcp-search-tool.md`: `[McpServerToolType]`-decorated class; one `[McpServerTool(Name = "search")]` method `SearchAsync(string query, string? page_id = null, CancellationToken ct = default) → Task<string>` returning `ISearchResultFormatter.Format(matches)`; pre-call validation rejects empty query with `McpProtocolException(InvalidParams)` AND records zero buildin calls; exception switch maps `BuildinApiException` to `McpProtocolException` per the table in the contract; constructor injects `ISearchService`, `ISearchResultFormatter`, `ILogger<SearchToolHandler>`.
- [X] T024 [US2] Update `src/Buildout.Mcp/Program.cs` to add `.WithTools<SearchToolHandler>()` on the `AddMcpServer()` chain alongside the existing `.WithResources<PageResourceHandler>()` from feature 002. Verify all T021 + T022 (if `ANTHROPIC_API_KEY` set) tests now pass.

**Checkpoint**: The MCP server exposes the `search` tool. An LLM can chain `search` → `buildin://<page_id>` end-to-end against a mocked workspace. US2 is independently shippable.

---

## Phase 5: User Story 3 — Scoped search via `--page` / `page_id` (Priority: P2)

**Goal**: End-to-end validation that scoping behaves correctly through both surfaces. The underlying `AncestorScopeFilter` is fully unit-tested in foundation (T007/T008), and `SearchService` has unit-level scoped-vs-unscoped coverage (T012); US3's tasks add surface-level integration tests that drive scoping through the CLI and MCP boundaries to catch any regressions in the wiring.

**Independent Test**: With a fixture workspace where pages B and C have `Parent = ParentPage(A_id)` and an unrelated page D matches the same query, `buildout search "<q>" --page <A_id>` returns A/B/C only (excluding D). MCP `search({ query, page_id: A_id })` returns the same body. `buildout search "<q>" --page <missing_id>` returns exit 3.

### Tests for User Story 3

> **NOTE: Write these tests FIRST; they MUST FAIL before the corresponding implementation lands.**

- [X] T025 [P] [US3] Add scoped-search integration tests to `tests/Buildout.IntegrationTests/Cli/SearchCommandTests.cs` (extending the file from T017) covering: (a) given a fixture where `SearchPagesAsync(q)` returns four matches A/B/C/D where B.Parent=ParentPage(A.Id), C.Parent=ParentPage(B.Id) (transitive), and D is unrelated, then `buildout search q --page <A.Id>` exits 0 with stdout containing exactly three lines for A/B/C in that order, no D; (b) given the same fixture, `buildout search q --page <unrelated>` (where `unrelated` is a page ID with no descendants matching) returns exit 0 with empty stdout in plain mode; (c) `buildout search q --page <missing>` where `GetPageAsync(<missing>)` raises 404 during the ancestor walk → exit 3 with stderr containing `Page not found:`; (d) `buildout search q --page <A.Id>` produces stdout that is a strict subset of `buildout search q` (unscoped) for the same fixture — i.e. every line in the scoped output also appears in the unscoped output, in the same relative order.
- [X] T026 [P] [US3] Add scoped-search integration tests to `tests/Buildout.IntegrationTests/Mcp/SearchToolTests.cs` (extending the file from T021) covering: (a) `search({ query: q, page_id: <A.Id> })` against the same fixture used in T025 returns a tool-result body containing exactly the three lines for A/B/C; (b) `search({ query: q, page_id: <unrelated> })` returns body `""`; (c) `search({ query: q, page_id: <missing> })` raises `McpProtocolException(ResourceNotFound)`; (d) `ScopedToolResultBody_EqualsCliPlainBody` — the byte-equality assertion for the *scoped* path: drive both surfaces with the same `(query, page_id)` pair against the same fixture and assert byte-identical bodies.

### Implementation for User Story 3

- [X] T027 [US3] Verify all T025 + T026 tests pass against the existing implementation built in Phases 2–4. Scoping is implemented at the foundation layer (`AncestorScopeFilter` from T008, `SearchService` from T013) and surface boundaries (`SearchCommand`'s `--page` option from T019, `SearchToolHandler`'s `page_id` parameter from T023) — if any T025/T026 test fails, the regression lives in one of those four artefacts. Do **not** introduce new code paths exclusively for the scoped behaviour; fix at the lowest layer that makes the failing test pass. Document any fix in a brief commit message line referencing the failing test.

**Checkpoint**: Scoped search behaves identically through CLI and MCP. US3 is independently demonstrable: pick a known buildin parent page in the workspace and verify `buildout search "<query>" --page <id>` strictly reduces the unscoped result set.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation against the spec's success criteria and the quickstart "Definition of Demonstrable" checklist.

- [X] T028 Run `dotnet test buildout.slnx` with `ANTHROPIC_API_KEY` unset; confirm full feature suite (features 001 + 002 + 003) is green and completes well under 30 s on a developer laptop with no buildin network access (SC-007). Run again with `ANTHROPIC_API_KEY` set; confirm still under 30 s including both LLM tests.
- [X] T029 [P] Run a fresh build with no outbound network access to `api.buildin.ai` (firewall / hosts entry); confirm all tests still pass — proves SC-007's "no buildin network" invariant extends across this feature's additions.
- [ ] T030 [P] Run `quickstart.md` § "CLI demo" and § "MCP demo" manually with a real `BUILDOUT__BUILDIN__BOT_TOKEN` and a real workspace containing at least one page matching a known query: confirm styled CLI output, plain CLI output (TSV bytes), the `awk | xargs buildout get` pipeline (SC-008), CLI/MCP body equality via `diff`, and the four error-class exit codes (2/3/4/5/6).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies. T001 is a guardrail; instant.
- **Foundational (Phase 2)**: Depends on Phase 1.
- **User Story 1 (Phase 3)**: Depends on Phase 2 (needs `ISearchService` + `ISearchResultFormatter` + DI registration in T014).
- **User Story 2 (Phase 4)**: Depends on Phase 2 (same).
- **User Story 3 (Phase 5)**: Depends on Phases 3 AND 4 (both surfaces wired). T025 extends the test file from T017; T026 extends the test file from T021.
- **Polish (Phase 6)**: Depends on Phases 3, 4, and 5 being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2. No dependency on US2.
- **US2 (P1)**: Can start after Phase 2. No dependency on US1.
- **US3 (P2)**: Depends on US1 + US2 — its tests exercise both surface boundaries built in those phases.

US1 and US2 are fully parallelisable post-foundation: they touch different presentation projects (`Buildout.Cli` vs `Buildout.Mcp`) and different test subtrees. US3 follows once both surfaces are wired.

### Within Each Phase

**Phase 2 internal ordering**:

- A (T002 → T003): tests then implementation, sequential because both touch single test/source files.
- B (T004): independent.
- C (T005 → T006): tests then impl.
- D (T007 → T008): tests then impl. T008 depends on T004.
- E (T009 → T010): tests then impl. T010 depends on T004.
- F (T011): independent of A/B/C/D/E (interface only). Depends on T004 for `SearchMatch` type.
- G (T012 → T013): tests then impl. T013 depends on T004 + T006 + T008 + T011 (the orchestrator wires them all).
- H (T014, T015): T014 depends on every concrete impl (T006, T008, T010, T013) existing. T015 depends on T014.

In practice for a single contributor: do A first (the mapping fix unblocks `Page.ObjectType` for downstream tests), then B in parallel with C/D/E/F, then G, then H.

For multi-contributor: A is the critical path (single file changes); after T003, B/C/D/E/F can be claimed in parallel; G is the join; H is the seal.

**Phase 3 (US1) internal ordering**:

- T016, T017: both [P] — different test files. Write both first.
- T018: [P] with T019 — different files; depends on T016.
- T019: depends on the foundation (T013, T010) AND on T018 existing for DI; verifies T017 passes.
- T020: depends on T019. Final verification.

**Phase 4 (US2) internal ordering**:

- T021, T022: both [P] — same file (`PageReadingLlmTests.cs` is shared with feature 002 but the new test method is independent). Different test files (T021 in `Mcp/`, T022 in `Llm/`). Write both first.
- T023: [P] — sole implementation file; depends on the foundation.
- T024: depends on T023. Verifies T021 + T022 pass.

**Phase 5 (US3) internal ordering**:

- T025, T026: both [P] — different test files; both extend files from T017/T021 respectively, so they cannot be written until those exist.
- T027: depends on T025 + T026.

### Parallel Opportunities

- **Phase 2 A + B + C + D + E + F**: After T003 lands, T004 + T005 + T007 + T009 + T011 can all proceed in parallel — five different files. Once T004 lands, T006 + T008 + T010 + T013 can be implemented as soon as their respective tests exist.
- **Phase 3 + Phase 4**: entirely parallelisable across two contributors after T015.
- **Phase 5 T025 + T026**: parallel after T017 + T021 land.
- **Phase 6 T028 + T029 + T030**: T029 + T030 in parallel; T028 is the gate.

---

## Parallel Example: Phase 2 — independent foundation pieces after the mapping fix

```text
# Round 1 (sequential): land the mapping fix
Task T002: failing tests for MapV1SearchResponse Title + Parent
Task T003: implement the mapping fix

# Round 2 (parallel): five independent foundation pieces
Task T004: SearchMatch + SearchObjectType
Task T005: failing tests for TitleRenderer
Task T007: failing tests for AncestorScopeFilter
Task T009: failing tests for SearchResultFormatter
Task T011: ISearchService interface

# Round 3 (parallel): four independent implementations
Task T006: implement TitleRenderer
Task T008: implement AncestorScopeFilter
Task T010: implement SearchResultFormatter
Task T012: failing tests for SearchService

# Round 4: orchestrator
Task T013: implement SearchService

# Round 5: DI seal
Task T014: register all search seams
Task T015: DI smoke test
```

## Parallel Example: Phase 3 (US1) tests

```text
# Launch both US1 test tasks together:
Task T016: SearchResultStyledRenderer unit tests
Task T017: SearchCommand integration tests
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 (Setup): T001.
2. Phase 2 (Foundational): T002 → T015 — the bulk of the work; full search service + formatter.
3. Phase 3 (US1): T016 → T020 — CLI command landed.
4. **STOP and VALIDATE**: `buildout search <query>` works against a real workspace; quickstart.md § CLI demo (plain + styled) passes. MVP shippable without MCP.

### Incremental Delivery

1. Setup + Foundational → service ready, all foundation pieces covered by unit tests.
2. Add US1 (CLI) → demo via terminal.
3. Add US2 (MCP) → demo via MCP client / cheap-LLM chain test.
4. Add US3 (scoped) → end-to-end scope tests on both surfaces.
5. Polish → final SC validation.

### Parallel Team Strategy

With multiple developers post-Phase 2:

1. Team completes Phase 1 and Phase 2 together; Phase 2 fans out across A → (B/C/D/E/F) → G → H.
2. After T015:
   - Developer A: US1 (Phase 3)
   - Developer B: US2 (Phase 4)
3. After both stories integrate, Developer A or B drives Phase 5 (US3 scope tests), then Phase 6 polish.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- [Story] label maps task to specific user story for traceability.
- US1 and US2 are independently completable and testable; either can ship before the other.
- US3 is the smallest of the three phases — most of its work was done in foundation; it is a surface-level verification phase, not a separate implementation.
- Tests MUST fail before implementation — non-negotiable per Constitution Principle IV.
- Round-trip tests are N/A this feature for the same reason as feature 002: there is no writing tool yet, so the constitution's symmetric-direction requirement does not apply.
- Commit after each task or logical group; do not bundle unrelated changes.
- No test may make a network call to `api.buildin.ai` (FR-017, SC-007).
- The cheap-LLM test (T022) is the only test that makes a network call — to Anthropic's Haiku endpoint, gated by `ANTHROPIC_API_KEY`, skipped when unset.
- No secret or token may appear in committed source (FR-019).
- Generated code under `src/Buildout.Core/Buildin/Generated/` MUST NOT be hand-edited; only the hand-written `BotBuildinClient.cs`, the new hand-written `Search/` subtree under `Buildout.Core`, the new `Tools/` subtree under `Buildout.Mcp`, and the new command/renderer pair under `Buildout.Cli` are touched.
