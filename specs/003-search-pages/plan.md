# Implementation Plan: Page Search

**Branch**: `003-search-pages` | **Date**: 2026-05-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-search-pages/spec.md`

## Summary

Deliver page search across both buildout surfaces. `Buildout.Core` gains a
`PageSearchService` that takes a non-empty query and an optional page-scope
ID, calls `IBuildinClient.SearchPagesAsync` (the typed `/v1/search` endpoint
from feature 001), paginates `next_cursor` to exhaustion, optionally
post-filters matches to descendants of the scope page via an ancestor walk,
excludes archived pages by default, and returns an ordered list of
`SearchMatch` records. A second core service `SearchResultFormatter`
serialises a `SearchMatch` list to the canonical line-oriented body shared
byte-for-byte by both surfaces.

`Buildout.Cli` adds `buildout search <query> [--page <page_id>]` which
prints that body to stdout — raw when stdout is non-TTY, styled (via
`Spectre.Console.Table`) when stdout is a styled terminal. `Buildout.Mcp`
adds a `search` tool whose result content is the same body returned as a
single text block. Failure-class exit codes and MCP error mapping reuse
feature 002's taxonomy unchanged.

This feature also closes one latent gap in the feature-001 scaffold:
`BotBuildinClient.MapV1SearchResponse` currently maps only `Id`,
`CreatedAt`, `LastEditedAt`, and `Archived` — `Title` and `Parent` are
dropped. Both are required for this feature (titles for display,
`Parent` for the ancestor-walk filter), and both are pure additive
mapping fixes behind the existing `IBuildinClient` interface; no
presentation-side change.

The cheap-LLM integration test introduced in feature 002 is extended with
a sibling `LlmCanFindAndReadPage` that drives `search` → `buildin://`
end-to-end, validating that the line-oriented search body is consumable
by the same LLM that already consumes the Markdown read body.

## Technical Context

**Language/Version**: C# / .NET 10. Already in place from features 001 / 002.

**Primary Dependencies (additions in this feature)**:

- None. `Spectre.Console` (already referenced via `Spectre.Console.Cli`) is
  used for the styled CLI table.
- `ModelContextProtocol` SDK is already wired (it served the resource
  template in feature 002); this feature is its first **tool** usage.
- `Anthropic.SDK` is already referenced by `Buildout.IntegrationTests`; the
  search↔read LLM test reuses it.

No new dependencies in `Buildout.Core` itself; `SearchResultFormatter`
emits a plain `string` and uses no third-party library.

**Storage**: N/A — no persistence in this feature.

**Testing**: xUnit + NSubstitute (already in place). Unit tests mock
`IBuildinClient` directly. Integration tests for CLI / MCP inject a fake
`IBuildinClient` into the host process. The cheap-LLM test runs against
real Anthropic Haiku and is skipped without `ANTHROPIC_API_KEY`.

**Target Platform**: cross-platform .NET 10 (macOS, Linux, Windows).

**Project Type**: same five-project .NET solution as features 001 / 002.

**Performance Goals (from spec SCs)**:

- Full feature test suite — including the extended cheap-LLM test if its
  key is present — completes in **well under 30 s** on a developer laptop
  with no buildin network (SC-007).
- A single small search (≤ 50 matches, no scope filter) returns in well
  under one second locally (no spec target; not regression-tracked here).

**Constraints**:

- No outbound HTTPS to `api.buildin.ai` from any test (Constitution
  Principle IV; spec FR-017).
- The plain-mode CLI body MUST equal the MCP tool result body byte-for-byte
  (spec FR-014, SC-003) — both surfaces share the single string from
  `SearchResultFormatter`.
- The CLI MUST emit zero terminal escape codes when stdout is not a TTY
  (spec FR-010).
- Buildin-internal noise MUST NOT appear in the rendered output (spec
  FR-006; constitution Principle II).
- Empty / whitespace-only queries MUST be rejected before any buildin call
  is made (spec FR-008, SC-006).
- Generated client output (`src/Buildout.Core/Buildin/Generated/`) remains
  hand-edit-free; all changes are in hand-written code only.

**Scale/Scope**:

- 1 new core service (`PageSearchService`) + 1 new core formatter
  (`SearchResultFormatter`) + 1 small ancestor-walk helper.
