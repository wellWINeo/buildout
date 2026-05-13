# Implementation Plan: Page Creation from Markdown

**Branch**: `006-page-creation` | **Date**: 2026-05-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/006-page-creation/spec.md`

## Summary

Add the first user-visible *write* capability to buildout: turn a Markdown
document into a new buildin page. The new capability is the structural
inverse of feature 002's page read — same block compatibility matrix, same
inline mention forms, same surface parity (CLI + MCP).

A new core service, `IPageCreator`, owns the operation end-to-end:
(1) probe the parent kind from the supplied id (`GET /v1/pages/{id}` first,
`GET /v1/databases/{id}` fallback — spec FR-010 clarification),
(2) parse the Markdown document into a tree of `Block` payloads using
**Markdig** (lifted from `Buildout.Cli` into `Buildout.Core` so the
converter lives in the shared core per Principle I), (3) extract the page
title from the leading `# Heading` if present (spec FR-005), (4) issue
`POST /v1/pages` with up to 100 top-level body blocks embedded directly in
`CreatePageRequest.Children`, (5) issue follow-up
`PATCH /v1/blocks/{id}/children` batches for any remaining top-level blocks
and for every nested level (≤100 children per request — spec FR-008).

The CLI surface (`buildout create`) and the MCP tool (`create_page`) are
thin adapters that resolve arguments, call `IPageCreator`, and translate
the result. The two surfaces deliberately diverge on wire form: the CLI
prints the new page id on stdout under `--print id`; the MCP tool returns
a single `ResourceLinkBlock` whose URI is `buildin://<new_page_id>` (spec
FR-014 clarification). Both encodings are tested for *id equivalence*
rather than byte-identity (spec SC-004).

No new buildin client methods. `CreatePageAsync` and
`AppendBlockChildrenAsync` already exist on `IBuildinClient` from feature
001. Tests run against the existing WireMock-based buildin fixture from
feature 004; the new feature adds stubs for `POST /v1/pages` and
`PATCH /v1/blocks/{id}/children`, plus probe stubs that mirror the
`GET /v1/pages/{id}` and `GET /v1/databases/{id}` patterns already in
place.

## Technical Context

**Language/Version**: C# / .NET 10. Inherited from features 001–005.

**Primary Dependencies**: One existing-package shuffle, no genuinely new
production deps:

- **Markdig 1.1.3** — already a `Buildout.Cli` package reference (used
  by `MarkdownTerminalRenderer` to render markdown to a styled terminal).
  This feature moves the `PackageReference` from `Buildout.Cli` to
  `Buildout.Core` (or duplicates it; the CLI keeps using it for terminal
  rendering, so a duplicate `PackageReference` may be the lowest-friction
  fix). The Markdig usage in Core is *parsing* (`Markdown.Parse(...)`
  into a `MarkdownDocument` AST); the CLI usage remains *rendering*.
  These are independent surfaces of the same library — the dependency
  doesn't grow.
- `Microsoft.Extensions.DependencyInjection.Abstractions` — already
  present in Core for the existing converter registry.
- `ModelContextProtocol` 1.2.0 — already present in MCP. This feature
  declares the new `create_page` tool's return type as
  `ModelContextProtocol.Protocol.CallToolResult` containing one
  `ResourceLinkBlock`, per the SDK pattern documented in
  `ModelContextProtocol.Core.xml`.
- `Spectre.Console.Cli` — already present in CLI; new `create` command
  follows the existing pattern.

**Storage**: N/A. No persistence introduced; the only state is the new
buildin page itself, owned by buildin.

**Testing**: xUnit + NSubstitute, inherited. New tests:

- Unit tests in `Buildout.UnitTests` for the Markdown→blocks parser per
  block type, per inline-formatting case, per mention round-trip case,
  plus title-extraction edge cases (no H1, H1 not first, multiple H1s,
  H1-only document).
