---

description: "Task list for Initial Page Reading"
---

# Tasks: Initial Page Reading

**Input**: Design documents from `/specs/002-basic-page-reading/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Round-trip tests are required for every block-type conversion (Principle III — read direction only in this feature; the symmetric write direction lands with the writing tool). Tests are written and observed to FAIL before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. Both user stories in this feature are P1 — they share the same foundation (the core Markdown renderer + converter subsystem) and can be delivered in either order, or in parallel after foundational work completes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2)
- Include exact file paths in descriptions

## Path Conventions

- **Source projects**: `src/Buildout.Core/`, `src/Buildout.Mcp/`, `src/Buildout.Cli/`
- **Test projects**: `tests/Buildout.UnitTests/`, `tests/Buildout.IntegrationTests/`
- **Solution**: `buildout.slnx` at repo root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the two new package dependencies this feature introduces. No new projects; the five-project layout from feature 001 stands.

- [ ] T001 [P] Add `Markdig` package reference (latest stable on .NET 10) to `src/Buildout.Cli/Buildout.Cli.csproj`
- [ ] T002 [P] Add `Anthropic.SDK` package reference (latest stable supporting Claude Haiku 4.5) to `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj`

**Checkpoint**: `dotnet restore buildout.slnx` succeeds; existing test suite still green.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Close the three latent feature-001 scaffold gaps (Page title extraction, RichText mention metadata, GetBlockChildrenAsync mapping), then build the OCP-shaped converter subsystem and the page renderer that both user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. Both US1 and US2 consume `IPageMarkdownRenderer` exclusively — neither has any rendering logic of its own.

### A. Domain model additions (additive, source-compatible)

- [ ] T003 [P] Create `Mention` discriminated union (`Mention` abstract record + sealed `PageMention` { string PageId }, `DatabaseMention` { string DatabaseId }, `UserMention` { string UserId, string? DisplayName }, `DateMention` { string Start, string? End }) in `src/Buildout.Core/Buildin/Models/Mention.cs` per `data-model.md` § Mention
- [ ] T004 [P] Add `Title` property of type `IReadOnlyList<RichText>?` to existing `Page` record in `src/Buildout.Core/Buildin/Models/Page.cs` per `data-model.md` § Page
- [ ] T005 [P] Add `Mention` property of type `Mention?` to existing `RichText` record in `src/Buildout.Core/Buildin/Models/RichText.cs` per `data-model.md` § RichText

### B. BotBuildinClient mapping fixes (TDD — tests first)

- [ ] T006 Add three failing test methods to `tests/Buildout.UnitTests/Buildin/BotBuildinClientTests.cs` covering: (a) `MapBlockChildrenResponse` returns the mapped `Block` list (currently returns `Results = []`); (b) `MapPage` populates `Title` from the title-typed property in the buildin response, regardless of property name; (c) `MapRichText` populates `Mention` for each of the four supported sub-types (page / database / user / date) and leaves it null for non-mention rich-text items
- [ ] T007 Apply three corresponding fixes in `src/Buildout.Core/Buildin/BotBuildinClient.cs`: (a) iterate generated children response and route each item through the existing `MapBlock` path; (b) extend `MapPage` to find the property whose discriminator type is `"title"` and map its rich-text via existing `MapRichText`; (c) extend `MapRichText` to read `Gen.RichTextItem.Mention` and instantiate the appropriate `Mention` subclass — verify T006 tests now pass

### C. Renderer public surface and shared infrastructure

- [ ] T008 [P] Create `IPageMarkdownRenderer` public interface (`Task<string> RenderAsync(string pageId, CancellationToken cancellationToken = default)`) in `src/Buildout.Core/Markdown/IPageMarkdownRenderer.cs` per `contracts/converter.md`
- [ ] T009 [P] Create `IBlockToMarkdownConverter` interface (`Type BlockClrType`, `string BlockType`, `bool RecurseChildren`, `void Write(Block, IReadOnlyList<BlockSubtree>, IMarkdownRenderContext)`) in `src/Buildout.Core/Markdown/Conversion/IBlockToMarkdownConverter.cs` per `contracts/block-converters.md`
- [ ] T010 [P] Create `IMentionToMarkdownConverter` interface (`Type MentionClrType`, `string MentionType`, `string Render(Mention, string displayText)`) in `src/Buildout.Core/Markdown/Conversion/IMentionToMarkdownConverter.cs` per `contracts/block-converters.md`
- [ ] T011 [P] Create `IMarkdownRenderContext` interface (`IMarkdownWriter Writer`, `IInlineRenderer Inline`, `int IndentLevel`, `IMarkdownRenderContext WithIndent(int delta)`, `void WriteBlockSubtree(BlockSubtree subtree)`) in `src/Buildout.Core/Markdown/Conversion/IMarkdownRenderContext.cs`
- [ ] T012 [P] Create `IInlineRenderer` interface (`string Render(IReadOnlyList<RichText>? items, int indentLevel)`) in `src/Buildout.Core/Markdown/Conversion/IInlineRenderer.cs`
- [ ] T013 [P] Create internal `BlockSubtree` record (`Block Block`, `IReadOnlyList<BlockSubtree> Children`) in `src/Buildout.Core/Markdown/Internal/BlockSubtree.cs`
- [ ] T014 [P] Create `IMarkdownWriter` interface and `MarkdownWriter` impl (StringBuilder-backed; `WriteLine`, `WriteBlankLine` honouring "no consecutive blank lines"; `ToString`) in `src/Buildout.Core/Markdown/Internal/MarkdownWriter.cs`

### D. Registries (TDD)

- [ ] T015 [P] Write failing unit tests for `BlockToMarkdownRegistry` (lookup by `Block` CLR type returns the registered converter; lookup for unregistered block type returns null; constructor throws `InvalidOperationException` on duplicate `BlockClrType` registration) in `tests/Buildout.UnitTests/Markdown/BlockToMarkdownRegistryTests.cs`
- [ ] T016 [P] Write failing unit tests for `MentionToMarkdownRegistry` (lookup by `Mention` CLR type returns the registered converter; null on miss; duplicate detection at construction) in `tests/Buildout.UnitTests/Markdown/MentionToMarkdownRegistryTests.cs`
- [ ] T017 Implement `BlockToMarkdownRegistry` (constructor takes `IEnumerable<IBlockToMarkdownConverter>`, builds `Dictionary<Type, IBlockToMarkdownConverter>` keyed by `BlockClrType`, throws on duplicate keys; `Resolve(Block block)` returns nullable converter) in `src/Buildout.Core/Markdown/Conversion/BlockToMarkdownRegistry.cs` — verify T015 passes
- [ ] T018 Implement `MentionToMarkdownRegistry` mirroring `BlockToMarkdownRegistry`'s shape in `src/Buildout.Core/Markdown/Conversion/MentionToMarkdownRegistry.cs` — verify T016 passes

### E. Unsupported-block handler (TDD)

- [ ] T019 Write failing unit tests in `tests/Buildout.UnitTests/Markdown/UnsupportedBlockHandlerTests.cs` covering: every unsupported block type listed in `data-model.md` § Compatibility Matrix produces the placeholder `<!-- unsupported block: <type> -->`; the placeholder is followed by a blank line; the placeholder string survives a Markdig CommonMark parse without errors
- [ ] T020 Implement `UnsupportedBlockHandler` (static `Write(Block, IMarkdownRenderContext)`) using `Block.Type` as the placeholder discriminator in `src/Buildout.Core/Markdown/Conversion/UnsupportedBlockHandler.cs` — verify T019 passes

### F. Block converters (one per supported type — TDD pairs, all [P] after interfaces exist)

> Each pair: write the failing test in `tests/Buildout.UnitTests/Markdown/Blocks/<Name>ConverterTests.cs` covering the cases enumerated in `contracts/block-converters.md` § Test obligations (canonical Markdown form per matrix; child recursion when `RecurseChildren = true`; indent-level honoured; degenerate empty content), then implement the converter at `src/Buildout.Core/Markdown/Conversion/Blocks/<Name>Converter.cs` following the data-model compatibility matrix.

- [ ] T021 [P] Write failing tests for `ParagraphConverter` in `tests/Buildout.UnitTests/Markdown/Blocks/ParagraphConverterTests.cs`
- [ ] T022 [P] Implement `ParagraphConverter` (BlockType `paragraph`, RecurseChildren false, emits inline content + blank line) in `src/Buildout.Core/Markdown/Conversion/Blocks/ParagraphConverter.cs`
- [ ] T023 [P] Write failing tests for `Heading1Converter` in `tests/Buildout.UnitTests/Markdown/Blocks/Heading1ConverterTests.cs`
- [ ] T024 [P] Implement `Heading1Converter` (BlockType `heading_1`, emits `## <inline>` per heading-shift rule) in `src/Buildout.Core/Markdown/Conversion/Blocks/Heading1Converter.cs`
- [ ] T025 [P] Write failing tests for `Heading2Converter` in `tests/Buildout.UnitTests/Markdown/Blocks/Heading2ConverterTests.cs`
- [ ] T026 [P] Implement `Heading2Converter` (emits `### <inline>`) in `src/Buildout.Core/Markdown/Conversion/Blocks/Heading2Converter.cs`
- [ ] T027 [P] Write failing tests for `Heading3Converter` in `tests/Buildout.UnitTests/Markdown/Blocks/Heading3ConverterTests.cs`
- [ ] T028 [P] Implement `Heading3Converter` (emits `#### <inline>`) in `src/Buildout.Core/Markdown/Conversion/Blocks/Heading3Converter.cs`
- [ ] T029 [P] Write failing tests for `BulletedListItemConverter` (including nested-children case) in `tests/Buildout.UnitTests/Markdown/Blocks/BulletedListItemConverterTests.cs`
- [ ] T030 [P] Implement `BulletedListItemConverter` (BlockType `bulleted_list_item`, RecurseChildren true, indent-aware `- <inline>`) in `src/Buildout.Core/Markdown/Conversion/Blocks/BulletedListItemConverter.cs`
- [ ] T031 [P] Write failing tests for `NumberedListItemConverter` (including nested-children case) in `tests/Buildout.UnitTests/Markdown/Blocks/NumberedListItemConverterTests.cs`
- [ ] T032 [P] Implement `NumberedListItemConverter` (RecurseChildren true, emits `1. <inline>` — CommonMark renumbers) in `src/Buildout.Core/Markdown/Conversion/Blocks/NumberedListItemConverter.cs`
- [ ] T033 [P] Write failing tests for `ToDoConverter` (checked + unchecked, nested children) in `tests/Buildout.UnitTests/Markdown/Blocks/ToDoConverterTests.cs`
- [ ] T034 [P] Implement `ToDoConverter` (RecurseChildren true, emits GFM `- [ ]` / `- [x] <inline>`) in `src/Buildout.Core/Markdown/Conversion/Blocks/ToDoConverter.cs`
- [ ] T035 [P] Write failing tests for `CodeConverter` (with and without language tag; multi-line content) in `tests/Buildout.UnitTests/Markdown/Blocks/CodeConverterTests.cs`
- [ ] T036 [P] Implement `CodeConverter` (RecurseChildren false, emits ` ```<lang>\n…\n``` `) in `src/Buildout.Core/Markdown/Conversion/Blocks/CodeConverter.cs`
- [ ] T037 [P] Write failing tests for `QuoteConverter` (single-line + multi-line content; nested children also quoted) in `tests/Buildout.UnitTests/Markdown/Blocks/QuoteConverterTests.cs`
- [ ] T038 [P] Implement `QuoteConverter` (RecurseChildren true, line-prefix `> `) in `src/Buildout.Core/Markdown/Conversion/Blocks/QuoteConverter.cs`
- [ ] T039 [P] Write failing tests for `DividerConverter` in `tests/Buildout.UnitTests/Markdown/Blocks/DividerConverterTests.cs`
- [ ] T040 [P] Implement `DividerConverter` (emits `---` thematic break) in `src/Buildout.Core/Markdown/Conversion/Blocks/DividerConverter.cs`

