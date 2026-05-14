---
description: "Task list for feature 006: page creation from Markdown"
---

# Tasks: Page Creation from Markdown

**Input**: Design documents from `/specs/006-page-creation/`
**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)
**Prerequisites**: plan.md ✅ · spec.md ✅ · research.md ✅ · data-model.md ✅ · contracts/ ✅

**Tests**: Mandatory per Constitution Principle IV (Test-First, NON-NEGOTIABLE). Every unit and integration test is written and confirmed failing before the code that satisfies it. Round-trip tests required per Principle III.

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel with other [P]-marked tasks in the same section (different files, no mutual dependency)
- **[Story]**: User story this task belongs to (US1, US2, US3)
- Exact file paths included in every description

---

## Phase 1: Setup

**Purpose**: Package changes and in-memory data types used across all phases. No behavior yet — just structures and the WireMock stub helpers the integration tests will need.

- [x] T001 Add `<PackageReference Include="Markdig" Version="0.38.0" />` to `src/Buildout.Core/Buildout.Core.csproj` (lift from Buildout.Cli; CLI keeps its reference for terminal rendering)
- [x] T002 [P] Define `CreatePageInput` record and `CreatePagePrintMode` enum in `src/Buildout.Core/Markdown/Authoring/CreatePageInput.cs` (fields: ParentId, Markdown, Title?, Icon?, CoverUrl?, Properties?, Print? — see data-model.md)
- [x] T003 [P] Define `CreatePageOutcome` record, `FailureClass` enum, and `PartialCreationException` class in `src/Buildout.Core/Markdown/Authoring/CreatePageOutcome.cs` (fields: NewPageId, PartialPageId?, FailureClass?, UnderlyingException? — see data-model.md)
- [x] T004 [P] Define `AuthoredDocument` record in `src/Buildout.Core/Markdown/Authoring/AuthoredDocument.cs` (fields: Title?, Body IReadOnlyList\<BlockSubtreeWrite\>)
- [x] T005 [P] Define `BlockSubtreeWrite` record in `src/Buildout.Core/Markdown/Authoring/BlockSubtreeWrite.cs` (fields: Block, Children IReadOnlyList\<BlockSubtreeWrite\>)
- [x] T006 [P] Define `ParentKind` abstract record with `Page(string PageId)`, `Database(Database Schema)`, and `NotFound` cases in `src/Buildout.Core/Markdown/Authoring/ParentKind.cs`
- [x] T007 [P] Add WireMock stub helpers to `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs`: `RegisterPageProbe`, `RegisterPageProbeNotFound`, `RegisterDatabaseProbe`, `RegisterDatabaseProbeNotFound`, `RegisterCreatePage`, `RegisterAppendBlockChildren`, `RegisterAppendBlockChildrenFailure` (see contracts/buildin-endpoints.md)

**Checkpoint**: Types and stub helpers compile. No implementation logic yet.

---

## Phase 2: Foundational

**Purpose**: Interface contracts that bind all downstream phases. All phases 3–5 depend on these. DI registration deferred until Phase 3 implementations exist.

**⚠️ CRITICAL**: No user story work can begin until these interfaces are defined.

- [x] T008 [P] Define `IMarkdownBlockParser` interface (per-block-type single-dispatch) in `src/Buildout.Core/Markdown/Authoring/IMarkdownBlockParser.cs`
- [x] T009 [P] Define `IInlineMarkdownParser` interface (`ParseInlines(ContainerInline, ...) → IReadOnlyList<RichText>`) in `src/Buildout.Core/Markdown/Authoring/Inline/IInlineMarkdownParser.cs`
- [x] T010 [P] Define `IDatabasePropertyValueParser` interface (`Parse(string name, string raw, PropertyItem schema) → PropertyValue`) in `src/Buildout.Core/Markdown/Authoring/Properties/IDatabasePropertyValueParser.cs`
- [x] T011 [P] Define `IMarkdownToBlocksParser` interface (`Parse(string markdown) → AuthoredDocument`) in `src/Buildout.Core/Markdown/Authoring/IMarkdownToBlocksParser.cs`
- [x] T012 [P] Define `IPageCreator` interface (`CreateAsync(CreatePageInput, CancellationToken) → Task<CreatePageOutcome>`) in `src/Buildout.Core/Markdown/Authoring/IPageCreator.cs`