- Round-trip tests (constitution Principle III, write direction) in
  `Buildout.UnitTests`: for every fixture page used by feature 002's
  golden tests, render → parse-back → render → assert equality under
  the compatibility matrix.
- Integration tests in `Buildout.IntegrationTests` exercising
  `CreateCommand` and `CreatePageToolHandler` against the WireMock
  fixture, with new stubs for `POST /v1/pages`,
  `PATCH /v1/blocks/{id}/children`, plus probe stubs. A negative-path
  test asserts the partial-failure error message contains the partial
  page id.
- A cheap-LLM MCP integration test extending feature 002/003's harness
  to demonstrate `create_page` → `buildin://{new_page_id}` chaining
  (spec FR-017).
- A contract test asserting the create code path issues no
  `updateBlock` / `updatePage` / `deleteBlock` / `updateDatabase` /
  `createDatabase` calls under any input (spec SC-008).

**Target Platform**: Same as existing surfaces — .NET 10 console
processes; no platform change.

**Project Type**: Internal feature touching three of the existing five
projects (`Buildout.Core`, `Buildout.Cli`, `Buildout.Mcp`) plus their
two test projects. No new projects.

**Performance Goals**:

- A Markdown document of up to 1000 lines (~300 top-level blocks)
  converts and creates in under 4 seconds excluding buildin network
  latency, on the CI runner used by feature 004 (verified via an
  integration test that times the operation against the WireMock
  fixture with zero injected latency).
- Total buildin round-trips for a document of N top-level blocks with
  M nested-child parents: 1 probe GET + 1 `createPage` +
  `⌈(N−100)/100⌉` follow-up `appendBlockChildren` for the trailing
  top-level blocks + one `appendBlockChildren` per nested parent
  block. No per-block fetches. No retries (transport errors propagate
  as failure classes).

**Constraints**:

- No real buildin network calls during tests (Constitution IV; spec
  FR-016).
- The create code path must touch no buildin endpoints outside
  `GET page` (probe), `GET database` (probe fallback),
  `POST /v1/pages`, and `PATCH /v1/blocks/{id}/children`. Verified by
  a contract test enumerating the WireMock requests the create
  operation produces (spec SC-008).
- The new page id printed by CLI `--print id` (plain mode) and the id
  encoded in the MCP `ResourceLinkBlock`'s URI must be the same
  string (spec SC-004; verified by an integration test that runs both
  surfaces over the same fixture and extracts the ids).
- Partial-failure responses must name the partial page id in stderr
  (CLI) and in the MCP error message body (spec FR-012, FR-015).

**Scale/Scope**:

- 1 new core service interface + 1 implementation (`IPageCreator` /
  `PageCreator`).
- 1 new Markdown-AST-to-block parser interface + ~10 per-block-type
  parsers (paragraph, heading1/2/3, bulleted item, numbered item,
  to-do, code, quote, divider, plus the unsupported-block placeholder
  pass-through).
- 1 new inline parser handling inline formatting (bold, italic, inline
  code, plain links) plus mention recovery from
  `[Title](buildin://<id>)`.
- 1 new title extractor.
- 1 new batcher that splits a top-level block list into ≤100-element
  batches and walks nested levels with follow-up appends.
- 1 new CLI command (`create`) + its settings type.
- 1 new MCP tool (`create_page`) on a new handler class.
- ~8 new unit-test files (parser, per-block-type parsers, title
  extractor, mention recovery, batcher, error-class mapping,
  inline-format parser, round-trip suite).
- ~3 new integration-test files (CLI happy + error paths, MCP happy +
  error paths, partial-failure id surfacing).
- ~1 cheap-LLM integration test extension (chain `create_page` →
  `buildin://{new_page_id}`).