### G. Mention converters (one per supported sub-type — TDD pairs, all [P])

- [ ] T041 [P] Write failing tests for `PageMentionConverter` (`[displayText](buildin://<page_id>)`; fallback to displayText when PageId missing) in `tests/Buildout.UnitTests/Markdown/Mentions/PageMentionConverterTests.cs`
- [ ] T042 [P] Implement `PageMentionConverter` in `src/Buildout.Core/Markdown/Conversion/Mentions/PageMentionConverter.cs`
- [ ] T043 [P] Write failing tests for `DatabaseMentionConverter` (same form with database_id) in `tests/Buildout.UnitTests/Markdown/Mentions/DatabaseMentionConverterTests.cs`
- [ ] T044 [P] Implement `DatabaseMentionConverter` in `src/Buildout.Core/Markdown/Conversion/Mentions/DatabaseMentionConverter.cs`
- [ ] T045 [P] Write failing tests for `UserMentionConverter` (`@DisplayName`; fallback to displayText when DisplayName null) in `tests/Buildout.UnitTests/Markdown/Mentions/UserMentionConverterTests.cs`
- [ ] T046 [P] Implement `UserMentionConverter` in `src/Buildout.Core/Markdown/Conversion/Mentions/UserMentionConverter.cs`
- [ ] T047 [P] Write failing tests for `DateMentionConverter` (single date; date range with en-dash; null Start fallback) in `tests/Buildout.UnitTests/Markdown/Mentions/DateMentionConverterTests.cs`
- [ ] T048 [P] Implement `DateMentionConverter` in `src/Buildout.Core/Markdown/Conversion/Mentions/DateMentionConverter.cs`

