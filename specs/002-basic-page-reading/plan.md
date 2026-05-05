# Implementation Plan: Initial Page Reading

**Branch**: `002-basic-page-reading` | **Date**: 2026-05-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-basic-page-reading/spec.md`

## Summary

Deliver the first user-visible read capability of buildout. `Buildout.Core` gains a
`PageMarkdownRenderer` that takes a buildin page ID, fetches the page and the full
descendant block tree (paginating `next_cursor` to exhaustion at every level via the
`IBuildinClient` from feature 001), and renders deterministic CommonMark/GFM. The
output begins with the page title as an H1, then the body. Supported blocks
(paragraph, heading 1/2/3, bulleted/numbered/todo lists, code, quote, divider) plus
inline formatting (bold/italic/strike/inline-code/links) and inline mentions
(page/database/user/date) are first-class; every other block type renders as a
single-line CommonMark-safe placeholder that names the block type. `Buildout.Mcp`
exposes one resource template `buildin://{page_id}` that returns the rendered
Markdown as a single text resource. `Buildout.Cli` adds `buildout get <page_id>`
which prints the same Markdown to stdout — raw when stdout is non-TTY, styled (via
Markdig + Spectre.Console primitives) when stdout is a styled terminal. All tests
mock buildin via the `IRequestAdapter` / `HttpMessageHandler` mechanisms already in
place; one integration test exercises the MCP resource through a cheap testing LLM
(skipped if the LLM key env var is absent).

The conversion is OCP-shaped: each supported block type is a separate
`IBlockToMarkdownConverter` class in its own file, registered via DI and dispatched
by a registry. Adding a new block type means adding one new class; no existing
class is modified. Inline mentions follow the same pattern via
`IMentionToMarkdownConverter`. The interface names are deliberately suffixed
`-ToMarkdown` so that the future writing feature can introduce a paired
`IMarkdownToBlockConverter` family without churn — see
`contracts/block-converters.md`.

This feature also closes three latent gaps in the feature-001 scaffold that block
the work: `BotBuildinClient.MapBlockChildrenResponse` currently returns
`Results = []` (missing the block-list mapping); `MapPage` does not populate the
page title; and `RichText` does not expose mention metadata. Each fix lives behind
the existing `IBuildinClient` interface so presentation projects are unaffected.

## Technical Context

**Language/Version**: C# / .NET 10 (per constitution Technology Standards). Already in place.

**Primary Dependencies (additions in this feature)**:

- `Markdig` (latest stable on .NET 10) — Markdown parsing for the CLI's rich-mode
  renderer. Used in `Buildout.Cli` only; never in `Buildout.Core`.
- `Spectre.Console` — already a transitive dependency via `Spectre.Console.Cli`; this
  feature uses its `AnsiConsole`, `Markup`, `Rule`, `Panel`, capability-detection
  surfaces.
- `ModelContextProtocol` — already referenced by `Buildout.Mcp`; this feature is the
  first concrete usage. Resource template support per SDK 1.2.0.
- `Anthropic.SDK` (or equivalent for the cheap-LLM integration test) — referenced
  only by `Buildout.IntegrationTests`. Pinned at the lightest version that supports
  Claude Haiku 4.5 (`claude-haiku-4-5-20251001`). Tests gracefully skip when
  `ANTHROPIC_API_KEY` is unset.

No new dependencies in `Buildout.Core` itself; the renderer emits Markdown as a
plain `string` and uses no Markdown library.

**Storage**: N/A — no persistence in this feature.

**Testing**: xUnit (already in place). Unit tests for the renderer mock
`IBuildinClient` directly via NSubstitute. Integration tests for MCP and CLI inject
a fake `IBuildinClient` (test double) into the host process; no Kiota / HTTP layer
is exercised in the conversion path. The cheap-LLM MCP test runs against a real
Anthropic Haiku endpoint — its only external call — and is skipped without a key.

**Target Platform**: cross-platform .NET 10 (macOS, Linux, Windows).

**Project Type**: same as feature 001 — single .NET solution with a library
(`Buildout.Core`) and two console-style apps (`Buildout.Mcp`, `Buildout.Cli`), plus
two test projects.