- `BuildinStubs` gains `RegisterCreatePage`,
  `RegisterAppendBlockChildren`, and helpers to inject mid-stream
  failures for the partial-failure test.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|---|---|---|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | All Markdown→blocks parsing, title extraction, batching, and `createPage` / `appendBlockChildren` orchestration live in `Buildout.Core` behind `IPageCreator`. CLI and MCP adapters resolve arguments, call `IPageCreator`, and translate the result for the user. Neither adapter parses Markdown, builds Block payloads, or talks to buildin directly. Markdig is lifted into Core so the parser can live in the shared layer. |
| II | LLM-Friendly Output Fidelity | ✅ PASS | The new direction (Markdown → blocks) preserves the same semantic structure feature 002's read direction emits. Inline formatting, mentions, and unsupported-block placeholders all round-trip per the compatibility matrix, which gains a new "write" column documented in `data-model.md`. The CLI prints a single line (`<id>\n`) under `--print id`; the MCP tool returns a `ResourceLinkBlock` — both deterministic. Buildin-internal noise (request payloads, internal property ids) never appears in user-facing output. |
| III | Bidirectional Round-Trip Testing (NON-NEGOTIABLE) | ✅ PASS | This feature is *why* the principle exists. Every supported block type ships with a Markdown → blocks → Markdown round-trip test; every block type with a feature 002 golden fixture ships with a blocks → Markdown → blocks round-trip. Mentions and inline formatting are exercised in both directions. The compatibility matrix's "lossy" rows (currently only `@user` mentions and date mentions inside otherwise-supported blocks, per spec FR-004) are explicitly enumerated in `data-model.md` and exercised by tests that assert the loss is exactly the documented loss — no silent additional drift. |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | Tasks (Phase 2) will be ordered red-first: every parser, batcher, title extractor, CLI command, MCP tool, and round-trip case has a failing test before its implementation. WireMock-based integration tests use the existing fixture from feature 004; no real buildin host. No tests skipped or disabled. |
| V | Buildin API Abstraction | ✅ PASS | `IPageCreator` consumes the existing `IBuildinClient` interface — `GetPageAsync` / `GetDatabaseAsync` for the probe, `CreatePageAsync` for the write, `AppendBlockChildrenAsync` for trailing and nested batches. No new methods. A future User API client would slot in without changes here. |
| VI | Non-Destructive Editing | ✅ PASS | This is a pure *create* operation. It writes one new page; it never modifies, archives, deletes, or replaces any pre-existing block, page, or database. The destructive-intent surface is satisfied by the command name (`create`) and the MCP tool description (FR-013 / FR-019). The contract test in `Buildout.IntegrationTests/Cross/CreatePageReadOnlyOnExistingDataTests.cs` asserts no `updateBlock` / `updatePage` / `deleteBlock` / `updateDatabase` / `createDatabase` request is recorded by the WireMock fixture across the entire test suite for this feature. |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | New code respects `Directory.Build.props`. |
| `ModelContextProtocol` SDK | ✅ | `create_page` returns `CallToolResult` directly (SDK-supported return type) so it can attach one `ResourceLinkBlock`. |
| `Spectre.Console.Cli` | ✅ | `create` registered via the existing `config.AddCommand<T>(name)` pattern. |
| Solution layout (5 projects) | ✅ | No new projects; only files added under existing ones. |
| Bot API as one impl of `IBuildinClient` | ✅ | Creator uses the interface only. |
| Secrets from env/config | ✅ | No new secrets introduced. |

| Out-of-scope item | Respected? |
|---|---|
| Admin dashboard | ✅ Not added. |
| Managed/enterprise deployment | ✅ Not added. |
| Multi-tenant hosting | ✅ Not added. |
| New buildin client methods or auth modes | ✅ None added. |
| Block updates / deletes / page archival | ✅ Creator is create-only. |

**Gate result (pre-Phase 0)**: PASS — no unjustified violations.

**Re-check after Phase 1 design**: PASS — the Phase 1 design
(data-model, contracts, quickstart) preserves all gates above. The
creator interface stays in core; CLI/MCP adapters stay thin; the only
buildin endpoints touched are the four enumerated in
`contracts/buildin-endpoints.md`.

