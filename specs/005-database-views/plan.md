# Implementation Plan: Database Views (Read-Only)

**Branch**: `005-database-views` | **Date**: 2026-05-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/005-database-views/spec.md`

## Summary

Render any buildin database, in the user's terminal or to an MCP client,
in one of six client-side view styles (`table`, `board`, `gallery`,
`list`, `calendar`, `timeline`). Buildin's API exposes no server-side
view object (see `openapi.json`), so a "view" here is purely a
client-side render of the rows returned by the existing
`POST /v1/databases/{id}/query` endpoint, paginated to exhaustion
before a single character of output is emitted.

A new core service, `IDatabaseViewRenderer`, owns the rendering for all
six styles. CLI and MCP each add a thin adapter — a `DbViewCommand`
under `Buildout.Cli` and a `DatabaseViewToolHandler` under
`Buildout.Mcp` — that resolves arguments, calls the renderer, and
selects TTY styling for CLI only. The MCP body and the CLI plain-mode
output are byte-identical, mirroring the contract already established
by `search`.

The same renderer is also reused by the existing page-read pipeline
(`PageMarkdownRenderer`) to expand `child_database` blocks inline as
table-style views (spec User Story 4 / FR-016–FR-021). A new
`ChildDatabaseBlock` model is added to the existing block hierarchy,
and a new `ChildDatabaseConverter` (an `IBlockToMarkdownConverter`)
substitutes the embedded database's rendered table at the block's
position. Failures to expand never abort the page render — a
single-line placeholder is emitted instead.

No new buildin client methods, no new production dependencies. Tests
run against the existing WireMock-based buildin fixture introduced by
feature 004.

## Technical Context

**Language/Version**: C# / .NET 10. Inherited from features 001–004.

**Primary Dependencies**: No additions. Existing:

- `Spectre.Console.Cli` for the new CLI command (mandated by
  constitution).
- `Spectre.Console` for TTY styling of the rendered view.
- `ModelContextProtocol` SDK for the new MCP tool.
- `Microsoft.Extensions.DependencyInjection` for the renderer
  registration alongside existing core services.

**Storage**: N/A. Read-only feature; no persistence.

**Testing**: xUnit + NSubstitute (already in place). New tests:

- Unit tests in `Buildout.UnitTests` for `DatabaseViewRenderer` per
  style, per property type, and per edge case, each driven by hand-
  built `Database` + `QueryDatabaseResult` fixtures.
- Integration tests in `Buildout.IntegrationTests` exercising
  `DbViewCommand` and `DatabaseViewToolHandler` against the
  WireMock-based buildin fixture (feature 004) with new stubs for
  `GET /v1/databases/{id}` and `POST /v1/databases/{id}/query` —
  including a multi-page query stub to exercise cursor follow-through.

**Target Platform**: Same as existing surfaces — .NET 10 console
processes shipped as framework-dependent single-file artifacts via the
existing CI/CD pipeline (feature 004). No new platform.

**Project Type**: Internal feature touching three of the existing five
projects (`Buildout.Core`, `Buildout.Cli`, `Buildout.Mcp`) plus their
two test projects. No new projects.

**Performance Goals**:

- Render a database with up to 1000 rows in under 2 seconds excluding
  network time, on the CI runner used by feature 004 (verifiable via
  a unit test that times the renderer against an in-memory 1000-row
  fixture).
- No additional buildin round-trips beyond `GET database` (1 call) +
  `POST query` (N calls until `has_more=false`); no per-row fetches.

**Constraints**:

- No real buildin network calls during tests (Constitution IV;
  spec FR-014).
- CLI plain-mode output and MCP tool body MUST be byte-identical
  (spec FR-012; verified by an integration test).
- Rendered output legible in 80 columns: per-cell character budget,
  table-to-stacked fallback, side-by-side board cap of three groups
  (spec FR-004).
- Read-only: only `GET database` and `POST query` may appear in any
  rendering code path (spec SC-007; verified by a contract test that
  asserts the WireMock stubs the renderer triggers are exactly that
  set).

**Scale/Scope**:

- 1 new core service interface + 1 implementation (renderer).
- 6 style strategies (one per view style) in core, behind the renderer.
- 1 new CLI command (`db view`).
- 1 new MCP tool (`database_view`) on a new handler class.
- 1 new block model (`ChildDatabaseBlock`) added to
  `Buildout.Core/Buildin/Models/Block.cs`.
- 1 new converter (`ChildDatabaseConverter : IBlockToMarkdownConverter`)
  registered alongside the existing block converters.
- ~5 new unit-test files (one per style + a property-formatter file)
  plus 1 for `ChildDatabaseConverter`.
- ~2 new integration-test files (CLI command + MCP tool, sharing the
  existing WireMock fixture) plus 1 page-read integration test
  covering inline expansion + the no-regression case for pages
  without `child_database` blocks.
- 1 new `BuildinStubs` extension method for paginated
  `database/query` stubs (and a stub for a page payload that
  contains a `child_database` block).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|---|---|---|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | All rendering lives in `Buildout.Core` behind `IDatabaseViewRenderer`. CLI and MCP adapters take a database id + parameters, call the renderer, and pick TTY styling on or off. Neither adapter parses property values, formats cells, or talks to buildin directly. The new `ChildDatabaseConverter` also lives in core and reuses the same renderer. |
| II | LLM-Friendly Output Fidelity | ✅ PASS | Output is deterministic. Table style emits a GFM pipe-table (the GFM extension is permitted by Constitution II) and list style emits CommonMark bullets; the four non-table styles (board, gallery, calendar, timeline) emit ASCII-only structured text with documented per-style shape. No buildin-internal IDs leak unless explicitly opted in (which this feature does not). Per-property-type formatting rules are enumerated in `data-model.md` and exercised by tests. The inline-expansion path emits the same GFM pipe-table inside the page's existing markdown body, so page-read output remains a valid GFM document. |
| III | Bidirectional Round-Trip Testing | ➖ N/A | This feature does not edit blocks or convert Markdown → blocks. Round-trip applies to writing tools; this is read-only rendering. |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | New behavior ships with failing tests written first: unit tests per style/property type, integration tests against WireMock for both surfaces. Tests use the existing WireMock fixture; no test depends on real buildin. No tests are skipped or deleted. |
| V | Buildin API Abstraction | ✅ PASS | Renderer consumes the existing `IBuildinClient` interface (`GetDatabaseAsync`, `QueryDatabaseAsync`). No new methods, no Bot-API specifics, no direct HTTP. A future User API client implementation would slot in without changes here. |
| VI | Non-Destructive Editing | ➖ N/A | Read-only on both surfaces, including inline expansion. The renderer and the new converter never call a write/edit/append/delete buildin method. Asserted by SC-007 / contract test, which now also covers the page-read code path triggered by `child_database` blocks. |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | New code respects `Directory.Build.props`. |
| `ModelContextProtocol` SDK | ✅ | New tool registered via the existing `[McpServerTool]` attribute pattern. |
| `Spectre.Console.Cli` | ✅ | New command registered via the existing `config.AddCommand<T>(name)` pattern. |
| Solution layout (5 projects) | ✅ | No new projects; only files added under existing ones. |
| Bot API as one impl of `IBuildinClient` | ✅ | Renderer uses the interface only. |
| Secrets from env/config | ✅ | No new secrets introduced. |

| Out-of-scope item | Respected? |
|---|---|
| Admin dashboard | ✅ Not added. |
| Managed/enterprise deployment | ✅ Not added. |
| Multi-tenant hosting | ✅ Not added. |
| New buildin client methods or auth modes | ✅ None added. |
| Block writes / page writes / database mutations | ✅ Renderer is read-only. |

**Gate result (pre-Phase 0)**: PASS — no unjustified violations.

**Re-check after Phase 1 design**: PASS — Phase 1 design (data-model,
contracts, quickstart) preserves the gates above. The renderer
interface stays in core; CLI/MCP adapters stay thin; the only
buildin endpoints touched are `GET database` and `POST query`.

`Complexity Tracking` table remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/005-database-views/
├── plan.md                 # This file (/speckit-plan output)
├── spec.md                 # /speckit-specify output
├── design-sketches.md      # Companion to spec — concrete style renderings
├── research.md             # Phase 0 output (this command)
├── data-model.md           # Phase 1 output (this command)
├── quickstart.md           # Phase 1 output (this command)
├── contracts/              # Phase 1 output
│   ├── core-renderer.md         # IDatabaseViewRenderer surface
│   ├── cli-command.md           # `db view` command surface
│   ├── mcp-tool.md              # `database_view` tool surface
│   └── buildin-endpoints.md     # WireMock stubs ↔ openapi.json
├── checklists/
│   └── requirements.md     # spec quality checklist (already created)
└── tasks.md                # Phase 2 (/speckit-tasks output — NOT this command)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    DatabaseViews/                                   # NEW directory
      IDatabaseViewRenderer.cs                       # NEW: renderer entry point
      DatabaseViewRenderer.cs                        # NEW: composes paginate + style
      DatabaseViewRequest.cs                         # NEW: validated render parameters
      DatabaseViewStyle.cs                           # NEW: enum (Table, Board, Gallery, List, Calendar, Timeline)
      Styles/
        IDatabaseViewStyle.cs                        # NEW: per-style strategy
        TableViewStyle.cs                            # NEW
        BoardViewStyle.cs                            # NEW
        GalleryViewStyle.cs                          # NEW
        ListViewStyle.cs                             # NEW
        CalendarViewStyle.cs                         # NEW
        TimelineViewStyle.cs                         # NEW
      Properties/
        IPropertyValueFormatter.cs                   # NEW: format a PropertyValue inline
        PropertyValueFormatter.cs                    # NEW: dispatch on subclass
      Rendering/
        CellBudget.cs                                # NEW: per-cell width + truncation rule
        DatabaseViewMetadataHeader.cs                # NEW: "# Title — style view" line
    Buildin/
      Models/
        Block.cs                                     # MODIFIED: add ChildDatabaseBlock subclass (discriminator "child_database")
    Markdown/
      Converters/
        ChildDatabaseConverter.cs                    # NEW: IBlockToMarkdownConverter that calls IDatabaseViewRenderer for child_database blocks
    DependencyInjection/
      ServiceCollectionExtensions.cs                 # MODIFIED: register renderer + style strategies + formatter + ChildDatabaseConverter

  Buildout.Cli/
    Commands/
      DbViewCommand.cs                               # NEW: Spectre.Console.Cli command "db view"
      DbViewSettings.cs                              # NEW: command settings (positional id + options)
    Program.cs                                       # MODIFIED: config.AddCommand<DbViewCommand>("db view") — see research R3 for command-name shape

  Buildout.Mcp/
    Tools/
      DatabaseViewToolHandler.cs                     # NEW: [McpServerTool(Name = "database_view")]
    Program.cs                                       # MODIFIED: .WithTools<DatabaseViewToolHandler>()

tests/
  Buildout.UnitTests/
    DatabaseViews/                                   # NEW directory
      DatabaseViewRendererTests.cs                   # NEW: orchestration (pagination, dispatch)
      Styles/
        TableViewStyleTests.cs                       # NEW
        BoardViewStyleTests.cs                       # NEW
        GalleryViewStyleTests.cs                     # NEW
        ListViewStyleTests.cs                        # NEW
        CalendarViewStyleTests.cs                    # NEW
        TimelineViewStyleTests.cs                    # NEW
      Properties/
        PropertyValueFormatterTests.cs               # NEW: 14 PropertyValue subtypes × edge cases

  Buildout.IntegrationTests/
    Buildin/
      BuildinStubs.cs                                # MODIFIED: add RegisterGetDatabase + RegisterQueryDatabase (multi-page)
    Cli/
      DbViewCommandTests.cs                          # NEW: end-to-end CLI through WireMock
      GetCommandChildDatabaseTests.cs                # NEW: page-read with child_database block expands inline
    Mcp/
      DatabaseViewToolTests.cs                       # NEW: end-to-end MCP through WireMock
      PageResourceChildDatabaseTests.cs              # NEW: MCP page resource expands child_database inline
    Cross/
      DatabaseViewParityTests.cs                     # NEW: byte-identity CLI plain ↔ MCP body
      ChildDatabasePlaceholderTests.cs               # NEW: 404/401/transport on embedded db → placeholder, page still renders
```

