# Research: Markdown Page Update via Patch Operations

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16
**Phase**: 0

---

## R1 – CRC32 in .NET 10

**Decision**: Use `System.IO.Hashing.Crc32` from the .NET 10 shared framework.

**Rationale**: `System.IO.Hashing` has been part of the .NET shared framework since .NET 7
(shipped out-of-band as a NuGet package in earlier versions but is now inbox). No new
`PackageReference` is required in `Buildout.Core.csproj`.

API used:
```csharp
uint checksum = System.IO.Hashing.Crc32.HashToUInt32(utf8Bytes);
string revision = checksum.ToString("x8");  // "1a2b3c4d"
```

The input to the hash is the UTF-8 bytes of the full anchored-Markdown body returned by
`FetchForEditAsync` — the exact string the caller receives (including the
`<!-- buildin:root -->` sentinel and all block-anchor comments). The token is exactly 8
lowercase hexadecimal characters.

**Alternatives considered**:
- `System.Security.Cryptography.SHA256` — rejected: produces 64-char hex, too long for a
  per-call revision cookie an LLM must carry on every `update_page` invocation.
- `System.IO.Hashing.XxHash32` — rejected: CRC32 was selected in spec clarification for
  compatibility reasons and familiarity; both are equally fast on small strings.
- Third-party CRC32 (`Force.Crc32`) — rejected: `System.IO.Hashing.Crc32` is inbox.

---

## R2 – Anchored Markdown Parsing Strategy

**Decision**: AST-based via Markdig.