`Complexity Tracking` table remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/006-page-creation/
├── plan.md                 # This file (/speckit-plan output)
├── spec.md                 # /speckit-specify output (+ /speckit-clarify)
├── research.md             # Phase 0 output (this command)
├── data-model.md           # Phase 1 output (this command)
├── quickstart.md           # Phase 1 output (this command)
├── contracts/              # Phase 1 output
│   ├── core-creator.md         # IPageCreator surface
│   ├── markdown-parser.md      # IMarkdownToBlocksParser surface
│   ├── cli-create.md           # `create` command surface
│   ├── mcp-create.md           # `create_page` tool surface (resource_link)
│   └── buildin-endpoints.md    # WireMock stubs ↔ openapi.json
├── checklists/
│   └── requirements.md     # spec quality checklist (already created)
└── tasks.md                # Phase 2 (/speckit-tasks output — NOT this command)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    Buildout.Core.csproj                                # MODIFIED: add <PackageReference Include="Markdig" />
    Markdown/
      Authoring/                                        # NEW directory (inverse of Conversion/)
        IPageCreator.cs                                 # NEW: end-to-end create entry point
        PageCreator.cs                                  # NEW: probes + parses + creates + appends
        CreatePageInput.cs                              # NEW: validated input (parent id, markdown, title, icon, cover, properties)
        CreatePageOutcome.cs                            # NEW: { NewPageId, PartialPageId?, FailureClass? }
        IMarkdownToBlocksParser.cs                      # NEW: pure Markdown → IReadOnlyList<BlockSubtreeWrite>
        MarkdownToBlocksParser.cs                       # NEW: Markdig-based impl
        BlockSubtreeWrite.cs                            # NEW: write-direction mirror of BlockSubtree (block + children)
        TitleExtractor.cs                               # NEW: leading-H1 consumption rule (spec FR-005)
        AppendBatcher.cs                                # NEW: 100-block batching + nested-level fanout
        ParentKindProbe.cs                              # NEW: GET page → GET database fallback
        IMarkdownBlockParser.cs                         # NEW: per-block-type parser interface
        Blocks/
          ParagraphBlockParser.cs                       # NEW
          HeadingBlockParser.cs                         # NEW: heading_1/2/3
          BulletedListItemBlockParser.cs                # NEW
          NumberedListItemBlockParser.cs                # NEW
          ToDoBlockParser.cs                            # NEW: GFM task list
          CodeBlockParser.cs                            # NEW
          QuoteBlockParser.cs                           # NEW
          DividerBlockParser.cs                         # NEW: thematic break
          UnsupportedBlockPlaceholderPassThrough.cs     # NEW: keep placeholder lines from feature 002 FR-003 as comments (no Block emitted)
        Inline/
          IInlineMarkdownParser.cs                      # NEW
          InlineMarkdownParser.cs                       # NEW: bold/italic/inline code/links → RichText
          MentionLinkRecovery.cs                        # NEW: parse [Title](buildin://<id>) → mention RichText
        Properties/
          IDatabasePropertyValueParser.cs               # NEW: --property name=value → PropertyValue
          DatabasePropertyValueParser.cs                # NEW: per-property-type dispatch (title, rich_text, number, select, multi_select, checkbox, date, url, email, phone_number)
    DependencyInjection/
      ServiceCollectionExtensions.cs                    # MODIFIED: register IPageCreator + parsers + batcher + property parser

  Buildout.Cli/
    Buildout.Cli.csproj                                 # MODIFIED: Markdig package reference may stay (still used for terminal rendering) or be removed if Core's transitive reference suffices
    Commands/
      CreateCommand.cs                                  # NEW: Spectre.Console.Cli command "create"
      CreateSettings.cs                                 # NEW: positional <markdown_source> + --parent, --title, --icon, --cover, --property (repeatable), --print
    Program.cs                                          # MODIFIED: config.AddCommand<CreateCommand>("create")

  Buildout.Mcp/
    Tools/
      CreatePageToolHandler.cs                          # NEW: [McpServerTool(Name = "create_page")] returning CallToolResult with one ResourceLinkBlock
    Program.cs                                          # MODIFIED: .WithTools<CreatePageToolHandler>()

