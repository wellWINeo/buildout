# Implementation Plan: Tree Command

**Branch**: `015-tree-command` | **Date**: 2026-05-28 | **Spec**: `specs/015-tree-command/spec.md`
**Input**: Feature specification from `/specs/015-tree-command/spec.md`

## Summary

Add a `tree` operation to `Buildout.Core` that, given a page or database UUID, traverses its descendant pages and databases (skipping content blocks) and returns a hierarchical model. Two renderers — ASCII (Unix `tree`-style box-drawing with `[Name](<URL>)` markdown links) and JSON (recursive `{name, uri, children}`) — share that model. The traversal is exposed identically on both presentation surfaces: a Spectre.Console CLI command `buildout-cli tree <id> [--format ascii|json] [--depth N]` and an MCP tool `tree` with the same parameters. Format selection is parameter-only on both surfaces. Depth is bounded `[1, 7]` with a default of 3; the bound is rejected with a clear error. A failed root read aborts the command; a failed descendant read renders that node as `(unavailable)` and traversal continues. Traversal reuses the existing buildin client and benefits from the existing read cache (feature 012) for repeated page reads.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (SDK-style projects, `net10.0`), nullable reference types + warnings-as-errors enabled solution-wide
**Primary Dependencies**: `IBuildinClient` (existing core abstraction); `Spectre.Console.Cli` for the CLI command; `ModelContextProtocol` SDK for the MCP tool; `System.Text.Json` for JSON output and tool serialization; `Microsoft.Extensions.Logging.Abstractions` for descendant-failure logging
**Storage**: N/A — read-only feature, no persistence
**Testing**: xUnit v3 + NSubstitute (unit tests against a mocked `IBuildinClient`); cheap-LLM MCP integration test per Principle IV and the MCP-tool-change merge gate
**Target Platform**: Cross-platform (.NET 10 runtime); CLI binary and MCP server (stdio + http) — same surfaces already supported by the project
**Project Type**: Feature added to existing core library + both presentation projects (CLI command + MCP tool); no new project
**Performance Goals**: SC-003 — under 3 seconds for any tree with up to 100 descendants within the requested depth on a warm workspace
**Constraints**: Hard depth ceiling of 7; only `child_page` and `child_database` blocks contribute to traversal; only buildin.ai web URLs (the `Url` field returned by the API) appear in output; no client-side reordering of siblings; markdown-significant characters in names must not break the link syntax
**Scale/Scope**: Read-only operation. A wide root can fan out to hundreds of children; cache (feature 012) absorbs repeated reads but cannot reduce first-touch fan-out — that is accepted (see Assumptions in spec)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Core/Presentation Separation | PASS | Traversal logic, the `TreeNode` model, and both renderers live in `Buildout.Core/PageTree/`. `Buildout.Cli` adds a thin `TreeCommand` that calls the service and writes the rendered string; `Buildout.Mcp` adds a thin `TreeToolHandler` that does the same. Neither presentation calls buildin.ai or formats output. |
| II. LLM-Friendly Output Fidelity | PASS | Output is deterministic CommonMark links + box-drawing characters (ASCII format) or canonical JSON (JSON format). No internal buildin IDs or noise leak into either format. |
| III. Bidirectional Round-Trip Testing | N/A | No block ↔ Markdown conversion is added or modified. The tree operation reads `Page`, `Database`, and `ChildPageBlock`/`ChildDatabaseBlock` metadata only — it does not render block content. |
| IV. Test-First Discipline | PASS | Red-Green-Refactor: unit tests for the traversal (mocked client), each renderer, name escaping, untitled/unavailable placeholders, depth validation, cycle detection. Cheap-LLM integration test for the MCP tool per the MCP-tool-change merge gate. All tests run against a mocked buildin client — no real network. |
| V. Buildin API Abstraction | PASS | All buildin calls go through `IBuildinClient` (`GetPageAsync`, `GetDatabaseAsync`, `GetBlockChildrenAsync`, `QueryDatabaseAsync`). No presentation project takes a direct dependency on the bot API or any specific transport. |
| VI. Non-Destructive Editing | N/A | The operation is read-only; no block or page is created, updated, deleted, or restored. |
| VII. Dual-Channel Configuration | N/A | No new user-facing configurable option is introduced. `depth` is a per-call parameter with a hard-coded default (3) and hard-coded valid range (1–7), not a process-level configuration. `format` is likewise per-call. No new entry in `~/.config/buildout/config.json` or `Buildout__*` env vars; `docs/configuration.md` is unchanged. |
| VIII. Skills & Prompts Parity | PASS | A new skill file `Buildout.Cli/Skills/tree.md` is added and referenced from `SKILL.md`. The MCP `tree` tool's `[Description]` attribute fully captures its contract (3 parameters, format/depth semantics, error shape); behavior is not complex enough to warrant a separate named prompt under Principle VIII's "complex enough" clause. If future enhancements (e.g. filters, multiple roots) raise complexity, a `tree.md` prompt under `Buildout.Mcp/Prompts/` will be added in that PR. |