**Checkpoint**: Solution compiles cleanly. All interfaces defined; no implementations yet.

---

## Phase 3: User Story 1 — CLI create from Markdown file (Priority: P1) 🎯 MVP

**Goal**: `buildout create --parent <id> file.md` creates a buildin page from a Markdown document. Title from leading H1. Body blocks batched per the 100-block limit. Prints new page id on stdout.

**Independent Test**: Run `CreateCommandTests.cs` against WireMock — asserts one `POST /v1/pages` with correct parent and title, body batches in document order, stdout contains page id, exit 0.

### Unit Tests — write first, confirm failing (all [P])

- [x] T013 [P] [US1] Write `InlineMarkdownParserTests` covering bold, italic, inline code, plain http link, buildin:// link promotion to mention, hard line break in `tests/Buildout.UnitTests/Markdown/Authoring/Inline/InlineMarkdownParserTests.cs`
- [x] T014 [P] [US1] Write `MentionLinkRecoveryTests` covering buildin:// → page mention, http link unchanged, db-vs-page disambiguation deferred to server-side in `tests/Buildout.UnitTests/Markdown/Authoring/Inline/MentionLinkRecoveryTests.cs`
- [x] T015 [P] [US1] Write `TitleExtractorTests` covering 6 cases: H1 first, H1 not first (stays as body block), no H1 (returns null title), H1-only document (empty body), multiple H1s (only first consumed), empty document in `tests/Buildout.UnitTests/Markdown/Authoring/TitleExtractorTests.cs`
- [x] T016 [P] [US1] Write `ParagraphBlockParserTests` covering plain text, bold, italic, inline code, mixed formatting, buildin:// link in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/ParagraphBlockParserTests.cs`
- [x] T017 [P] [US1] Write `HeadingBlockParserTests` covering H1→heading_1, H2→heading_2, H3→heading_3, H4+→paragraph with `#…` text preserved in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/HeadingBlockParserTests.cs`
- [x] T018 [P] [US1] Write `BulletedListItemBlockParserTests` covering simple items, nested sub-bullets, inline formatting inside items in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/BulletedListItemBlockParserTests.cs`
- [x] T019 [P] [US1] Write `NumberedListItemBlockParserTests` covering ordered lists, nested numbered items in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/NumberedListItemBlockParserTests.cs`
- [x] T020 [P] [US1] Write `ToDoBlockParserTests` covering `- [ ]` unchecked, `- [x]` checked, GFM task-list toggle in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/ToDoBlockParserTests.cs`
- [x] T021 [P] [US1] Write `CodeBlockParserTests` covering fenced code with language tag, fenced code without tag, indented code (if Markdig emits it) in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/CodeBlockParserTests.cs`
- [x] T022 [P] [US1] Write `QuoteBlockParserTests` covering single-line blockquote, multi-line blockquote, nested formatting inside quote in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/QuoteBlockParserTests.cs`
- [x] T023 [P] [US1] Write `DividerBlockParserTests` covering `---`, `***`, `___` thematic breaks in `tests/Buildout.UnitTests/Markdown/Authoring/Blocks/DividerBlockParserTests.cs`
- [x] T024 [P] [US1] Write `AppendBatcherTests` covering ≤100 top-level blocks (no follow-up call), >100 blocks (batched in order), nested children fanout (post-create appendBlockChildren per parent), empty body (no append calls) in `tests/Buildout.UnitTests/Markdown/Authoring/AppendBatcherTests.cs`
- [x] T025 [P] [US1] Write `DatabasePropertyValueParserTests` for all 10 property kinds (title, rich_text, number, select, multi_select comma-split, checkbox true/false/yes/no, date ISO 8601, url, email, phone_number) × valid + invalid input in `tests/Buildout.UnitTests/Markdown/Authoring/Properties/DatabasePropertyValueParserTests.cs`
- [x] T026 [P] [US1] Write `ParentKindProbeTests` covering page found (returns Page), 404 page then db found (returns Database with schema), 404 page and 404 db (returns NotFound), probe call order (page-first, then db) in `tests/Buildout.UnitTests/Markdown/Authoring/ParentKindProbeTests.cs`
- [x] T029 [P] [US1] Implement `InlineMarkdownParser` in `src/Buildout.Core/Markdown/Authoring/Inline/InlineMarkdownParser.cs`
- [x] T030 [P] [US1] Implement `MentionLinkRecovery` in `src/Buildout.Core/Markdown/Authoring/Inline/MentionLinkRecovery.cs`
- [x] T031 [P] [US1] Implement `TitleExtractor` in `src/Buildout.Core/Markdown/Authoring/TitleExtractor.cs`
- [x] T032 [P] [US1] Implement `ParagraphBlockParser` in `src/Buildout.Core/Markdown/Authoring/Blocks/ParagraphBlockParser.cs`
- [x] T033 [P] [US1] Implement `HeadingBlockParser` (heading_1/2/3; H4+ falls through to ParagraphBlockParser) in `src/Buildout.Core/Markdown/Authoring/Blocks/HeadingBlockParser.cs`
- [x] T034 [P] [US1] Implement `BulletedListItemBlockParser` (with recursive children) in `src/Buildout.Core/Markdown/Authoring/Blocks/BulletedListItemBlockParser.cs`
- [x] T035 [P] [US1] Implement `NumberedListItemBlockParser` (with recursive children) in `src/Buildout.Core/Markdown/Authoring/Blocks/NumberedListItemBlockParser.cs`
- [x] T036 [P] [US1] Implement `ToDoBlockParser` (GFM TaskList extension; `Checked` from `[ ]`/`[x]`) in `src/Buildout.Core/Markdown/Authoring/Blocks/ToDoBlockParser.cs`
- [x] T037 [P] [US1] Implement `CodeBlockParser` (`Language` from Info string) in `src/Buildout.Core/Markdown/Authoring/Blocks/CodeBlockParser.cs`
- [x] T038 [P] [US1] Implement `QuoteBlockParser` in `src/Buildout.Core/Markdown/Authoring/Blocks/QuoteBlockParser.cs`
- [x] T039 [P] [US1] Implement `DividerBlockParser` in `src/Buildout.Core/Markdown/Authoring/Blocks/DividerBlockParser.cs`
- [x] T040 [P] [US1] Implement `UnsupportedBlockPlaceholderPassThrough` (detects `<!-- unsupported block: … -->` raw HTML; emits no Block) in `src/Buildout.Core/Markdown/Authoring/Blocks/UnsupportedBlockPlaceholderPassThrough.cs`
- [x] T041 [P] [US1] Implement `AppendBatcher` (sequential ≤100-element top-level batching + nested-level fanout via post-create `appendBlockChildren` per parent block id) in `src/Buildout.Core/Markdown/Authoring/AppendBatcher.cs`
- [x] T042 [P] [US1] Implement `DatabasePropertyValueParser` (per-kind dispatch for 10 property kinds; validation errors for unsupported kinds) in `src/Buildout.Core/Markdown/Authoring/Properties/DatabasePropertyValueParser.cs`
- [x] T043 [P] [US1] Implement `ParentKindProbe` (sequential `GetPageAsync` → 404 → `GetDatabaseAsync` → 404 → `NotFound`; carries Database schema when found) in `src/Buildout.Core/Markdown/Authoring/ParentKindProbe.cs`
- [x] T044 [US1] Implement `MarkdownToBlocksParser` (Markdig pipeline with CommonMark + GFM TaskList; per-block-type parser registry; title extraction; inline parser pass; mention recovery) in `src/Buildout.Core/Markdown/Authoring/MarkdownToBlocksParser.cs`
- [x] T045 [US1] Implement `PageCreator` (probe → validate → parse → POST /v1/pages with first 100 children → batcher for remainder and nested levels → return outcome; `PartialCreationException` on mid-append failure) in `src/Buildout.Core/Markdown/Authoring/PageCreator.cs`
- [x] T046 [P] [US1] Define `CreateSettings` (positional `markdown_source`, `--parent`, `--title`, `--icon`, `--cover`, `--property` repeatable, `--print`) in `src/Buildout.Cli/Commands/CreateSettings.cs`
- [x] T047 [US1] Implement `CreateCommand` (resolve file/stdin source; call `IPageCreator`; map `CreatePageOutcome` to exit codes per contracts/cli-create.md; print per `--print` mode; stderr for partial failure with partial id) in `src/Buildout.Cli/Commands/CreateCommand.cs`
- [x] T048 [US1] Register `IPageCreator` → `PageCreator`, `IMarkdownToBlocksParser` → `MarkdownToBlocksParser`, all block parsers, inline parser, property parser, `AppendBatcher` in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- [x] T049 [US1] Register `CreateCommand` with `config.AddCommand<CreateCommand>("create")` in `src/Buildout.Cli/Program.cs`

