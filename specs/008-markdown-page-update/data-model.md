# Data Model: Markdown Page Update via Patch Operations

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

---

## In-Memory Shapes (Core, Editing namespace)

### `FetchForEditInput`

Input to `IPageEditor.FetchForEditAsync`.

| Field | Type | Constraints |
|-------|------|-------------|
| `PageId` | `string` | Non-empty; must be a valid buildin page id |

### `AnchoredPageSnapshot`

Output of `IPageEditor.FetchForEditAsync`. The triple callers pass into `UpdateAsync` and
CLI / MCP surface translate directly.

| Field | Type | Notes |
|-------|------|-------|
| `Markdown` | `string` | Full anchored-Markdown body; starts with `<!-- buildin:root -->` |
| `Revision` | `string` | CRC32 of `Markdown`'s UTF-8 bytes, 8-char lowercase hex |
| `UnknownBlockIds` | `IReadOnlyList<string>` | Block IDs the renderer could not express in Markdown |

### `UpdatePageInput`

Input to `IPageEditor.UpdateAsync`.

| Field | Type | Constraints |
|-------|------|-------------|
| `PageId` | `string` | Non-empty |
| `Revision` | `string` | Non-empty; opaque 8-char hex supplied by the caller |
| `Operations` | `IReadOnlyList<PatchOperation>` | Min length 1 |
| `DryRun` | `bool` | Default `false` |
| `AllowLargeDelete` | `bool` | Default `false` |

### `ReconciliationSummary`

Output of `IPageEditor.UpdateAsync` (committing or dry-run).

| Field | Type | Notes |
|-------|------|-------|
| `PreservedBlocks` | `int` | Blocks unchanged (no write call issued) |
| `UpdatedBlocks` | `int` | Blocks patched in-place (`updateBlock` calls) |
| `NewBlocks` | `int` | Blocks created (`appendBlockChildren` calls) |
| `DeletedBlocks` | `int` | Blocks deleted (`deleteBlock` calls) |
| `AmbiguousMatches` | `int` | Always 0 for a successful call (non-zero possible in future partial-ambiguity surface) |
| `NewRevision` | `string` | Revision that would be returned by an immediate re-fetch |
| `PostEditMarkdown` | `string?` | Non-null only when `DryRun = true`; the anchored Markdown the commit would have produced |

**Invariant** (SC-004):
`UpdatedBlocks + NewBlocks + DeletedBlocks == total buildin write calls issued`

### `PageEditorOptions`

`IOptions<PageEditorOptions>` — registered in DI, configurable via `IConfiguration`.

| Field | Type | Default |
|-------|------|---------|
| `LargeDeleteThreshold` | `int` | `10` |

Configuration key: `"PageEditor:LargeDeleteThreshold"` (matches the `BuildinClientOptions`
pattern in `BuildinClientOptions.cs`).

---

## Patch Operation Discriminated Union

```
PatchOperation (abstract record base)
├── ReplaceBlockOperation      { Anchor: string, Markdown: string }
├── ReplaceSectionOperation    { Anchor: string, Markdown: string }
├── SearchReplaceOperation     { OldStr: string, NewStr: string }
├── AppendSectionOperation     { Anchor: string?, Markdown: string }
└── InsertAfterBlockOperation  { Anchor: string, Markdown: string }
```

JSON discriminant field: `"op"` with values `"replace_block"`, `"replace_section"`,
`"search_replace"`, `"append_section"`, `"insert_after_block"`.
Deserialized by a `JsonConverter` in `Buildout.Core`; used by CLI (JSON file / stdin) and
the MCP tool handler.

---

## Internal Types (Editing/Internal)

### `BlockSubtreeWithAnchor`

Write-direction sibling of feature 002's `BlockSubtree` (read) and feature 006's
`BlockSubtreeWrite` (create). Extends the create-direction subtree with an optional anchor.

| Field | Type | Notes |
|-------|------|-------|
| `AnchorId` | `string?` | Block ID extracted from the preceding anchor comment; null for new blocks |
| `AnchorKind` | `AnchorKind` | `Root`, `Block`, or `Opaque` |
| `Block` | `BlockSubtreeWrite` | Block payload + children (reuses feature 006's write-direction type) |

`AnchorKind.Root` is only used for the page-root sentinel at the top of the list (no
`BlockSubtreeWrite` payload; children are the page's top-level blocks).

### `AnchorKind`

```
enum AnchorKind { Root, Block, Opaque }
```

### `RevisionTokenComputer`

Stateless static helper.

```csharp
public static string Compute(string anchoredMarkdown)
    => Crc32.HashToUInt32(Encoding.UTF8.GetBytes(anchoredMarkdown)).ToString("x8");
```

---

## Error Classes

### `PatchRejectedException`

Base class for all `patch.*` failures. Carries:

| Field | Type | Notes |
|-------|------|-------|
| `PatchErrorClass` | `string` | e.g., `"patch.stale_revision"` |
| `Message` | `string` | Human-readable description |
| `Details` | `IReadOnlyDictionary<string,object>?` | Class-specific payload (see below) |

Subclasses:

| Subclass | `PatchErrorClass` | Notable `Details` keys |
|----------|-------------------|------------------------|
| `StaleRevisionException` | `patch.stale_revision` | `current_revision` |
| `AmbiguousMatchException` | `patch.ambiguous_match` | `old_str`, `match_count` |
| `NoMatchException` | `patch.no_match` | `old_str` |
| `UnknownAnchorException` | `patch.unknown_anchor` | `anchor` |
| `SectionAnchorNotHeadingException` | `patch.section_anchor_not_heading` | `anchor` |
| `AnchorNotContainerException` | `patch.anchor_not_container` | `anchor`, `block_type` |
| `ReorderNotSupportedException` | `patch.reorder_not_supported` | `anchor`, `old_position`, `new_position` |
| `UnsupportedBlockTouchedException` | `patch.unsupported_block_touched` | `anchor` |
| `LargeDeleteException` | `patch.large_delete` | `would_delete`, `threshold` |
| `PartialPatchException` | `patch.partial` | `partial_revision`, `committed_op_index`, `buildin_error` |

---

## Compatibility Matrix Extension

The feature extends spec 002's per-block-type compatibility matrix with an **edit** column:

| Block type | Read (spec 002) | Write (spec 006) | Edit (spec 008) |
|------------|-----------------|------------------|-----------------|
| paragraph | lossless | lossless | id-preserving |
| heading_1 / 2 / 3 | lossless | lossless | id-preserving |
| bulleted_list_item | lossless | lossless | id-preserving |
| numbered_list_item | lossless | lossless | id-preserving |
| to_do | lossless | lossless | id-preserving |
| code | lossless | lossless | id-preserving |
| quote | lossless | lossless | id-preserving (container) |
| divider | lossless | lossless | id-preserving |
| callout | lossless | lossless | id-preserving (container) |
| toggle | lossless | lossless | id-preserving (container) |
| column_list | lossless | N/A | id-preserving (container) |
| column | lossless | N/A | id-preserving (container) |
| table | lossless | N/A | id-preserving (container) |
| table_row | lossless | N/A | id-preserving (container) |
| child_page | placeholder | N/A | opaque-protected |
| child_database | placeholder | N/A | opaque-protected |
| image | lossless | N/A | id-preserving |
| unsupported (other) | placeholder | N/A | opaque-protected |

**id-preserving**: block ID retained when anchor, parent, and sibling position are unchanged.  
**opaque-protected**: emitted as `<!-- buildin:opaque:<id> -->` + placeholder text; patches that
would alter or remove the opaque anchor fail with `patch.unsupported_block_touched`.
