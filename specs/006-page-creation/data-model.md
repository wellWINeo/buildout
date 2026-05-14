# Data Model: Page Creation from Markdown

**Feature**: [spec.md](./spec.md) · [plan.md](./plan.md)
**Date**: 2026-05-13

This document captures the in-memory shapes the creator operates over.
None of these are new buildin entities — they are internal types in
`Buildout.Core.Markdown.Authoring`.

---

## `CreatePageInput`

The validated input to `IPageCreator.CreateAsync`.

| Field | Type | Required | Notes |
|---|---|---|---|
| `ParentId` | `string` | yes | Buildin id of the parent page or database. Probed at the start of `CreateAsync`. |
| `Markdown` | `string` | yes | The document body. May start with a leading `# Title`. |
| `Title` | `string?` | no | Overrides leading-H1 title (spec FR-005). Whitespace-trimmed before use. |
| `Icon` | `string?` | no | Single emoji grapheme cluster, or `http(s)://` URL → external icon. |
| `CoverUrl` | `string?` | no | `http(s)://` URL → external cover image. |
| `Properties` | `IReadOnlyDictionary<string, string>?` | no | Property name → raw string value. Only meaningful when the parent is a database (FR-010). |
| `Print` | `CreatePagePrintMode?` | no | CLI-only; `Id` (default), `Json`, `None`. The MCP surface does not pass this. |

**Validation rules** (applied by `IPageCreator` before any buildin
call, raising `ArgumentException` mapped to the validation-error
failure class):

- `ParentId` is non-empty.
- `Markdown` (after title resolution) is allowed to be empty as long as
  `Title` is set; otherwise either a leading H1 or an explicit `Title`
  must be present.
- `Icon`, if set, is either a single grapheme cluster *or* a
  well-formed absolute URI.
- `CoverUrl`, if set, is a well-formed absolute URI.
- `Properties`: property names exist in the probed database's schema
  (parent kind must be database; otherwise non-empty `Properties` is a
  validation error). Each value parses per its property kind (R6).
- Stdin reads (CLI `-`) are bounded at 16 MiB (R9). Path reads are
  unbounded.

---

## `ParentKind`

The result of the parent-kind probe.

```text
ParentKind =
    | Page(string PageId)
    | Database(Database Schema)        // carries the full Database model so the property parser dispatches
    | NotFound
```

`NotFound` is produced when both the page-probe and the
database-probe return 404. The caller maps it to the page-not-found
failure class (spec FR-010).

---

## `AuthoredDocument`

The parser's pure output (no I/O, no client involvement).

| Field | Type | Notes |
|---|---|---|
| `Title` | `string?` | The leading-H1 text, or `null` if no leading H1 was present. |
| `Body` | `IReadOnlyList<BlockSubtreeWrite>` | Top-level blocks in source order. May be empty. |

Produced by `IMarkdownToBlocksParser.Parse(string markdown)`. Pure
function; same input → same output.

---

## `BlockSubtreeWrite`

Write-direction sibling of feature 002's read-direction `BlockSubtree`
(under `Markdown/Internal/`).

| Field | Type | Notes |
|---|---|---|
| `Block` | `Block` | A buildin block payload. `Id`, `CreatedAt`, `LastEditedAt`, `HasChildren`, `Parent` are unset for new blocks. |
| `Children` | `IReadOnlyList<BlockSubtreeWrite>` | Children of this block. Empty when the block has none. |

Used by the batcher (R4) to fan out nested levels via post-create
`appendBlockChildren(parent_block_id, batch)`.

---

## `CreatePageOutcome`

The success/partial-failure result returned by
`IPageCreator.CreateAsync`. Internal type; not exposed across the
project boundary — the CLI and MCP adapters translate it.

| Field | Type | Set when |
|---|---|---|
| `NewPageId` | `string` | always (creation succeeded; full body may or may not be complete) |
| `PartialPageId` | `string?` | set iff a body batch failed after `createPage` succeeded; equals `NewPageId` in that case |
| `FailureClass` | `FailureClass?` | `null` on full success; set to the class the surface should map |
| `UnderlyingException` | `Exception?` | propagated for adapters that want to log the cause |

`FailureClass` enum values: `Validation`, `NotFound`, `Auth`,
`Transport`, `Unexpected`, `Partial`. Maps 1:1 to the exit codes /
MCP error codes documented in `contracts/cli-create.md` and
`contracts/mcp-create.md`.

A `PartialCreationException(string newPageId, int batchesAppended, int
totalBatches, Exception underlying)` is the wire form
`IPageCreator` throws when the post-`createPage` path fails; the
adapter catches it and synthesizes the `CreatePageOutcome`. This
keeps the throw-or-return decision inside the core rather than
duplicated across adapters.

---

## `CompatibilityMatrixEntry` (extended)

The per-block-type matrix from feature 002 gains a write column.

| Block type | Read direction (feature 002) | Write direction (this feature) | Notes |
|---|---|---|---|
| `paragraph` | lossless | lossless | Inline formatting and links round-trip. |
| `heading_1` | lossless | lossless | First-position H1 is *consumed as title* by default, not emitted as a body block (spec FR-005). |
| `heading_2` / `heading_3` | lossless | lossless | |
| `bulleted_list_item` | lossless | lossless | Includes nested children. |
| `numbered_list_item` | lossless | lossless | Numbering is regenerated by buildin; source numbers are not preserved. |
| `to_do` | lossless | lossless | GFM `- [ ]` / `- [x]` round-trips. |
| `code` | lossless | lossless | Language tag preserved when present. |
| `quote` | lossless | lossless | |
| `divider` | lossless | lossless | CommonMark thematic break. |
| `child_database` (inline) | rendered as inline table (feature 005) | **unsupported on write** | The rendered table is preserved as paragraphs of text; no `child_database` block is materialised. |
| `image`, `file`, `bookmark`, `embed`, `callout`, `equation`, `link_to_page`, `template`, `synced_block`, `column_list`, `column`, `table`, `table_row`, `child_page`, `toggle` | placeholder (feature 002 FR-003) | **placeholder pass-through**: the read-side placeholder line survives a CommonMark round-trip without producing a real block. | Spec FR-006 / FR-007. |
| **Inline** mention: page / database | rendered as `[Title](buildin://<id>)` | **lossless**: parsed back into a buildin mention `RichText` run (R3). | Page vs database disambiguated server-side. |
| **Inline** mention: user | rendered as plain `@Name` (feature 002 FR-005b) | **one-way-lossy**: written as plain text run, not a user-mention RichText. | Spec FR-004; documented loss. |
| **Inline** mention: date | rendered as ISO date string | **one-way-lossy**: written as plain text run, not a date-mention RichText. | Spec FR-004; documented loss. |
| **Inline** formatting: bold, italic, inline code, link | round-trips per CommonMark / GFM | round-trips per CommonMark / GFM | Plain `http(s)://` links remain link annotations; `buildin://` links promote to mentions per R3. |

The matrix is enforced by the round-trip tests under
`tests/Buildout.UnitTests/RoundTrip/`. Any drift (a row marked lossless
that turns out lossy) breaks the round-trip suite immediately.