tests/
  Buildout.UnitTests/
    Markdown/
      Authoring/                                        # NEW directory
        MarkdownToBlocksParserTests.cs                  # NEW: orchestration + AST walking
        TitleExtractorTests.cs                          # NEW: 6 cases (H1 first, H1 not first, no H1, H1-only, multiple H1s, empty doc)
        AppendBatcherTests.cs                           # NEW: ≤100 batching + nested level fanout
        Blocks/
          ParagraphBlockParserTests.cs                  # NEW
          HeadingBlockParserTests.cs                    # NEW (covers H1/H2/H3)
          BulletedListItemBlockParserTests.cs           # NEW
          NumberedListItemBlockParserTests.cs           # NEW
          ToDoBlockParserTests.cs                       # NEW
          CodeBlockParserTests.cs                       # NEW
          QuoteBlockParserTests.cs                      # NEW
          DividerBlockParserTests.cs                    # NEW
        Inline/
          InlineMarkdownParserTests.cs                  # NEW (bold/italic/code/link)
          MentionLinkRecoveryTests.cs                   # NEW (page mention, db mention, plain http link unchanged)
        Properties/
          DatabasePropertyValueParserTests.cs           # NEW (10 property kinds × valid + invalid input)
    RoundTrip/                                          # NEW directory (write-direction round-trips)
      ReadCreateReadRoundTripTests.cs                   # NEW: for each feature-002 golden fixture, blocks → md → blocks → md and assert under compatibility matrix
      WriteReadRoundTripTests.cs                        # NEW: for each authored md fixture, md → blocks → md and assert under compatibility matrix

  Buildout.IntegrationTests/
    Buildin/
      BuildinStubs.cs                                   # MODIFIED: RegisterCreatePage + RegisterAppendBlockChildren (incl. mid-stream failure helper) + RegisterProbe(page|database)
    Cli/
      CreateCommandTests.cs                             # NEW: end-to-end CLI through WireMock — happy path + every error class + partial-failure id surfacing
    Mcp/
      CreatePageToolTests.cs                            # NEW: end-to-end MCP through WireMock — happy path returns ResourceLinkBlock; error classes mapped; partial-failure error contains partial page id
      CreatePageRoundTripWithCheapLlmTests.cs           # NEW: extend cheap-LLM harness to chain create_page → buildin://{new_id}
    Cross/
      CreatePageIdEquivalenceTests.cs                   # NEW: CLI --print id stdout == id encoded in MCP ResourceLinkBlock URI for same fixture (SC-004)
      CreatePageReadOnlyOnExistingDataTests.cs          # NEW: contract test — across every test in this feature's integration suite, no updateBlock/updatePage/deleteBlock/updateDatabase/createDatabase request was recorded (SC-008)