**Checkpoint**: `dotnet test tests/Buildout.UnitTests` and `dotnet test tests/Buildout.IntegrationTests --filter Category=Cli` pass. US1 fully functional and testable.

---

## Phase 4: User Story 2 — MCP `create_page` tool (Priority: P1)

**Goal**: LLMs calling the MCP server discover and invoke `create_page` and receive a `ResourceLinkBlock` pointing at `buildin://<new_page_id>`. The new page id is identical to what the CLI prints.

**Independent Test**: `CreatePageToolTests.cs` — WireMock integration; asserts response contains one `resource_link` content item; error-class mapping; partial failure message contains partial id. Plus `CreatePageIdEquivalenceTests.cs` — asserts CLI `--print id` stdout equals id encoded in MCP `resource_link` URI (SC-004).

### Tests — write first, confirm failing

- [x] T050 [P] [US2] Write `CreatePageToolTests` (WireMock integration): happy path returns `CallToolResult` with one `ResourceLinkBlock` whose Uri is `buildin://<id>` and Name equals page title; validation error → `InvalidParams`; not-found → `ResourceNotFound`; auth/transport → `InternalError`; partial failure → `InternalError` containing partial page id in `tests/Buildout.IntegrationTests/Mcp/CreatePageToolTests.cs`
- [x] T051 [P] [US2] Write `CreatePageIdEquivalenceTests` (SC-004): for the same WireMock fixture, run CLI `--print id` and extract stdout id; run MCP `create_page` and extract id from `resource_link` URI; assert equal in `tests/Buildout.IntegrationTests/Cross/CreatePageIdEquivalenceTests.cs`