**Performance Goals (from spec SCs)**:

- Full feature test suite — including the cheap-LLM integration test if its key is
  present — completes in **< 30 s** on a developer laptop with no buildin network
  (SC-006).
- A single small page (≤ 50 blocks, ≤ 1 level of nesting) renders in well under
  one second locally (no spec target; not regression-tracked here).

**Constraints**:

- No outbound HTTPS to `api.buildin.ai` from any test (Constitution Principle IV;
  spec FR-013, SC-006).
- Output MUST be deterministic CommonMark/GFM (FR-004); two renderings of the same
  fetched payload must be byte-identical.
- CLI non-TTY output MUST equal the MCP resource body byte-for-byte (FR-008,
  SC-003) — both paths share a single `string` from the core renderer.
- The CLI MUST emit zero terminal escape codes when stdout is not a TTY (FR-007).
- Buildin internal IDs MUST NOT leak into rendered Markdown except where required
  by a supported block type's contract (FR-005); page-mention link targets are a
  permitted exception (`buildin://<page_id>` is the dereferenceable form).
- Generated client output (`src/Buildout.Core/Buildin/Generated/`) remains
  hand-edit-free; all changes for this feature are in hand-written code only.

**Scale/Scope**:

- 11 supported block types (paragraph, heading 1/2/3, bulleted list item, numbered
  list item, to_do, code, quote, divider, plus title H1).
- ~7 inline element shapes (text, link, bold, italic, strikethrough, underline,
  code) plus 4 mention sub-types (page, database, user, date).
- ~12 explicitly-unsupported block types in v1 (image, embed, table, table_row,
  column_list, column, child_page, child_database, synced_block, link_preview,
  toggle, equation/iframe/database mentions if any).
- Two presentation surfaces (CLI command, MCP resource). One Core renderer.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|---|---|---|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | The block→Markdown converter lives in `Buildout.Core/Markdown/PageMarkdownRenderer.cs`. `Buildout.Mcp` calls `IPageMarkdownRenderer.RenderAsync(pageId, ct)` and wraps the returned string in an MCP text resource. `Buildout.Cli` calls the same method, then optionally pipes the string through a Markdig-based terminal renderer (Markdown→ANSI conversion is a *transport* concern, not a buildin-domain concern). Neither presentation project parses buildin blocks or calls the buildin API directly. |
| II | LLM-Friendly Output Fidelity | ✅ PASS | Output is deterministic CommonMark with GFM only where required (todo task lists). Buildin-internal block UUIDs do not appear in the output. The per-block-type compatibility matrix is in `data-model.md` (§ Compatibility Matrix). Lossy conversions (image, embed, table, etc.) are enumerated and exercised by tests. |
| III | Bidirectional Round-Trip Testing | ⚠️ PARTIAL — JUSTIFIED | Read direction (blocks → Markdown) has full per-block-type tests under `tests/Buildout.UnitTests/Markdown/`. The reverse direction (Markdown → blocks → Markdown) is **N/A in this feature** because no writing tool exists yet — the constitution requires the symmetric direction "for input formats accepted by the writing tool", and there is no writing tool. The block-type compatibility matrix already exists for when the writing feature lands. No exception requested; this is the constitution's own "for input formats accepted by the writing tool" carve-out. |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | `/speckit-tasks` will produce failing tests first for each renderer rule, each unsupported-block placeholder, the title H1, the pagination loop, the CLI's TTY-detection branch, and the MCP resource shape. The cheap-LLM MCP integration test is one of those tests and runs only when its API key is set; absence does not silence the rest of the suite. No test is skipped, disabled, or deleted to make a build pass. |
| V | Buildin API Abstraction | ✅ PASS | The renderer depends only on `IBuildinClient` (already public surface). The three scaffold-bug fixes (`MapBlockChildrenResponse`, page title in `MapPage`, mention metadata on `RichText`) are all behind `IBuildinClient`'s existing surface or pure additions to hand-written domain models in `Buildout.Core/Buildin/Models/`. A future `UserApiBuildinClient` slots in unchanged. |
| VI | Non-Destructive Editing | ➖ N/A | Read-only feature. |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | All new code respects `Directory.Build.props`; generated namespace remains exempt from style rules only. |
| `ModelContextProtocol` SDK | ✅ | First real wiring of the SDK lands here; resource template registration uses the SDK's documented mechanism (see `research.md` R2). |
| `Spectre.Console.Cli` | ✅ | First real command (`get <page_id>`) lands here. CLI continues to use the framework for command definition, parsing, dispatch, and now `Spectre.Console`'s widgets for rich rendering. |
| Solution layout (5 projects) | ✅ | No new projects. |
| Bot-API as one impl of `IBuildinClient`; User API path open | ✅ | Renderer depends only on the interface. |
| Secrets from env/config; no committed tokens | ✅ | Buildin token continues to be supplied via configuration; the new cheap-LLM test reads `ANTHROPIC_API_KEY` from the environment and skips if absent. No secrets land in source. |