```

**Structure Decision**: All new production code lives under existing
projects, organised under a new `Markdown/Authoring/` namespace in
`Buildout.Core`. The directory name is the deliberate inverse of the
existing `Markdown/Conversion/` (which holds the *read* direction's
block-to-markdown converters). The CLI and MCP additions follow the
exact patterns established by `GetCommand` / `PageResourceHandler`,
`SearchCommand` / `SearchToolHandler`, and `DbViewCommand` /
`DatabaseViewToolHandler`. No new test project. Round-trip tests live
under a new `Buildout.UnitTests/RoundTrip/` directory because they
exercise both the existing `PageMarkdownRenderer` (read) and the new
`MarkdownToBlocksParser` (write) and thus don't fit cleanly under
either namespace.

## Phase 0: Research (output: research.md)

The following items were unknown at the start of `/speckit-plan` and
are resolved in `research.md`:

- **R1 – Markdown parser choice**: hand-roll a CommonMark parser, lift
  Markdig from `Buildout.Cli` to `Buildout.Core`, or add a different
  parser library? *(Decision: lift Markdig to Core. It is already in
  the solution at 1.1.3 for terminal-rendering use; the parser entry
  point `Markdown.Parse(...)` produces a `MarkdownDocument` AST we
  walk. CLI's existing `MarkdownTerminalRenderer` keeps its own
  Markdig usage for rendering. Hand-rolling rejected: CommonMark is
  too large a grammar to re-implement just for this feature.)*

- **R2 – Title extraction from the Markdig AST**: which AST traversal
  pinpoints the "leading H1" the read side emits, and how does it
  interact with the "blank line after the H1" rule from spec FR-005?
  *(Decision: take the first child of `MarkdownDocument` after
  skipping any leading `LinkReferenceDefinitionGroup` / empty
  paragraph; if it is a `HeadingBlock` with `Level == 1`, consume it
  and remove it from the document before block-parsing. The "blank
  line after H1" is CommonMark-implicit — Markdig doesn't emit
  blank-line nodes. The body starts at the next AST child. Documented
  in `contracts/markdown-parser.md`.)*

- **R3 – Mention recovery from Markdown links**: should we register a
  custom Markdig inline extension, or post-process the AST?
  *(Decision: post-process. Walk every `LinkInline` whose `Url`
  starts with `buildin://`; replace it in the emitted `RichText` run
  with a buildin mention RichText annotation rather than a link
  annotation. Cheaper than a Markdig pipeline extension and easier to
  test in isolation. Page-vs-database mention disambiguation:
  feature 002's read side emits the same `buildin://<id>` URI for
  both kinds, so on the way back in we emit a page-mention by default
  and let buildin's response classify the id; mismatches surface as
  buildin errors. This matches spec FR-004.)*

- **R4 – Nested children fanout**: buildin's
  `AppendBlockChildrenRequest` body takes a flat `children` array
  (each item is `{ type, data }` per OpenAPI), and `BlockData` does
  **not** carry a nested `children` field. `CreatePageRequest.Children`
  is also a flat `IReadOnlyList<Block>`. So nested-child blocks
  (sub-bullets inside a list item, etc.) cannot ride along in the
  parent's create or append request. *(Decision: post-create fanout.
  After `appendBlockChildren` for a level returns its
  `AppendBlockChildrenResult` with the created block ids, the batcher
  recurses into each child-bearing parent and issues
  `appendBlockChildren(parent_id, ...)` with that parent's children
  — up to 100 per batch. The `AppendBatcher` owns the recursion; the
  per-block-type parser owns the in-memory child tree but emits flat
  batches grouped by parent.)*

- **R5 – Parent kind probing semantics**: spec FR-010 fixes
  page-first, database-fallback. The remaining choice is whether the
  probe is a single sequential GET-then-GET or a parallel two-call
  probe. *(Decision: sequential — `GET page` first, on 404 only then
  `GET database`. The 404 case is rare in practice (users mostly
  create under page parents), and parallel probes would double the
  rate-limit cost on the common path. Workspace / space-id parents
  are **deferred**: buildin's openapi.json exposes no
  `GET /v1/spaces/{id}` endpoint, and the clarified probe sequence
  has no way to recognise a space id. Spec FR-009 still references
  "workspace identifier"; the plan flags this as a residual spec
  drift and proposes resolving it by tightening FR-009 in the next
  clarification or in `/speckit-tasks`'s spec-sync pass. The
  implementation will surface workspace-id-shaped parents as
  parent-not-found until a follow-up feature adds workspace
  support.)*