### Implementations

- [x] T052 [US2] Implement `CreatePageToolHandler` (class with `[McpServerToolType]`; method `CreatePageAsync` with `[McpServerTool(Name = "create_page")]` returning `Task<CallToolResult>`; builds `CreatePageInput` from parameters; maps `CreatePageOutcome` to `ResourceLinkBlock` or throws `McpProtocolException`) in `src/Buildout.Mcp/Tools/CreatePageToolHandler.cs`
- [x] T053 [US2] Register `CreatePageToolHandler` with `.WithTools<CreatePageToolHandler>()` in `src/Buildout.Mcp/Program.cs`

**Checkpoint**: `dotnet test tests/Buildout.IntegrationTests --filter "Category=Mcp|Category=Cross"` passes for US2 tests. CLI and MCP surfaces both functional; id equivalence confirmed.

---

## Phase 5: User Story 3 — Round-trip fidelity (Priority: P2)

**Goal**: Read a page with `buildout get`, edit the Markdown, create a new page with `buildout create`. The new page preserves every supported block type per the compatibility matrix (data-model.md). The write direction's round-trip suite closes the constitution Principle III loop opened by feature 002.

**Independent Test**: `ReadCreateReadRoundTripTests.cs` and `WriteReadRoundTripTests.cs` each exercise every supported block type and report exactly the documented compatibility-matrix rows. `CreatePageRoundTripWithCheapLlmTests.cs` demonstrates LLM chaining `create_page` → `buildin://{new_page_id}` end-to-end (FR-017).