- 1 modified `BotBuildinClient` mapper (`MapV1SearchResponse` populates
  `Title` + `Parent`).
- 1 new CLI command (`SearchCommand`).
- 1 new MCP tool handler (`SearchToolHandler`).
- ~5 new unit-test files + 2 new integration-test files (CLI, MCP) + 1
  extended LLM test file.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|---|---|---|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | `PageSearchService` and `SearchResultFormatter` live in `Buildout.Core/Search/`. `Buildout.Mcp` calls `ISearchService.SearchAsync(...)` and wraps the formatted body in an MCP text content. `Buildout.Cli` calls the same service, then optionally pipes the body through a `Spectre.Console.Table` for styled mode. Neither presentation project parses buildin objects, calls the buildin search endpoint directly, or implements result formatting. |
| II | LLM-Friendly Output Fidelity | ✅ PASS | The body is a stable, line-oriented format — three tab-separated columns per match: `<page_id>\t<object_type>\t<title>`. Buildin-internal IDs other than the page ID itself never appear; archived pages are filtered out by default; raw property blobs are not exposed. The format is documented in `contracts/search-result-format.md` and is byte-stable for a fixed buildin response (FR-014). |
| III | Bidirectional Round-Trip Testing | ⚠️ PARTIAL — JUSTIFIED | Same carve-out as feature 002. Search is a read operation; there is no symmetric write counterpart and no input format the writing tool would later need to round-trip. The constitution's "for input formats accepted by the writing tool" clause applies. No exception requested. |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | `/speckit-tasks` will produce failing tests first for: empty-query rejection at both surfaces, pagination loop drains `NextCursor`, archived filter, ancestor-walk filter (positive + negative + missing-parent + cycle defence), the formatter's exact line shape including untitled fallback and tab escaping, the CLI's TTY-detection branch, the CLI's exit-code mapping, the MCP tool's input schema and error mapping, and the `search → buildin://` LLM chain. The cheap-LLM test runs only when `ANTHROPIC_API_KEY` is set; absence skips that single test, never silences any other. |
| V | Buildin API Abstraction | ✅ PASS | The service depends only on `IBuildinClient` (already public surface; `SearchPagesAsync` already declared on the interface from feature 001). The mapping fix to `MapV1SearchResponse` is behind that interface. A future `UserApiBuildinClient` slots in unchanged. |
| VI | Non-Destructive Editing | ➖ N/A | Read-only feature. |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | All new code respects `Directory.Build.props`. |
| `ModelContextProtocol` SDK | ✅ | First **tool** wiring of the SDK; reuses the host registration pattern feature 002 established for the resource template. |
| `Spectre.Console.Cli` | ✅ | Adds one new command (`search`) registered through `CommandApp` exactly as feature 002 added `get`. |
| Solution layout (5 projects) | ✅ | No new projects. |
| Bot-API as one impl of `IBuildinClient`; User API path open | ✅ | Service depends only on the interface. |
| Secrets from env/config; no committed tokens | ✅ | Buildin token continues to be supplied via configuration; the LLM test reads `ANTHROPIC_API_KEY` from the environment and skips if absent. |

| Out-of-scope item | Respected? |
|---|---|
| Admin dashboard | ✅ Not added. |
| Managed/enterprise deployment | ✅ Not added. |
| Multi-tenant hosting | ✅ Not added. |
| CI configuration | ✅ Not added in this feature. |
| Re-ranking / scoring / sort | ✅ Explicitly deferred per spec Assumptions. |
| Streaming / caching / max-result caps | ✅ Explicitly deferred per spec FR-002. |
| Including archived pages | ✅ Default-excluded; opt-in deferred. |

**Gate result (pre-Phase 0)**: PASS — no unjustified violations.
Principle III's partial coverage is the constitution's own carve-out for
read-only work.

**Re-check after Phase 1 design**: PASS — no new violations introduced.