**Rationale**: The `replace_section` and `insert_after_block` operations require knowing the
parent–sibling relationship of each block (e.g., "walk forward through siblings of the same
parent until a heading of equal-or-higher level"). A flat string-split approach cannot
determine container boundaries (toggles, callouts, columns, list items with children) without
re-parsing anyway. The AST-based approach parses once and supports all operations naturally.

**How it works**:

Markdig parses the full anchored-Markdown string. HTML comment lines of the form
`<!-- buildin:block:<id> -->`, `<!-- buildin:root -->`, and `<!-- buildin:opaque:<id> -->`
become `HtmlBlock` nodes in the Markdig `MarkdownDocument` at the appropriate nesting depth.
The `AnchoredMarkdownParser` walks the Markdig AST using the same visitor pattern as feature
006's `MarkdownToBlocksParser`, with one addition: before processing each block node, it checks
whether the immediately preceding sibling is an `HtmlBlock` matching the anchor pattern. If so,
it attaches the extracted anchor ID and kind (`Block`, `Opaque`, or `Root`) to the produced
`BlockSubtreeWithAnchor` node.

The existing `MarkdownToBlocksParser` from feature 006 already handles per-block-type mapping
(paragraph, headings, lists, code, quote, divider, etc.). The `AnchoredMarkdownParser` wraps
or subclasses this parser; the delta is the anchor-extraction pass.

**Alternatives considered**:
- Regex-split into `AnchorSegment[]` (flat) — rejected: cannot detect container boundaries,
  making `replace_section`'s section-extent scan non-trivial and error-prone.

---

## R3 – `search_replace` at String vs AST Level

**Decision**: String-level search-replace, then re-parse.

**Rationale**: `search_replace` operates on the text content of the page, not on its block
structure. Callers may include anchor comments in `old_str` to disambiguate (per FR-007).
A string search naturally handles multi-line spans. The cost of a full re-parse after
each `search_replace` is acceptable: the anchored Markdown for a 200-block page is ~10 KB,
and Markdig is fast.

**How it works**:

1. Serialise the current `BlockSubtreeWithAnchor[]` to anchored Markdown string.
2. Count occurrences of `old_str` (exact, case-sensitive). If > 1 → `patch.ambiguous_match`.
   If 0 → `patch.no_match`.
3. Replace the first occurrence with `new_str`.
4. Re-parse the result with `AnchoredMarkdownParser` → updated `BlockSubtreeWithAnchor[]`.

The serialisation in step 1 uses the same `AnchoredMarkdownRenderer` that produced the
original snapshot. This guarantees that anchor IDs in the re-parsed tree are consistent with
those in the original tree.

---

## R4 – `replace_section` Boundary Scan

**Decision**: Operate on the `BlockSubtreeWithAnchor[]` tree; scan the heading node's
parent's `Children` list.

**Rationale**: The tree already encodes container hierarchy. The parent–child relationship
confines the scan to the correct scope without any additional bookkeeping.

**Algorithm**:

```
procedure replace_section(tree, anchorId, newMarkdown):
  node = find_by_anchor(tree, anchorId)
  if node is null → patch.unknown_anchor
  if node.Block is not Heading → patch.section_anchor_not_heading
  parent = find_parent(tree, node)
  siblings = parent.Children
  startIdx = indexOf(siblings, node)
  endIdx = startIdx + 1
  headingLevel = node.Block.HeadingLevel
  while endIdx < siblings.Length:
    sib = siblings[endIdx]
    if sib.Block is Heading and sib.Block.HeadingLevel <= headingLevel:
      break
    endIdx++
  // Replace [startIdx, endIdx) with parsed new Markdown
  newNodes = parse(newMarkdown)
  siblings = siblings[0..startIdx] + newNodes + siblings[endIdx..]
  parent.Children = siblings
```

Container boundaries are automatically respected because the scan never leaves `parent.Children`.

---

## R5 – Container/Leaf Predicate for `append_section`

**Decision**: Use the existing `BlockToMarkdownRegistry.Resolve(block).RecurseChildren`.

**Rationale**: The `BlockToMarkdownRegistry` already records the per-block-type `RecurseChildren`
flag, which is `true` for every container type (toggle heading, list/to-do item, quote, callout,
column, toggle, table row, page root) and `false` for leaves (paragraph, code, divider, image,
plain non-toggle heading, etc.). This is the spec 002 compatibility matrix.

The `PatchApplicator` resolves the anchored block's type via the same `BlockToMarkdownRegistry`
instance already in the DI container. If `RecurseChildren == false` for the anchor's block type
→ `patch.anchor_not_container`.

---

## R6 – Large-Delete Threshold

**Decision**: Default threshold = **10 blocks**.

**Rationale**:
- Single-section edits: a heading + 3–6 paragraphs = 4–7 deletions → safely under 10.
- Runaway `replace_block(root)` against a 30-block page → 29 deletions → blocked.
- The threshold is per-call, not cumulative.

The value is the default for `PageEditorOptions.LargeDeleteThreshold` (type `int`). Operators
can override it via `IConfiguration` ("PageEditor:LargeDeleteThreshold") in `Buildout.Mcp`'s
`Program.cs`, following the existing options pattern (`BuildinClientOptions` pattern).

---

## R7 – Spec 007 Observability Integration

**Decision**: Use `OperationRecorder` with two new operation names; extend the metrics registry
with new label values; no new instruments.

**New operation names** (added to the `operation` dimension):

| Surface | Operation name |
|---------|----------------|
| `FetchForEditAsync` | `page_read_editing` |
| `UpdateAsync` | `page_update` |

These appear in:
- `buildout.operations.total{operation=page_read_editing, outcome=success/failure}`
- `buildout.operation.duration{operation=page_update, outcome=...}`
- `buildout.blocks.processed.total{operation=page_update}` — counts `preserved + updated + deleted + new`
- `buildout.blocks.processed.total{operation=page_read_editing}` — counts total blocks fetched

**New MCP tool names** (added to the `tool` dimension):

| Tool name |
|-----------|
| `get_page_markdown` |
| `update_page` |

These appear in `buildout.mcp.tool.invocations.total` and `buildout.mcp.tool.duration`.

**Error type mapping** (patch-rejected outcomes):

```csharp
recorder.Fail("patch.stale_revision");
recorder.Fail("patch.ambiguous_match");
recorder.Fail("patch.no_match");
recorder.Fail("patch.unknown_anchor");
recorder.Fail("patch.section_anchor_not_heading");
recorder.Fail("patch.anchor_not_container");
recorder.Fail("patch.reorder_not_supported");
recorder.Fail("patch.unsupported_block_touched");
recorder.Fail("patch.large_delete");
recorder.Fail("patch.partial");
```

Each `Fail` call records `buildout.operations.total{outcome=failure}` with the patch error
class name as the `error_type` tag value. The metrics-registry.md is updated in
`contracts/observability.md` to enumerate these values in the `error_type` dimension.

**No new metrics, spans, or exporters are introduced** — as required by FR-028.

---

## R8 – `update --print summary` Wire Form

**Decision**: Human-readable multi-line block for `--print summary` (default); JSON object
for `--print json`.

**Summary format** (stdout):
```
Reconciled page <page_id>: <preserved> preserved, <updated> updated, <new> new, <deleted> deleted
Revision: <new_revision>
```

Dry-run variant (prepend `[dry-run] ` to first line):
```
[dry-run] Reconciled page <page_id>: <preserved> preserved, <updated> updated, <new> new, <deleted> deleted
Revision: <new_revision> (not committed)
```

**JSON format** (`--print json`) — the full `ReconciliationSummary` object:
```json
{
  "preserved_blocks": 5,
  "updated_blocks": 1,
  "new_blocks": 2,
  "deleted_blocks": 0,
  "ambiguous_matches": 0,
  "new_revision": "1a2b3c4d",
  "post_edit_markdown": "<!-- buildin:root -->\n# Title\n..."  // only when dry_run = true
}
```

This is consistent with the MCP `update_page` response (same JSON shape).

For `get --editing --print json` (CLI fetch-for-edit):
```json
{
  "markdown": "<!-- buildin:root -->\n# Title\n...",
  "revision": "1a2b3c4d",
  "unknown_block_ids": []
}
```

Without `--print json` (default, stdout + stderr):
- stdout: anchored Markdown body
- stderr:
  ```
  revision: 1a2b3c4d
  unknown_block_id: <id-if-any>
  ```