### Tests — write first, confirm failing

- [x] T054 [P] [US3] Write `ReadCreateReadRoundTripTests`: for each feature-002 golden fixture, `RenderAsync(blocks) → markdown → ParseBlocks → RenderAsync` and assert second rendering equals first under compatibility-matrix equivalence (lossless rows are byte-equal; lossy rows match documented loss); test for every block type in isolation and nested in `tests/Buildout.UnitTests/RoundTrip/ReadCreateReadRoundTripTests.cs`
- [x] T055 [P] [US3] Write `WriteReadRoundTripTests`: for each authored-md fixture (paragraph, heading 1–3, bulleted/numbered/todo list, code, quote, divider, mention, inline formatting), `Parse(markdown) → blocks → Render → compare`; assert under compatibility-matrix equivalence; mention links recover correctly; unsupported-block placeholder survives without materialising a block in `tests/Buildout.UnitTests/RoundTrip/WriteReadRoundTripTests.cs`
- [x] T056 [US3] Write `CreatePageRoundTripWithCheapLlmTests` extending the cheap-LLM MCP harness: call `create_page` with a Markdown body; extract `buildin://` URI from response; read back via `buildin://{id}` MCP resource; assert rendered Markdown matches input under compatibility matrix (FR-017) in `tests/Buildout.IntegrationTests/Mcp/CreatePageRoundTripWithCheapLlmTests.cs`

**Checkpoint**: `dotnet test tests/Buildout.UnitTests --filter "Category=RoundTrip"` passes. LLM chain test passes. Principle III fully satisfied in both directions.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Contract test for non-destructive behaviour (SC-008), performance baseline, and spec drift fix flagged in R5.

- [x] T057 [P] Write `CreatePageReadOnlyOnExistingDataTests` (SC-008): across every integration test in this feature's suite, assert WireMock recorded no `PATCH /v1/pages/{id}`, `PATCH /v1/blocks/{id}`, `DELETE /v1/blocks/{id}`, `PATCH /v1/databases/{id}`, `POST /v1/databases`, `POST /v1/databases/{id}/query`, `POST /v1/search`, `POST /v1/pages/search` requests in `tests/Buildout.IntegrationTests/Cross/CreatePageReadOnlyOnExistingDataTests.cs`
- [x] T058 [P] Add performance integration test to `tests/Buildout.IntegrationTests/Cli/CreateCommandTests.cs`: generate a 1000-line Markdown fixture (~300 top-level blocks); time the create operation against zero-latency WireMock; assert completion under 4 seconds (plan.md performance goal)
- [x] T059 Tighten `specs/006-page-creation/spec.md` FR-009: remove "or workspace identifier" from the `--parent` description; add a note that workspace parents are deferred per R5; ensure FR-009 text is consistent with the page-first/database-fallback probe in FR-010