| Out-of-scope item | Respected? |
|---|---|
| Admin dashboard | ✅ Not added. |
| Managed/enterprise deployment | ✅ Not added. |
| Multi-tenant hosting | ✅ Not added. |
| CI configuration | ✅ Not added in this feature. |
| Pagination performance / caching / streaming | ✅ Explicitly deferred per spec Q2 clarification. |

**Gate result (pre-Phase 0)**: PASS — no unjustified violations. Principle III's
partial coverage is the constitution's own carve-out for read-only work.

**Re-check after Phase 1 design**: PASS — no new violations introduced.

- The renderer's public surface (`IPageMarkdownRenderer.RenderAsync(pageId, ct)`)
  is documented in `contracts/converter.md` and is the single seam between Core
  and presentation. Both `Buildout.Mcp` and `Buildout.Cli` consume only this
  surface for rendering.
- Internal dispatch is OCP-shaped: per-block-type converters under
  `Buildout.Core/Markdown/Conversion/Blocks/`, one file each, registered via
  DI; lookup happens through `BlockToMarkdownRegistry`. No `switch` over block
  types in the rendering pipeline. Same pattern for mention sub-types. Full
  design in `contracts/block-converters.md`. This pattern naturally extends to
  the future writing direction (`IMarkdownToBlockConverter`) without churn.
- The MCP resource template (`contracts/mcp-resource.md`) declares
  `buildin://{page_id}` with no buildin domain logic in `Buildout.Mcp`; the
  handler is a 5-line wrapper.
- The CLI command (`contracts/cli-get-command.md`) declares the
  `Spectre.Console.Cli` command shape; the rich renderer is a small Markdig→
  Spectre adapter scoped to the supported-block subset.
- The data-model document records the additive changes to `Page` (new `Title`
  field) and `RichText` (new `Mention` field) — both are pure additions, source-
  compatible with feature 001's existing shape.
- Compatibility matrix is complete; every supported block type has a target test;
  every unsupported block type has a placeholder-rendering test.