**Gate result**: PASS — no violations. All non-N/A principles satisfied by the design.

## Project Structure

### Documentation (this feature)

```text
specs/015-tree-command/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (CLI + MCP + service interface contracts)
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    PageTree/
      IPageTreeService.cs              # Interface: BuildAsync(targetId, depth, ct) -> TreeNode
      PageTreeService.cs               # Implementation: traverses via IBuildinClient (page → block children → child_page/child_database; database → QueryDatabaseAsync); enforces depth; cycle guard; descendant-failure handling
      TreeNode.cs                      # Immutable record: Name, Uri, Children
      TreeNodeKind.cs                  # Enum (Page, Database, Unavailable) — internal, used only for logging/diagnostics
      TreeDepth.cs                     # Static helpers: Min = 1, Max = 7, Default = 3, Validate(int)
      TreeFormat.cs                    # Enum: Ascii, Json — used by both CLI and MCP for parameter validation
      Rendering/
        ITreeRenderer.cs               # Interface: Render(TreeNode) -> string
        AsciiTreeRenderer.cs           # Unix tree-style; markdown link escaping per FR-015
        JsonTreeRenderer.cs            # System.Text.Json — canonical {name, uri, children} shape
      Errors/
        TreeDepthOutOfRangeException.cs # Thrown by service on invalid depth
        TreeRootNotFoundException.cs    # Thrown on root read failure (wraps BuildinApiException)
        TreeCycleDetectedException.cs   # Thrown if a repeated node is seen (defensive)
    DependencyInjection/
      ServiceCollectionExtensions.cs    # MODIFIED: register IPageTreeService + both renderers + dict<TreeFormat, ITreeRenderer>
  Buildout.Cli/
    Commands/
      TreeCommand.cs                    # NEW: AsyncCommand<TreeCommand.Settings>; resolves IPageTreeService + format-keyed ITreeRenderer; maps exceptions to exit codes
      TreeSettings.cs                   # NEW: <page_id> argument, --format (default ascii), --depth (default 3)
    Skills/
      SKILL.md                          # MODIFIED: add `tree` to the Quick Reference table and Reference Files list
      tree.md                           # NEW: skill reference per Agent Skills spec (syntax, options, examples, exit codes)
      SkillResourceLoader.cs            # UNCHANGED (existing loader picks up the new embedded resource via .csproj glob)
    Buildout.Cli.csproj                 # MODIFIED only if the Skills/*.md embedded-resource pattern is not already globbed (verify in Phase 1)
    Program.cs                          # MODIFIED: register TreeCommand under "tree"
  Buildout.Mcp/
    Tools/
      TreeToolHandler.cs                # NEW: [McpServerTool(Name = "tree")]; parameters page_id, format ("ascii"|"json"), depth (1–7); maps exceptions to McpProtocolException; emits BuildoutMeter tags consistent with other tools
    Program.cs                          # MODIFIED: register `.WithTools<TreeToolHandler>()`
tests/
  Buildout.UnitTests/
    PageTree/
      PageTreeServiceTests.cs           # Mocked IBuildinClient — depth=1/3/7, mixed page+database children, database root, untitled, unavailable descendant, cycle detection, root failure aborts, sibling order preserved, depth out-of-range rejected, only child_page/child_database considered (paragraphs/lists ignored)
      AsciiTreeRendererTests.cs         # Connector glyphs (├──, └──, │, four-space gutter); single-node tree (no connectors); markdown-link escaping for ], [, <, >, \, ); untitled rendering; unavailable rendering
      JsonTreeRendererTests.cs          # Canonical shape; leaves have children: []; recursive nesting; verbatim names; UTF-8; deterministic property order
      TreeDepthTests.cs                 # Validate() boundaries
    Cli/
      TreeCommandTests.cs               # Exit codes for invalid usage / not-found / auth / transport / unexpected; stdout vs stderr; default format ascii; --format json switches renderer
  Buildout.IntegrationTests/
    Mcp/
      TreeToolTests.cs                  # End-to-end MCP call against mocked buildin; verifies tool description, parameter schema, error shape, and both formats; cheap-LLM exercise satisfies the MCP-tool-change merge gate
```

**Structure Decision**: Follows the existing project layout. All traversal and rendering live in `Buildout.Core/PageTree/` (Principle I). `Buildout.Cli` and `Buildout.Mcp` each gain a single thin entry point that translates transport concerns into a single `IPageTreeService` call and then picks the right renderer from a `TreeFormat`-keyed dictionary. Both presentations share the same renderers, guaranteeing identical output for identical inputs (SC-004).

## Complexity Tracking

> No violations — table intentionally empty.
