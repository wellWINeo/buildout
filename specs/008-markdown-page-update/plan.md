# Implementation Plan: Markdown Page Update via Patch Operations

**Branch**: `008-markdown-page-update` | **Date**: 2026-05-16 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/008-markdown-page-update/spec.md`

## Summary

Add the first user-visible *edit* capability to buildout: update an existing buildin page via
semantic patch operations against an anchored Markdown projection, without ever exposing raw
block JSON to LLM callers.

A new core service, `IPageEditor`, owns the operation end-to-end for both the read side
(`FetchForEditAsync`) and the write side (`UpdateAsync`). The read side re-uses the existing
`PageMarkdownRenderer` fetch-and-recurse pipeline, then weaves `<!-- buildin:block:<id> -->`
anchor comments into the rendered Markdown and computes a CRC32 revision token over the result.
The write side re-fetches the current block tree, checks the caller's revision token, applies
the ordered operation list against an `AnchoredAst` (a `BlockSubtreeWithAnchor[]` in which each
node carries its original block ID), diffs the patched AST against the original tree, and issues
the minimal sequence of buildin write calls (`UpdateBlockAsync`, `DeleteBlockAsync`,
`AppendBlockChildrenAsync`) that reproduces the patched AST while preserving block IDs.

The CLI adds `--editing` / `--print` flags to the existing `buildout get` command and a new
`buildout update` command. The MCP server gains `get_page_markdown` and `update_page` tools.
No new buildin client methods are required — `GetPageAsync`, `GetBlockChildrenAsync`,
`UpdateBlockAsync`, `DeleteBlockAsync`, and `AppendBlockChildrenAsync` are all already on
`IBuildinClient`. No new NuGet dependencies are required — `System.IO.Hashing` (for CRC32)
is part of the .NET 10 shared framework.

## Technical Context

**Language/Version**: C# / .NET 10. Inherited from features 001–007.

**Primary Dependencies**: All existing. Two in-tree capabilities newly exercised:

- **`System.IO.Hashing.Crc32`** — `.NET 10` built-in (`System.IO.Hashing` assembly, no
  `PackageReference` required). Used for `RevisionTokenComputer`.
- **Markdig 1.1.3** — already in `Buildout.Core` from feature 006. The edit pipeline parses
  anchored Markdown with `Markdown.Parse(...)` to build the `AnchoredAst`. The inline parser
  already present in feature 006's authoring pipeline is reused.
- **`ModelContextProtocol` SDK** — already present in MCP. The two new tools follow the
  `Task<string>` return pattern used by `SearchToolHandler` and `DatabaseViewToolHandler`
  (auto-wrapped as `TextContentBlock` containing JSON).

**Storage**: N/A. The only state mutation is the edited buildin page itself.

**Testing**: xUnit v3 + NSubstitute, WireMock-based integration harness — all inherited.
New categories:
- Unit tests for `AnchoredMarkdownRenderer`, `AnchoredMarkdownParser`, `RevisionTokenComputer`,
  `PatchApplicator`, `Reconciler`, `PageEditor` orchestration.
- Round-trip edit tests: fetch → patch → reconciliation-summary match.
- Integration tests (CLI + MCP, all error classes, WireMock stubs for `UpdateBlockAsync` /
  `DeleteBlockAsync` additions).
- Cheap-LLM integration test extending the spec 006 chain.

**Target Platform**: Same as existing — .NET 10 console processes.

**Project Type**: Internal feature touching three existing projects (`Buildout.Core`,
`Buildout.Cli`, `Buildout.Mcp`) plus their two test projects. No new projects.

**Performance Goals**: Re-fetch + reconcile for a 200-block page completes under 3 s excluding
buildin network latency (WireMock fixture, zero injected latency). A dry-run is timing-identical
to a committing run minus the write calls.

**Constraints**:
- No real buildin network calls in tests (Constitution IV; FR-023).
- CRC32 computation must be deterministic across platforms and process restarts (FR-005).
- Large-delete threshold is `10` (configurable via `IOptions<PageEditorOptions>`).
- The `buildout get` command without `--editing` must remain byte-for-byte identical to the
  spec 002 behavior — the flag is the only switch.

**Scale/Scope**:
- 1 new core service interface + implementation (`IPageEditor` / `PageEditor`).
- 1 new anchored-Markdown renderer (extends the existing `PageMarkdownRenderer` pipeline).
- 1 new anchored-Markdown parser (Markdig-based; extracts `BlockSubtreeWithAnchor[]`).
- 1 new revision token computer (`System.IO.Hashing.Crc32`).
- 5 operation types in a `PatchApplicator`.
- 1 reconciler (`Reconciler`) that diffs `BlockSubtreeWithAnchor[]` against the original
  `BlockSubtree[]`.
- 1 new CLI command (`update`) + flag extensions to `get`.
- 2 new MCP tool handlers (`GetPageMarkdownToolHandler`, `UpdatePageToolHandler`).
- ~8 new unit-test files; ~6 new integration-test files; 1 cheap-LLM test extension.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|-----------|------------|-------|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | `IPageEditor` owns everything: anchored-Markdown rendering, CRC32 revision, operation application, AST diffing, buildin write calls, large-delete guard, dry-run path. `Buildout.Cli` and `Buildout.Mcp` resolve arguments, call `IPageEditor`, and translate results. Neither adapter parses Markdown, inspects block JSON, applies patches, or calls `IBuildinClient` directly. |
| II | LLM-Friendly Output Fidelity | ✅ PASS | Anchored Markdown is the spec 002 read output with anchor comments woven in; stripping the anchor comments yields byte-for-byte spec 002 output (SC-001). The reconciler preserves block IDs — and thus the comments, backlinks, and timestamps buildin attaches to them — for every block whose anchor, parent, and sibling position are unchanged, maximising semantic fidelity of the edit. |
| III | Bidirectional Round-Trip Testing | ✅ PASS | This feature adds the third round-trip direction: fetch-for-edit → patch → re-fetch. FR-025 / SC-010 require an LLM integration test that chains `get_page_markdown` → `update_page` → `get_page_markdown` and verifies both that the targeted block changed and that every other block's anchor ID is preserved. Unit tests assert anchor-stripped Markdown equals spec 002 output for all spec 002 golden fixtures (SC-001). |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | Tasks (Phase 2) are ordered red-first. Every renderer, parser, operation type, reconciler rule, CLI command, and MCP tool has a failing test before its implementation. No real buildin host. No tests skipped or disabled. |
| V | Buildin API Abstraction | ✅ PASS | `PageEditor` consumes the existing `IBuildinClient` interface: `GetPageAsync`, `GetBlockChildrenAsync`, `UpdateBlockAsync`, `DeleteBlockAsync`, `AppendBlockChildrenAsync`. No new methods added to `IBuildinClient`. A future User-API client slots in without changes here. |
| VI | Non-Destructive Editing | ✅ PASS | The `update` command name and the `update_page` MCP tool description (FR-026) explicitly name the destructive nature. The large-delete guard (FR-013) + `allow_large_delete` opt-in is the explicit-flag mechanism. The revision check (FR-015) and per-operation `patch.*` failure classes are the targeting mechanisms. The reorder rejection (FR-011) prevents silent block-ID destruction via position change. A contract test asserts no `CreatePageAsync` / `CreateDatabaseAsync` / `UpdatePageAsync` / `UpdateDatabaseAsync` calls are issued. |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | New code respects `Directory.Build.props`. |
| `ModelContextProtocol` SDK | ✅ | New tools declared with `[McpServerTool]` and `Task<string>` return type (auto-wrapped as `TextContentBlock` containing JSON). |
| `Spectre.Console.Cli` | ✅ | `update` registered via `config.AddCommand<UpdateCommand>("update")`; `get` flags extended via the existing `Settings` class. |
| Solution layout (5 projects) | ✅ | No new projects. |
| Bot API as one impl of `IBuildinClient` | ✅ | Editor uses the interface only. |
| Secrets from env/config | ✅ | No new secrets. |

**Gate result (pre-Phase 0)**: PASS — no violations.

**Re-check after Phase 1 design**: PASS — Phase 1 design preserves all gates.
`Complexity Tracking` table remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/008-markdown-page-update/
├── plan.md                  # This file (/speckit-plan output)
├── spec.md                  # /speckit-specify + /speckit-clarify output
├── research.md              # Phase 0 output (this command)
├── data-model.md            # Phase 1 output (this command)
├── quickstart.md            # Phase 1 output (this command)
├── contracts/               # Phase 1 output
│   ├── core-editor.md           # IPageEditor surface
│   ├── patch-operations.md      # 5 operation types, fields, error classes
│   ├── cli-update.md            # get --editing + update command surface
│   ├── mcp-edit.md              # get_page_markdown + update_page tool surfaces
│   ├── buildin-endpoints.md     # WireMock stubs ↔ IBuildinClient methods
│   └── observability.md         # Spec 007 integration: operation names + error_type values
├── checklists/
│   └── requirements.md      # Spec quality checklist (already created)
└── tasks.md                 # Phase 2 (/speckit-tasks output — NOT this command)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    Markdown/
      Editing/                                            # NEW namespace — edit direction
        IPageEditor.cs                                    # NEW: FetchForEditAsync + UpdateAsync
        PageEditor.cs                                     # NEW: orchestration implementation
        FetchForEditInput.cs                              # NEW: { PageId }
        AnchoredPageSnapshot.cs                           # NEW: { Markdown, Revision, UnknownBlockIds }
        UpdatePageInput.cs                                # NEW: { PageId, Revision, Operations[], DryRun, AllowLargeDelete }
        ReconciliationSummary.cs                          # NEW: { PreservedBlocks, NewBlocks, DeletedBlocks, UpdatedBlocks, AmbiguousMatches, NewRevision, PostEditMarkdown? }
        PatchRejectedException.cs                         # NEW: base class for all patch.* error classes
        PageEditorOptions.cs                              # NEW: LargeDeleteThreshold = 10
        PatchOperations/
          PatchOperation.cs                               # NEW: abstract record base
          ReplaceBlockOperation.cs                        # NEW: { Anchor, Markdown }
          ReplaceSectionOperation.cs                      # NEW: { Anchor, Markdown }
          SearchReplaceOperation.cs                       # NEW: { OldStr, NewStr }
          AppendSectionOperation.cs                       # NEW: { Anchor?, Markdown }
          InsertAfterBlockOperation.cs                    # NEW: { Anchor, Markdown }
        Internal/
          AnchoredMarkdownRenderer.cs                     # NEW: weaves anchor comments into spec-002 Markdown
          AnchoredMarkdownParser.cs                       # NEW: Markdig parse → BlockSubtreeWithAnchor[]
          BlockSubtreeWithAnchor.cs                       # NEW: BlockSubtreeWrite + AnchorId? + AnchorKind
          PatchApplicator.cs                              # NEW: applies ordered ops → BlockSubtreeWithAnchor[]
          Reconciler.cs                                   # NEW: diffs patched tree vs original → WriteOps[]
          RevisionTokenComputer.cs                        # NEW: CRC32 over UTF-8 bytes → 8-char lowercase hex
    DependencyInjection/
      ServiceCollectionExtensions.cs                      # MODIFIED: register IPageEditor, PageEditorOptions

  Buildout.Cli/
    Commands/
      GetCommand.cs                                       # MODIFIED: add --editing + --print flags
      UpdateCommand.cs                                    # NEW: buildout update
      UpdateSettings.cs                                   # NEW: --page, --revision, --ops, --dry-run, --allow-large-delete, --print
    Program.cs                                            # MODIFIED: config.AddCommand<UpdateCommand>("update")

  Buildout.Mcp/
    Tools/
      GetPageMarkdownToolHandler.cs                       # NEW: [McpServerTool(Name = "get_page_markdown")]
      UpdatePageToolHandler.cs                            # NEW: [McpServerTool(Name = "update_page")]
    Program.cs                                            # MODIFIED: .WithTools<GetPageMarkdownToolHandler, UpdatePageToolHandler>()

tests/
  Buildout.UnitTests/
    Markdown/
      Editing/                                            # NEW directory
        AnchoredMarkdownRendererTests.cs                  # NEW: anchor-emission per block type; opaque placeholder; byte-identity with spec 002 (SC-001)
        AnchoredMarkdownParserTests.cs                    # NEW: segment round-trips; root sentinel; opaque handling
        RevisionTokenComputerTests.cs                     # NEW: CRC32 format, determinism, metadata-change stability (SC-002 / SC-002a)
        PatchApplicatorTests.cs                           # NEW: per-operation × happy + every error path
        ReconcilerTests.cs                                # NEW: minimal-write guarantee; reorder detection; opaque protection; large-delete guard; dry-run
        PageEditorTests.cs                                # NEW: orchestration; stale-revision; partial-failure surfacing

  Buildout.IntegrationTests/
    Buildin/
      BuildinStubs.cs                                     # MODIFIED: RegisterUpdateBlock + RegisterDeleteBlock (stubs for existing IBuildinClient methods not previously stubbed for write flows)
    Cli/
      GetCommandEditingTests.cs                           # NEW: --editing + --print json; no-regression for spec 002 behavior (SC-007)
      UpdateCommandTests.cs                               # NEW: all error classes; happy paths; partial-failure surfacing; --dry-run; --allow-large-delete
    Mcp/
      GetPageMarkdownToolTests.cs                         # NEW: structured triple; revision; unknown_block_ids
      UpdatePageToolTests.cs                              # NEW: all 5 operation types × happy + every error class
      UpdatePageRoundTripWithCheapLlmTests.cs             # NEW: LLM chain get_page_markdown → update_page → get_page_markdown (FR-025 / SC-010)
    Cross/
      EditModeParityTests.cs                              # NEW: CLI --editing --print json == MCP get_page_markdown (SC-007); CLI update --print json == MCP update_page response
      UpdateReadOnlyOnOtherPagesTests.cs                  # NEW: no write call targets blocks outside the target page (SC-012)
```