- **R6 – Property value parsing for `--property name=value`**: how do
  we serialize each of the 10 supported plain-text property kinds
  (`title`, `rich_text`, `number`, `select`, `multi_select`,
  `checkbox`, `date`, `url`, `email`, `phone_number`)? *(Decision:
  per-kind dispatch keyed on the database schema fetched during the
  probe. `multi_select` splits on `,` with whitespace trim;
  `checkbox` accepts `true`/`false`/`yes`/`no` case-insensitive;
  `date` accepts ISO 8601 (`YYYY-MM-DD` or with time/timezone);
  `number` parses invariant-culture decimal; remaining string types
  pass through. Unknown property name → validation error before any
  network write call.)*

- **R7 – MCP `resource_link` return shape via the SDK**: the SDK
  supports declaring an `[McpServerTool]` method's return type as
  `ModelContextProtocol.Protocol.CallToolResult` directly (per
  `ModelContextProtocol.Core.xml`). *(Decision: declare the
  `create_page` handler as `Task<CallToolResult>`; populate
  `result.Content` with a single
  `ModelContextProtocol.Protocol.ResourceLinkBlock` whose `Uri` is
  `buildin://<new_page_id>` and whose `Name` is the page title. Set
  `result.IsError = false`. Other tools in this project continue to
  return `Task<string>` (auto-wrapped as a `TextContentBlock`); this
  is the one tool that deliberately diverges, per spec FR-014
  clarification.)*

- **R8 – Partial-failure error message format**: spec FR-012 / FR-015
  require the partial page id in the error message but do not pin
  exact wording. *(Decision: stderr / MCP error text =
  `"Partial creation: page <new_page_id> exists but
  appendBlockChildren failed after <K> of <N> top-level batches:
  <underlying message>"`. The id is the first non-whitespace token
  on the line after the colon-space-page prefix so grep / awk can
  extract it without parsing JSON. Recorded in
  `contracts/cli-create.md` and `contracts/mcp-create.md`.)*

- **R9 – Stdin handling and large documents**: when the
  `markdown_source` is `-`, we read stdin to a `string`.
  *(Decision: hard cap stdin reading at 16 MiB to bound memory;
  documents larger than that are rejected with a validation error
  before parsing. The cap matches the practical ceiling Markdig
  handles comfortably; buildin's per-batch limit (100 blocks) is the
  true downstream throttle. Filesystem-path inputs are not capped —
  the user knows what file they pointed at.)*

## Phase 1: Design & Contracts

### data-model.md

Captures the in-memory shapes the creator operates over (none are
new buildin entities):

- **`CreatePageInput`** — validated input: parent id (string),
  markdown string, optional title, optional icon (emoji or external
  URL), optional cover URL, optional `Dictionary<string, string>` of
  property name → raw value strings, optional print mode (CLI only).
- **`ParentKind`** — discriminated value: `Page(string id)`,
  `Database(Database schema)` (carries the schema so the property
  parser can dispatch), or `NotFound`.
- **`AuthoredDocument`** — the parser's pure output: the extracted
  title (if any), the ordered top-level block list, and for each
  block with children a `BlockSubtreeWrite` carrying its children
  recursively.
- **`BlockSubtreeWrite`** — write-direction sibling of feature 002's
  read-direction `BlockSubtree` (under `Markdown/Internal/`): a
  `Block` payload + an ordered `IReadOnlyList<BlockSubtreeWrite>` of
  children.
- **`CreatePageOutcome`** — success/partial-failure result returned
  by `IPageCreator`: `NewPageId`, optional `PartialPageId`,
  optional `FailureClass` (validation, not-found, auth, transport,
  unexpected), optional `UnderlyingException`. Internal type; not
  exposed across the project boundary directly — surfaces translate
  it.
- **`CompatibilityMatrixEntry`** — extended from feature 002's
  matrix: gains a `WriteDirection` column (`lossless`,
  `lossy-documented`, `unsupported-paragraph-fallback`,
  `unsupported-noop`). The matrix lives in `data-model.md` and is
  exercised by the round-trip tests.