- The service's public surface (`ISearchService.SearchAsync(query,
  pageId?, ct)`) is documented in `contracts/search-service.md` and is
  the single seam between Core and presentation. Both `Buildout.Mcp` and
  `Buildout.Cli` consume only this surface (plus the small
  `SearchResultFormatter`) for search.
- The MCP tool contract (`contracts/mcp-search-tool.md`) declares the
  `search` tool's input schema and result shape with no buildin domain
  logic in `Buildout.Mcp`; the handler is a thin wrapper.
- The CLI command contract (`contracts/cli-search-command.md`) declares
  the `Spectre.Console.Cli` command shape; the styled-mode renderer is a
  small `Spectre.Console.Table` adapter scoped to the formatter's body.
- The data-model document records the additive mapping fix to
  `Page` population inside `MapV1SearchResponse` — pure addition, no
  removed fields.
- Compatibility surfaces are complete: every `V1SearchPageResult` field
  the service depends on (`Id`, `Object`, `Properties.Title`, `Parent`,
  `Archived`) is mapped; the service treats any other field as
  irrelevant.

`Complexity Tracking` table remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/003-search-pages/
├── plan.md                      # This file (/speckit-plan output)
├── research.md                  # Phase 0 output
├── data-model.md                # Phase 1 output
├── quickstart.md                # Phase 1 output
├── contracts/                   # Phase 1 output
│   ├── search-service.md
│   ├── search-result-format.md
│   ├── cli-search-command.md
│   └── mcp-search-tool.md
├── checklists/
│   └── requirements.md          # spec quality checklist (already created)
└── tasks.md                     # Phase 2 (/speckit-tasks output — not in this command)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    Buildin/
      BotBuildinClient.cs                      # MODIFIED: MapV1SearchResponse fills Title + Parent
    Search/
      ISearchService.cs                        # NEW: public seam
      SearchService.cs                         # NEW: orchestrator (validate + paginate + filter)
      SearchMatch.cs                           # NEW: per-match result record
      ISearchResultFormatter.cs                # NEW: shared CLI/MCP body format contract
      SearchResultFormatter.cs                 # NEW: implements the line-oriented body
      Internal/
        AncestorScopeFilter.cs                 # NEW: filters matches to descendants of scope
        TitleRenderer.cs                       # NEW: rich-text-list → plain title string
    DependencyInjection/
      ServiceCollectionExtensions.cs           # MODIFIED: register search service + formatter
  Buildout.Mcp/
    Program.cs                                  # MODIFIED: register WithTools<SearchToolHandler>
    Tools/
      SearchToolHandler.cs                      # NEW: search MCP tool
  Buildout.Cli/
    Program.cs                                  # MODIFIED: register SearchCommand with CommandApp
    Commands/
      SearchCommand.cs                          # NEW: `buildout search <query> [--page <page_id>]`
    Rendering/
      SearchResultStyledRenderer.cs             # NEW: line body → Spectre.Console.Table for TTY mode
tests/
  Buildout.UnitTests/
    Search/                                     # NEW
      SearchServiceTests.cs                     # validation + pagination loop + archived exclusion
      AncestorScopeFilterTests.cs               # filter semantics (incl. cycle defence + missing parent)
      SearchResultFormatterTests.cs             # exact line shape, untitled fallback, byte-stability
      TitleRendererTests.cs                     # rich-text → plain title
      DependencyInjectionTests.cs               # search seams resolve from DI
    Buildin/
      BotBuildinClientTests.cs                  # MODIFIED: cover MapV1SearchResponse Title + Parent
  Buildout.IntegrationTests/
    Cli/
      SearchCommandTests.cs                    # NEW: TTY vs non-TTY, exit codes, scope flag
    Mcp/
      SearchToolTests.cs                       # NEW: list-tools, invoke, error shape, byte-equality
    Llm/
      PageReadingLlmTests.cs                   # MODIFIED: add LlmCanFindAndReadPage sibling test
```

**Structure Decision**: Same five-project .NET 10 solution as features
001 / 002. New work concentrates in three places: a new `Search/` subtree
under `Buildout.Core` (parallel to `Markdown/` from feature 002), a
`Tools/` subtree under `Buildout.Mcp` (parallel to `Resources/` from
feature 002), and a new `SearchCommand` + small styled renderer pair
under `Buildout.Cli` (parallel to `GetCommand` from feature 002). The
single mapping fix in `BotBuildinClient.MapV1SearchResponse` is the only
change to feature-001/002 surface; the change is source-compatible
because it only fills additional fields on existing `Page` records that
were previously left as defaults. The Kiota-generated namespace
(`Buildout.Core.Buildin.Generated.*`) is not touched.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