**Final checkpoint**: Full suite passes — `dotnet test tests/Buildout.UnitTests && dotnet test tests/Buildout.IntegrationTests`. No real buildin network calls. All constitution principles PASS. Run the quickstart scenarios from `quickstart.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1** (T001–T007): No dependencies — start immediately
- **Phase 2** (T008–T012): Depends on Phase 1 (T002–T006 provide the types interfaces reference)
- **Phase 3** (T013–T049): Depends on Phase 2; tests (T013–T028) can start as soon as interfaces exist
- **Phase 4** (T050–T053): Depends on Phase 3 fully complete (US2 reuses `IPageCreator` + `PageCreator`)
- **Phase 5** (T054–T056): Depends on Phase 3 and Phase 4 complete (round-trips exercise both read and write; LLM test needs MCP tool)
- **Phase 6** (T057–T059): Depends on Phases 3–5 complete (contract test scans the full integration suite)

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational phase — no cross-story dependencies
- **US2 (P1)**: Depends on US1 complete (reuses `PageCreator` through DI; only adapts the MCP surface)
- **US3 (P2)**: Depends on US1 and US2 complete; US3 is test-and-documentation work, not new surface code

### Within Phase 3

- All unit-test tasks (T013–T027) are [P] once interfaces exist — write them all in parallel
- T028 (`CreateCommandTests`) depends on T007 (`BuildinStubs`) — write as test class shell before T029+
- All implementation tasks T029–T043 are [P] among themselves — different files, no mutual dependency
- T044 (`MarkdownToBlocksParser`) depends on T029–T042 passing unit tests
- T045 (`PageCreator`) depends on T043 (`ParentKindProbe`) and T044 (`MarkdownToBlocksParser`)
- T046 (`CreateSettings`) is [P] alongside T029–T043
- T047 (`CreateCommand`) depends on T045 and T046
- T048 (DI registration) depends on T045 — registers concrete implementations
- T049 (`Program.cs` CLI) depends on T046 (has the type reference)

---

## Parallel Example: Phase 3 unit tests

```text
# After T008–T012 (interfaces defined), launch all unit-test writing tasks together:
Task: T013 InlineMarkdownParserTests.cs
Task: T014 MentionLinkRecoveryTests.cs
Task: T015 TitleExtractorTests.cs
Task: T016 ParagraphBlockParserTests.cs
Task: T017 HeadingBlockParserTests.cs
Task: T018 BulletedListItemBlockParserTests.cs
Task: T019 NumberedListItemBlockParserTests.cs
Task: T020 ToDoBlockParserTests.cs
Task: T021 CodeBlockParserTests.cs
Task: T022 QuoteBlockParserTests.cs
Task: T023 DividerBlockParserTests.cs
Task: T024 AppendBatcherTests.cs
Task: T025 DatabasePropertyValueParserTests.cs
Task: T026 ParentKindProbeTests.cs

# Once tests fail cleanly, launch all implementation tasks together:
Task: T029 InlineMarkdownParser.cs
Task: T030 MentionLinkRecovery.cs
Task: T031 TitleExtractor.cs
Task: T032–T040 block parsers (8 files, fully independent)
Task: T041 AppendBatcher.cs
Task: T042 DatabasePropertyValueParser.cs
Task: T043 ParentKindProbe.cs
```

## Parallel Example: Phase 5 round-trip tests

```text
Task: T054 ReadCreateReadRoundTripTests.cs
Task: T055 WriteReadRoundTripTests.cs
# (T056 depends on MCP tool being complete, so starts after T052–T053)
```

---

## Implementation Strategy

### MVP First (US1 only — stop after Phase 3)

1. Phase 1: Setup (types + WireMock stubs) — ~1 session
2. Phase 2: Interfaces — ~1 session
3. Phase 3 tests: Write all unit + integration tests in parallel — confirm they fail
4. Phase 3 impl: Implement block parsers, inline parser, batcher, probe, orchestrator, CLI command in parallel where possible
5. **STOP and VALIDATE**: `dotnet test` passes. Run quickstart path 1 and 2. US1 is shippable.

### Incremental Delivery

1. Phases 1–3 → US1 (CLI create) → validate → demo
2. Phase 4 → US2 (MCP create_page) → validate → demo (LLM write flows now work)
3. Phase 5 → US3 (round-trip suite) → validate (constitution Principle III fully closed)
4. Phase 6 → Contract + performance + spec tightening → final clean build

### Task Count Summary

| Phase | Tasks | Notes |
|---|---|---|
| 1 Setup | T001–T007 (7) | Package + data types + WireMock stubs |
| 2 Foundational | T008–T012 (5) | Interfaces only |
| 3 US1 | T013–T049 (37) | 16 unit test files + 1 integration test + 18 impl files + 2 wiring tasks |
| 4 US2 | T050–T053 (4) | 2 integration tests + handler + wiring |
| 5 US3 | T054–T056 (3) | 2 round-trip suites + LLM chain test |
| 6 Polish | T057–T059 (3) | Non-destructive contract + perf test + spec fix |
| **Total** | **59** | |