**Structure Decision**: All new production code lives under existing
projects, organised under a new `DatabaseViews/` namespace in
`Buildout.Core` so the rendering domain is self-contained and easy to
move out later if it grows. The CLI and MCP additions follow the
exact patterns already used by `GetCommand` / `PageResourceHandler` and
`SearchCommand` / `SearchToolHandler`. No new test project; integration
tests piggyback on the existing WireMock fixture from feature 004.

## Phase 0: Research (output: research.md)

The following items were unknown at the start of `/speckit-plan` and
are resolved in `research.md`:

- **R1 – Pagination strategy**: should the renderer or the buildin
  client own the cursor loop? *(Decision: renderer — the existing
  client returns a single `QueryDatabaseResult` with `HasMore` /
  `NextCursor`; centralising the loop in the renderer keeps the
  interface unchanged and lets the renderer surface its own
  transport-error semantics.)*
- **R2 – Plain-output rendering primitive**: do we use any
  Spectre.Console rendering helpers, or hand-build strings? *(Decision:
  hand-build strings via `StringBuilder` for the plain output; reuse
  `MarkdownTerminalRenderer` only for table style in TTY mode. Avoids
  Spectre rendering escape codes leaking into the byte-identical
  body.)*
- **R3 – Command name shape**: `db view <id>` vs `database view <id>`
  vs flat `db-view <id>`. *(Decision: a single command `db view`
  registered as a two-word command in Spectre.Console.Cli, matching
  the established noun-then-verb shape and leaving room for future
  `db query`, `db schema` commands without renaming.)*