**Structure Decision**: All new production code lives under existing projects, organised under
a new `Markdown/Editing/` namespace in `Buildout.Core`. This is the deliberate third member
of the `Markdown/` family: `Conversion/` (read, spec 002), `Authoring/` (create, spec 006),
`Editing/` (edit, this feature). The CLI and MCP additions follow the exact patterns of
`GetCommand` / `PageResourceHandler`, `CreateCommand` / `CreatePageToolHandler`. No new
projects. No new NuGet dependencies.

## Phase 0: Research (output: research.md)

Items unknown at the start of `/speckit-plan` and resolved in `research.md`:

- **R1 – CRC32 in .NET 10**: `System.IO.Hashing.Crc32` (static `Crc32.HashToUInt32(ReadOnlySpan<byte>)`
  available in the .NET 10 shared framework without a separate NuGet package. Formatted as
  `crc.ToString("x8")` for the 8-char lowercase hex token. Decision: use `System.IO.Hashing.Crc32`
  directly; no new package.

- **R2 – Anchored Markdown parsing strategy**: two approaches considered — (A) split by regex
  into `AnchorSegment[]` (flat), (B) parse with Markdig then walk AST. Decision: (B) AST-based,
  because container-boundary detection for `replace_section` requires the parent–sibling
  relationship that Markdig's `MarkdownDocument` tree provides. The existing `MarkdownToBlocksParser`
  from feature 006 is the structural template; the new `AnchoredMarkdownParser` extends it to
  extract the `<!-- buildin:block:<id> -->` HTML-comment nodes that Markdig surfaces as
  `HtmlBlock` siblings immediately before each block node.

- **R3 – `search_replace` at string vs AST level**: Decision: string-level. The patch applicator
  serialises the current `BlockSubtreeWithAnchor[]` back to anchored Markdown string, applies
  the `old_str → new_str` substitution (exact-match, case-sensitive, first occurrence only),
  re-parses the result. This preserves anchor comments inside `old_str` for disambiguation
  (per FR-007) and avoids building a custom AST text-search.

- **R4 – `replace_section` boundary scan**: the `PatchApplicator` operates on the
  `BlockSubtreeWithAnchor[]` tree (not the flat string). The section-end scan walks the
  heading node's immediate parent's `Children` list forward from the heading; it stops at the
  first sibling whose `HeadingLevel <= anchorHeadingLevel` or at the end of the sibling list.
  Container boundaries are handled naturally because the tree's parent–child structure already
  confines the sibling scan to the heading's parent.

- **R5 – Container/leaf predicate for `append_section`**: the existing `BlockToMarkdownRegistry`
  records `RecurseChildren: true` for container block types and `false` for leaves. The
  `PatchApplicator` queries this flag via the same registry already injected into the rendering
  pipeline. No new predicate table required.

- **R6 – Large-delete threshold**: `10` blocks per call. Single-section edits involving a
  heading + several paragraphs stay under 10 comfortably; runaway `replace_block(root)` against
  large pages is blocked. Configurable via `IOptions<PageEditorOptions>.LargeDeleteThreshold`.

- **R7 – Spec 007 observability integration**: operation names for the two new core operations
  are `page_read_editing` (for `FetchForEditAsync`) and `page_update` (for `UpdateAsync`).
  These appear as `operation` label values on `buildout.operations.total`,
  `buildout.operation.duration`, and `buildout.blocks.processed.total` (for `page_update`,
  counts `preserved + updated + deleted + new`). MCP tool names `get_page_markdown` and
  `update_page` appear as `tool` label values on `buildout.mcp.tool.invocations.total` /
  `buildout.mcp.tool.duration`. Patch-rejected outcomes call `recorder.Fail(patchErrorClass)`,
  so the patch error class name (e.g., `patch.stale_revision`) surfaces as the `error_type` tag
  on all existing spec 007 signals — no new metric names, no new span names.

- **R8 – `update --print summary` wire form**:
  ```
  Reconciled page <page_id>: <preserved> preserved, <new> new, <updated> updated, <deleted> deleted
  Revision: <new_revision>
  ```
  JSON form (via `--print json`) is the full `ReconciliationSummary` object serialised with
  `System.Text.Json`. Dry-run adds a `post_edit_markdown` field in JSON mode; in summary mode
  it prepends `[dry-run] ` to the first line.

## Phase 1: Design & Contracts

### data-model.md

Captures in-memory shapes the editor operates over. See `data-model.md`.

### contracts/

Six contract documents:
- `core-editor.md` — `IPageEditor` surface and error taxonomy.
- `patch-operations.md` — 5 operation types, fields, semantic preconditions, error classes.
- `cli-update.md` — `get --editing`, `get --print`, and `update` command surfaces.
- `mcp-edit.md` — `get_page_markdown` and `update_page` tool schemas and error mapping.
- `buildin-endpoints.md` — exhaustive list of buildin endpoints this feature uses.
- `observability.md` — spec 007 extension: new `operation` label values, new `tool` label
  values, patch error class → `error_type` dimension mapping.

### quickstart.md

Three scenarios:
1. Fetch anchored Markdown via CLI, edit, and submit a `replace_section` patch.
2. Pipe an operations JSON document from stdin.
3. LLM-native round-trip via MCP: `get_page_markdown` → edit → `update_page` → re-read.

### Agent context update

`CLAUDE.md` (project root) currently references `specs/007-observability/plan.md` between
the `<!-- SPECKIT START -->` and `<!-- SPECKIT END -->` markers. Phase 1 updates that link to
`specs/008-markdown-page-update/plan.md`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