### H. Inline renderer (TDD)

- [ ] T049 [P] Write failing unit tests for `InlineRenderer` covering: annotation stacking (bold + italic = `***text***`); link form `[text](href)` when `Href` is present; underline annotation is dropped (documented lossy); inline code wrap; mention dispatch through `MentionToMarkdownRegistry`; fallback to `RichText.Content` for unknown rich-text type or unknown mention sub-type; equation rich-text falls back to Content; empty / null input produces empty string in `tests/Buildout.UnitTests/Markdown/InlineRendererTests.cs`
- [ ] T050 Implement `InlineRenderer` (depends on `IInlineRenderer` from T012 and `MentionToMarkdownRegistry` from T018; constructor takes `MentionToMarkdownRegistry`; iterates `RichText` items and emits Markdown segment by segment) in `src/Buildout.Core/Markdown/Internal/InlineRenderer.cs` — verify T049 passes

### I. Page renderer orchestrator (TDD)

- [ ] T051 [P] Write failing unit tests for `PageMarkdownRenderer` in `tests/Buildout.UnitTests/Markdown/PageMarkdownRendererTests.cs` covering: title H1 prepended when `Page.Title` is non-null/non-empty; H1 omitted when `Title` is null or empty; full-tree fetch — multi-page `GetBlockChildrenAsync` with `HasMore = true / NextCursor` is drained until exhausted (mock `IBuildinClient` returns two pages, both pages' blocks appear in output); recursion follows per-converter `RecurseChildren = true` opt-in (mocked converter for a list-item type recurses; mocked converter for a leaf type does not); unsupported block parents are not recursed into (placeholder represents whole subtree); determinism — two consecutive `RenderAsync` calls against identical mock produce byte-identical strings; cancellation — `CancellationToken` propagation to the mocked client
- [ ] T052 Implement `PageMarkdownRenderer` (constructor takes `IBuildinClient`, `BlockToMarkdownRegistry`, `IInlineRenderer`, `ILogger<PageMarkdownRenderer>`; two-phase: async fetch building `BlockSubtree` via `GetPageAsync` + recursive `GetBlockChildrenAsync`, then sync walk dispatching through the registry to converter `Write` methods, with `UnsupportedBlockHandler` as the fallback; emits title H1 when present) in `src/Buildout.Core/Markdown/PageMarkdownRenderer.cs` — verify T051 passes

### J. DI registration

- [ ] T053 Extend `ServiceCollectionExtensions.AddBuildoutCore(...)` (or add a new `AddBuildoutMarkdown(...)` extension called from within `AddBuildoutCore`) in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` to register: all 10 `IBlockToMarkdownConverter` implementations as singletons; all 4 `IMentionToMarkdownConverter` implementations as singletons; `BlockToMarkdownRegistry` and `MentionToMarkdownRegistry` as singletons; `IInlineRenderer` → `InlineRenderer`; `IPageMarkdownRenderer` → `PageMarkdownRenderer`. Add unit test in `tests/Buildout.UnitTests/Markdown/DependencyInjectionTests.cs` confirming the service provider resolves `IPageMarkdownRenderer` end-to-end.

**Checkpoint**: `dotnet test buildout.slnx` is green. `IPageMarkdownRenderer` is fully implemented end-to-end with comprehensive per-converter unit coverage. Both user stories can now begin in parallel.

---

## Phase 3: User Story 1 — CLI: read a page as Markdown (Priority: P1) 🎯 MVP

**Goal**: A developer runs `buildout get <page_id>` and sees the page as Markdown — styled when stdout is a TTY, plain CommonMark when piped or redirected. Unsupported blocks render as visible placeholder comments at their original position. Errors map to distinct exit codes.

**Independent Test**: With a fake `IBuildinClient` returning a fixture page that contains heading, paragraph, bulleted list, numbered list, and code block, run `buildout get <id>` (a) into a real TTY → observe styled output; (b) piped to a file → observe raw CommonMark whose semantic structure round-trips through a CommonMark parser. Same fake → exit code 3 for not-found, 4 for auth, 5 for transport, 6 for unexpected.

### Tests for User Story 1

> **NOTE: Write these tests FIRST; they MUST compile and FAIL before the corresponding implementation lands.**

- [ ] T054 [P] [US1] Write failing unit tests for `TerminalCapabilities` (TTY+ANSI → `IsStyledStdout = true`; non-TTY OR `NO_COLOR` set → false; injectable via constructor for testing) in `tests/Buildout.UnitTests/Cli/TerminalCapabilitiesTests.cs`
- [ ] T055 [P] [US1] Write failing unit tests for `MarkdownTerminalRenderer` covering: H1/H2/H3 emit bold-styled output; bulleted/numbered/task lists render with bullets/numbers/checkboxes; fenced code blocks render in a `Panel` with the language tag in the header when present; block quotes render with `│` glyph + dim italic; thematic breaks render as `Rule`; HTML-comment placeholders render as dim grey text verbatim; inline annotations (bold, italic, strike, inline code) render via Spectre `Markup`; output uses an injected `IAnsiConsole` so tests can capture against `TestConsole` in `tests/Buildout.UnitTests/Cli/MarkdownTerminalRendererTests.cs`
- [ ] T056 [P] [US1] Write failing integration tests for `GetCommand` in `tests/Buildout.IntegrationTests/Cli/GetCommandTests.cs` covering: happy path (exit 0, stdout matches renderer output, stderr empty); plain-mode stdout is byte-identical to `IPageMarkdownRenderer.RenderAsync` output for the same fixture (FR-008, SC-003); rich-mode (forced via injected `TerminalCapabilities` fake) stdout contains ANSI escape codes; plain-mode stdout contains zero ANSI escapes; not-found error → exit 3 + stderr identifies page id; auth failure → exit 4; transport failure → exit 5; unexpected error → exit 6; missing positional `<page_id>` → Spectre's parser exits 1 or 2 with usage (smoke check, not strict)

### Implementation for User Story 1

- [ ] T057 [P] [US1] Implement `TerminalCapabilities` wrapping `Spectre.Console.AnsiConsole.Profile.Capabilities` (`IsStyledStdout` derived from ANSI capability + `Console.IsOutputRedirected` + `NO_COLOR` env honoured by Spectre) in `src/Buildout.Cli/Rendering/TerminalCapabilities.cs` — verify T054 passes
- [ ] T058 [P] [US1] Implement `MarkdownTerminalRenderer` — Markdig parses the Markdown produced by Core; the walker emits to an injected `IAnsiConsole` per the contract in `contracts/cli-get-command.md` § Rich-mode rendering — in `src/Buildout.Cli/Rendering/MarkdownTerminalRenderer.cs` — verify T055 passes
- [ ] T059 [US1] Implement `GetCommand` (extends `AsyncCommand<Settings>` from `Spectre.Console.Cli`; injects `IPageMarkdownRenderer`, `IAnsiConsole`, `TerminalCapabilities`, `MarkdownTerminalRenderer`; plain path writes to `Console.Out` directly, rich path delegates to `MarkdownTerminalRenderer`; exception switch maps to exit codes per `contracts/cli-get-command.md` § Error handling) in `src/Buildout.Cli/Commands/GetCommand.cs`
- [ ] T060 [US1] Update `src/Buildout.Cli/Program.cs` to: build a `Microsoft.Extensions.DependencyInjection.ServiceCollection`, register `Buildout.Core` services via `AddBuildoutCore(...)`, register `IAnsiConsole` (default), `TerminalCapabilities`, `MarkdownTerminalRenderer`; create a Spectre `CommandApp` with a `TypeRegistrar` adapter binding the DI container; register the `get` command via `cfg.AddCommand<GetCommand>("get").WithDescription("Read a buildin page as Markdown.")`; run with `app.RunAsync(args)`. Verify all T054–T056 tests now pass.

**Checkpoint**: `buildout get <page_id>` is fully functional. With a real bot token and page id, the CLI demo in `quickstart.md` § "CLI demo" works end-to-end. US1 is independently shippable.

---

## Phase 4: User Story 2 — MCP: expose a page as a `buildin://{page_id}` resource (Priority: P1)

**Goal**: An LLM connected to the buildout MCP server discovers the resource template `buildin://{page_id}`, reads it, and receives the page rendered as Markdown — byte-identical to the CLI's plain output for the same page.

**Independent Test**: Start the MCP server in-process against a fake `IBuildinClient` returning a fixture page. From a test client, list resource templates → see `buildin://{page_id}` advertised; read `buildin://<id>` → receive one `text/markdown` resource whose body equals the CLI's plain-mode output for the same page. Read a not-found id → receive an MCP-protocol error (not a 200 with an error blob).

### Tests for User Story 2

> **NOTE: Write these tests FIRST; they MUST FAIL before the corresponding implementation lands.**

- [ ] T061 [US2] Write failing integration tests for `PageResourceHandler` in `tests/Buildout.IntegrationTests/Mcp/PageResourceTests.cs` covering: the in-process MCP server advertises exactly one resource template with URI scheme `buildin://{page_id}`, name `buildin-page`, MIME type `text/markdown`, and the description string from `contracts/mcp-resource.md` § Resource template; reading `buildin://<known-id>` returns one `TextResourceContents` with `text/markdown` MIME and body matching `IPageMarkdownRenderer.RenderAsync` output; not-found error → MCP-protocol error (`-32002` or SDK equivalent) — never a 200 with an error blob; auth failure → MCP error message distinguishes auth from transport; transport failure → MCP error distinct from auth and not-found
- [ ] T062 [US2] Write failing cheap-LLM integration test in `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs` that: skips with a clear reason if `ANTHROPIC_API_KEY` is unset; constructs an in-process MCP client + the buildout server with a fake `IBuildinClient` returning a fixture page that exercises every supported block type; reads `buildin://<id>` to get the rendered Markdown; sends ONE prompt to Claude Haiku 4.5 (`claude-haiku-4-5-20251001`) containing the rendered Markdown and a list of factual questions (one per supported block type sourced from the fixture); asserts the LLM's response contains the expected facts (per spec SC-002). Cost target: well under one cent per run.

### Implementation for User Story 2

- [ ] T063 [P] [US2] Implement `PageResourceHandler` in `src/Buildout.Mcp/Resources/PageResourceHandler.cs` per `contracts/mcp-resource.md` § Implementation shape: constructor takes `IPageMarkdownRenderer` + `ILogger<PageResourceHandler>`; one read method declared via the SDK's resource-template attribute or builder (whichever 1.2.0 supports for `buildin://{page_id}`); returns one `TextResourceContents` with MIME `text/markdown`; catches `BuildinApiException` and maps `BuildinError` discriminator to MCP-protocol errors per the table in the contract; does not catch `OperationCanceledException`
- [ ] T064 [US2] Update `src/Buildout.Mcp/Program.cs` to: build a `Microsoft.Extensions.Hosting.HostApplicationBuilder`; register `Buildout.Core` via `AddBuildoutCore(...)`; register `PageResourceHandler` as a singleton; configure the `ModelContextProtocol` SDK to host the MCP server with the stdio transport and the `PageResourceHandler` registered; `await host.RunAsync()`. Verify T061 and (if `ANTHROPIC_API_KEY` is set) T062 pass.

**Checkpoint**: The MCP server exposes `buildin://{page_id}` and an LLM can successfully read pages through it. US2 is independently shippable.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final validation against the spec's success criteria.

- [ ] T065 Run `dotnet test buildout.slnx` with `ANTHROPIC_API_KEY` unset; confirm full feature suite is green and completes in < 30 s (SC-006). Run again with `ANTHROPIC_API_KEY` set; confirm still under 30 s including the cheap-LLM test.
- [ ] T066 [P] Run a fresh build with no outbound network access to `api.buildin.ai` (firewall / hosts entry); confirm all tests still pass — proves SC-006's "no buildin network" invariant.
- [ ] T067 [P] Verify `tests/Buildout.IntegrationTests/Cli/GetCommandTests.cs` `PlainOutputMatchesMcp` is green (SC-003 byte-equivalence between CLI plain mode and MCP resource body).
- [ ] T068 [P] Run `quickstart.md` § "CLI demo" and § "MCP demo" manually with a real `BUILDOUT__BUILDIN__BOT_TOKEN` and a real page id; confirm styled output, plain output, and at least one error case each.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Phase 1 (needs Markdig in Buildout.Cli for the Cli unit tests in Phase 3, and Anthropic.SDK in IntegrationTests for Phase 4 — both are package adds, fast).
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion (needs `IPageMarkdownRenderer` + DI registration in T053).
- **User Story 2 (Phase 4)**: Depends on Phase 2 completion (same).
- **Polish (Phase 5)**: Depends on Phases 3 and 4 being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Phase 2. No dependency on US2.
- **User Story 2 (P1)**: Can start after Phase 2. No dependency on US1.

US1 and US2 are fully parallelisable post-foundation: they touch different presentation projects (`Buildout.Cli` vs `Buildout.Mcp`) and different test subtrees.

### Within Each Phase

**Phase 2 internal ordering**:

- A (T003–T005): all [P] — three different model files.
- B (T006 → T007): tests then implementation, sequential because both touch single test/source files.
- C (T008–T014): all [P] — seven different interface/infra files. Can also run in parallel with A and B (different files entirely).
- D (T015, T016 → T017, T018): the two `…Tests.cs` files are [P] with each other; the two registry impls are [P] with each other; impls depend on their respective tests existing and on the converter interfaces from C.
- E (T019 → T020): test then impl, both depend on C and D.
- F (T021–T040): all converter test+impl pairs are [P] across the 10 types; within each pair the impl depends on its test having been written. Each impl also depends on T009 (interface), T011 (context), T014 (writer), and T050 (inline renderer) — that last one creates a soft dependency between F and H.
- G (T041–T048): same shape as F, four mention types, all [P]; impls depend on T010 + T018.
- H (T049 → T050): test [P] with everything else; impl depends on T012 + T018.
- I (T051 → T052): test [P] with everything else; impl depends on every preceding piece because it orchestrates the whole graph.
- J (T053): depends on every concrete converter impl plus the orchestrator existing. Final foundational task.

In practice for a single contributor: do A and C in parallel mentally, then B sequentially, then D, then E, then sweep through F + G + H in any order (their tests/impls can interleave), then I, then J.

For multi-contributor: A + C can be claimed in chunks of 3–4 [P] tasks each; F's 20 tasks naturally fan out to multiple developers; G's 8 tasks similarly.

**Phase 3 (US1) internal ordering**:

- T054, T055, T056: all [P] — three different test files. Write all three first.
- T057, T058: [P] with each other (different files); each depends on its corresponding test.
- T059: depends on T057 + T058 + the renderer (T053).
- T060: depends on T059. Verifies the integration tests pass.

**Phase 4 (US2) internal ordering**:

- T061: failing tests, no internal dependency.
- T062: failing tests, no internal dependency on T061 (different file).
- T063: implements the handler; depends on T061 (and T062 if running with key).
- T064: wires Program.cs; depends on T063. Verifies tests pass.

### Parallel Opportunities

- **Phase 1**: T001 + T002 in parallel (different csproj files).
- **Phase 2 A**: T003, T004, T005 all in parallel.
- **Phase 2 C**: T008–T014 all in parallel (7 tasks).
- **Phase 2 D**: T015 + T016 in parallel; T017 + T018 in parallel.
- **Phase 2 F**: 20 tasks across 10 converters — all 10 test tasks in parallel; once tests are in, all 10 impls in parallel.
- **Phase 2 G**: 8 tasks across 4 mention converters, same pattern.
- **Phase 3** + **Phase 4**: entirely parallelisable across two contributors after Phase 2.

---

## Parallel Example: Phase 2 — block converters

```text
# Round 1: write all 10 failing block-converter test files in parallel
Task T021: Tests for ParagraphConverter
Task T023: Tests for Heading1Converter
Task T025: Tests for Heading2Converter
Task T027: Tests for Heading3Converter
Task T029: Tests for BulletedListItemConverter
Task T031: Tests for NumberedListItemConverter
Task T033: Tests for ToDoConverter
Task T035: Tests for CodeConverter
Task T037: Tests for QuoteConverter
Task T039: Tests for DividerConverter

# Round 2: implement all 10 converters in parallel
Task T022: Implement ParagraphConverter
Task T024: Implement Heading1Converter
…
Task T040: Implement DividerConverter
```

## Parallel Example: Phase 3 (US1) tests

```text
# Launch all three US1 test tasks together:
Task T054: TerminalCapabilities tests
Task T055: MarkdownTerminalRenderer tests
Task T056: GetCommand integration tests
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 (Setup): T001, T002.
2. Phase 2 (Foundational): T003 → T053 — the bulk of the work; full converter subsystem + renderer.
3. Phase 3 (US1): T054 → T060 — CLI command landed.
4. **STOP and VALIDATE**: `buildout get <page_id>` works against a real buildin page; quickstart.md § CLI demo passes. MVP shippable.

### Incremental Delivery

1. Setup + Foundational → renderer ready, all converters covered by unit tests.
2. Add US1 (CLI) → demo via terminal.
3. Add US2 (MCP) → demo via MCP client / cheap-LLM test.
4. Polish → final SC validation.

### Parallel Team Strategy

With multiple developers post-Phase 2:

1. Team completes Phase 1 (one task each, fast) and Phase 2 together — Phase 2 is most of the work and naturally fans out across A/C/F/G [P] groups.
2. After T053:
   - Developer A: US1 (Phase 3)
   - Developer B: US2 (Phase 4)
3. Stories complete and integrate independently.
4. Either developer drives Phase 5 polish.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- [Story] label maps task to specific user story for traceability.
- US1 and US2 are independently completable and testable; either can ship before the other.
- Tests MUST fail before implementation — non-negotiable per Constitution Principle IV.
- Round-trip read tests for every supported block type are produced by Phase 2 § F (per-converter tests). The reverse direction (Markdown → block) is N/A this feature; the constitution's symmetric-direction requirement applies "for input formats accepted by the writing tool" and there is no writing tool yet.
- Commit after each task or logical group; do not bundle unrelated changes.
- No test may make a network call to `api.buildin.ai` (FR-013, SC-006).
- The cheap-LLM test (T062) is the ONLY test that makes a network call — to Anthropic's Haiku endpoint, gated by `ANTHROPIC_API_KEY`, skipped when unset.
- No secret or token may appear in committed source (FR-015).
- Generated code under `src/Buildout.Core/Buildin/Generated/` MUST NOT be hand-edited; only the hand-written `BotBuildinClient.cs` and the new hand-written `Markdown/` subtree are touched.