- **R4 – Property formatting per type**: short rules for each of the
  14 `PropertyValue` subclasses, including which render as a
  placeholder (`[N files]`, `[rollup]`).
- **R5 – Truncation/budget numbers**: per-cell character budget,
  table-stacked threshold, side-by-side board cap. *(Decisions: 24
  chars/cell, switch to stacked at >6 columns or summed budget >80
  cols, board cap 3 non-empty groups.)*
- **R6 – Group-by typing**: which property types are valid
  `--group-by` targets for board view? *(Decision: `select`,
  `multi-select` (single combined-label group per row),
  `checkbox`. Buildin "status" columns are exposed as `select` and
  are therefore valid via that rule.)*
- **R7 – Date typing for calendar/timeline**: which property types
  are valid `--date-property` targets, and how is duration computed
  for timeline. *(Decision: `date`, `created_time`,
  `last_edited_time` accepted; timeline shows `start → end (Nd)`
  when end is set, `start (1d)` otherwise.)*
- **R8 – Inline expansion semantics**: heading style for the embedded
  database, fixed table style with no overrides, recursion depth = 1,
  placeholder format on failure, error isolation between
  `child_database` blocks. *(Decisions: dedicated
  `RenderInlineAsync` method on the renderer, `## <title>` heading,
  per-block try/catch in the converter, no bypass flag in v1.)*