`Complexity Tracking` table remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/002-basic-page-reading/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── converter.md
│   ├── mcp-resource.md
│   └── cli-get-command.md
├── checklists/
│   └── requirements.md  # spec quality checklist (already created)
└── tasks.md             # Phase 2 (/speckit-tasks output — not in this command)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    Buildin/
      Models/
        Block.cs                          # unchanged from feature 001
        RichText.cs                       # MODIFIED: add Mention field (additive)
        Mention.cs                        # NEW: discriminated union for mention sub-types
        Page.cs                           # MODIFIED: add Title field (additive)
      BotBuildinClient.cs                 # MODIFIED: fix MapBlockChildrenResponse
                                          #           extend MapPage to extract title
                                          #           extend MapRichText to fill Mention
    Markdown/
      IPageMarkdownRenderer.cs            # NEW: public seam
      PageMarkdownRenderer.cs             # NEW: orchestrator (fetch + paginate + dispatch)
      Conversion/                         # NEW: per-type converter subsystem
        IBlockToMarkdownConverter.cs      # NEW: contract — one impl per block type
        IMentionToMarkdownConverter.cs    # NEW: contract — one impl per mention sub-type
        IMarkdownRenderContext.cs         # NEW: writer + recursion callback + state
        IInlineRenderer.cs                # NEW: rich-text walker (uses mention registry)
        BlockToMarkdownRegistry.cs        # NEW: keyed dispatch by Block CLR type
        MentionToMarkdownRegistry.cs      # NEW: keyed dispatch by Mention CLR type
        UnsupportedBlockHandler.cs        # NEW: placeholder format (single source)
        Blocks/                           # NEW: one file per supported block type
          ParagraphConverter.cs
          Heading1Converter.cs
          Heading2Converter.cs
          Heading3Converter.cs
          BulletedListItemConverter.cs
          NumberedListItemConverter.cs
          ToDoConverter.cs
          CodeConverter.cs
          QuoteConverter.cs
          DividerConverter.cs
        Mentions/                         # NEW: one file per supported mention sub-type
          PageMentionConverter.cs
          DatabaseMentionConverter.cs
          UserMentionConverter.cs
          DateMentionConverter.cs
      Internal/
        InlineRenderer.cs                 # NEW: walks rich-text, applies annotations,
                                          #      delegates mention rendering to the registry
        MarkdownWriter.cs                 # NEW: tiny append-only writer + spacing rules
        BlockSubtree.cs                   # NEW: orchestrator-built (block + children) wrapper
  Buildout.Mcp/
    Program.cs                            # MODIFIED: register host + resource handler
    Resources/
      PageResourceHandler.cs              # NEW: buildin://{page_id} → text resource
  Buildout.Cli/
    Program.cs                            # MODIFIED: register GetCommand with CommandApp
    Commands/
      GetCommand.cs                       # NEW: `buildout get <page_id>` command
    Rendering/
      MarkdownTerminalRenderer.cs         # NEW: Markdig → AnsiConsole adapter
      TerminalCapabilities.cs             # NEW: thin wrapper around AnsiConsole.Profile
tests/
  Buildout.UnitTests/
    Markdown/
      Blocks/                             # NEW: one test file per block converter
        ParagraphConverterTests.cs
        Heading1ConverterTests.cs
        Heading2ConverterTests.cs
        Heading3ConverterTests.cs
        BulletedListItemConverterTests.cs
        NumberedListItemConverterTests.cs
        ToDoConverterTests.cs
        CodeConverterTests.cs
        QuoteConverterTests.cs
        DividerConverterTests.cs
      Mentions/                           # NEW: one test file per mention converter
        PageMentionConverterTests.cs
        DatabaseMentionConverterTests.cs
        UserMentionConverterTests.cs
        DateMentionConverterTests.cs
      BlockToMarkdownRegistryTests.cs     # NEW: dispatch + duplicate-registration check
      MentionToMarkdownRegistryTests.cs   # NEW: same shape for mentions
      InlineRendererTests.cs              # NEW: annotation stacking + mention dispatch
      UnsupportedBlockHandlerTests.cs     # NEW: placeholder format + parse-survival
      PageMarkdownRendererTests.cs        # NEW: title H1, pagination, determinism
    Buildin/
      BotBuildinClientTests.cs            # MODIFIED: cover the three mapping fixes
  Buildout.IntegrationTests/
    Cli/
      GetCommandTests.cs                  # NEW: TTY vs non-TTY, exit codes, stderr
    Mcp/
      PageResourceTests.cs                # NEW: list, read, error shape
    Llm/
      PageReadingLlmTests.cs              # NEW: cheap-LLM test, skipped without key
```

**Structure Decision**: Same five-project .NET 10 solution as feature 001. New
work concentrates in three places: a new `Markdown/` subtree under
`Buildout.Core`, a `Resources/` subtree under `Buildout.Mcp`, and a `Commands/` +
`Rendering/` pair under `Buildout.Cli`. The three scaffold-bug fixes in
`BotBuildinClient` and the additive model changes (`Page.Title`, `RichText.Mention`)
are the only changes to feature-001 surface; the changes are source-compatible
because they add fields rather than rename or remove. The Kiota-generated
namespace (`Buildout.Core.Buildin.Generated.*`) is not touched.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