### contracts/

Five contract documents define the surfaces this feature adds:

- **`core-creator.md`** — `IPageCreator` surface:
  `Task<CreatePageOutcome> CreateAsync(CreatePageInput input,
  CancellationToken)`. Documents validation rules (non-empty
  markdown OR explicit title; valid property names against the
  probed schema; valid property kinds; icon/cover shapes). Documents
  the buildin call sequence in spec terms and the error-class
  taxonomy it surfaces (`BuildinApiException` propagated for
  buildin errors; `ArgumentException` for validation; a dedicated
  `PartialCreationException(string newPageId, …)` for the post-
  `createPage` failure path so adapters can name the partial page
  in their output).

- **`markdown-parser.md`** — `IMarkdownToBlocksParser` surface:
  `AuthoredDocument Parse(string markdown)`. Documents the AST
  walk, title-extraction rule (spec FR-005), the per-block-type
  parser registry pattern (mirroring `BlockToMarkdownRegistry` in
  shape), the mention recovery pass, and the unsupported-block
  placeholder pass-through. Lists every supported AST node type
  from Markdig and the buildin block it maps to.

- **`cli-create.md`** — `buildout create <markdown_source>
  [--parent <id>] [--title <text>] [--icon <emoji_or_url>]
  [--cover <url>] [--property <name>=<value>]*
  [--print <id|json|none>]`. Documents how the command dispatches
  to `IPageCreator`, the exit-code table reused from features
  002/003 (validation 2, not-found 3, auth 4, transport 5,
  unexpected 6, partial-creation 6 with partial id in stderr), and
  the per-flag validation messages.

- **`mcp-create.md`** — `create_page` tool with arguments
  `parent_id` (required), `markdown` (required), `title`, `icon`,
  `cover_url`, `properties` (object). Documents the return type
  (`CallToolResult` with one `ResourceLinkBlock`, uri
  `buildin://<new_page_id>`, name = page title), the error-class
  mapping (404 → `ResourceNotFound`; 401/403 → `InternalError`;
  transport → `InternalError`; validation → `InvalidParams`;
  partial-creation → `InternalError` with the partial page id in
  the message), and the *deliberate* divergence from the
  byte-identical CLI/MCP invariant used by the read-only tools.

- **`buildin-endpoints.md`** — the exhaustive list of buildin
  endpoints this feature touches: `GET /v1/pages/{id}` (probe),
  `GET /v1/databases/{id}` (probe fallback), `POST /v1/pages`
  (create), `PATCH /v1/blocks/{id}/children` (append, repeated).
  Documents the WireMock stub shapes for the integration tests and
  asserts that no other endpoint may be invoked by any code path
  in this feature — the read-only-on-existing-data contract test
  in `Cross/CreatePageReadOnlyOnExistingDataTests.cs` enforces
  this.

### quickstart.md

Three short paragraphs:

1. Author a `notes.md` file with a leading `# Title` and some body.
   Run `dotnet run --project src/Buildout.Cli -- create
   --parent <page_id> notes.md` to create a new page. The command
   prints the new page id; pipe it into `buildout get <id>` to
   read what you just wrote.
2. Pipe markdown from another tool: `echo "# Hello\n\nWorld" |
   dotnet run --project src/Buildout.Cli -- create --parent <id>
   -`.
3. From an MCP client, invoke the `create_page` tool with
   `parent_id` and `markdown`; the response is a `resource_link`
   whose URI is the new page's `buildin://` URI — fetch it back
   through the `buildin://{page_id}` resource to see the rendered
   result.

### Agent context update

`CLAUDE.md` (project root) currently references
`specs/005-database-views/plan.md` between the `<!-- SPECKIT START -->`
and `<!-- SPECKIT END -->` markers. Phase 1 updates that link to
`specs/006-page-creation/plan.md`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