## Phase 1: Design & Contracts

### data-model.md

Captures the in-memory shapes the renderer operates over (none of
these are new buildin entities):

- `DatabaseViewRequest` — validated input: database id, style,
  optional group-by property name, optional date property name.
- `DatabaseViewResult` — orchestration result: the database title,
  the schema, the fully paginated row list, and the rendered output
  string. Internal type; not exposed across the project boundary.
- Per-style intermediate shapes (e.g., `BoardGroup`, `CalendarBucket`,
  `TimelineBand`) — internal to each style strategy.
- `CellBudget` — width and truncation policy.
- A property-formatter contract that maps each of the 14
  `PropertyValue` subclasses to a single-line string.

### contracts/

Four contract documents define the surfaces this feature adds:

- **`core-renderer.md`** — `IDatabaseViewRenderer` surface:
  `Task<string> RenderAsync(DatabaseViewRequest request,
  CancellationToken)`, the validation rules it enforces (style
  enum membership, group-by/date-property names match the
  schema), and the error classes it surfaces
  (`BuildinApiException` propagated unchanged;
  `ArgumentException` for validation failures the adapters map to
  the validation-error surface).

- **`cli-command.md`** — `db view <database_id> [--style <style>]
  [--group-by <name>] [--date-property <name>]`. Documents how the
  command dispatches to the renderer, how it picks plain vs styled
  output via `TerminalCapabilities.IsStyledStdout`, the exit-code
  table reused from features 002/003, and the validation/error
  message wording.

- **`mcp-tool.md`** — `database_view` tool with arguments
  `database_id` (required), `style`, `group_by`, `date_property`
  (all optional). Documents the byte-identity contract with CLI
  plain output, the error-class mapping (404 → ResourceNotFound,
  401/403 → InternalError, transport → InternalError, generic →
  InternalError) reused unchanged from `PageResourceHandler` and
  `SearchToolHandler`.

- **`buildin-endpoints.md`** — the exhaustive list of buildin
  endpoints this feature touches: `GET /v1/databases/{id}` and
  `POST /v1/databases/{id}/query`. Documents the WireMock stub
  shapes the integration tests register and asserts that no other
  endpoint may be invoked by any rendering code path (the
  read-only contract test).

### quickstart.md

Three short paragraphs:

1. Run `dotnet run --project src/Buildout.Cli -- db view <database_id>`
   to render a database as a table.
2. Pass `--style board --group-by Status` (or other styles) to switch
   shape.
3. From an MCP client, call the `database_view` tool with the same
   arguments to obtain identical output.

### Agent context update

`CLAUDE.md` (project root) currently points to the
`004-cicd-pipeline` plan. Phase 1 updates it to reference
`specs/005-database-views/plan.md` between the
`<!-- SPECKIT START -->` / `<!-- SPECKIT END -->` markers if those
markers exist, otherwise replaces the existing
`Active feature plan` line in place.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
