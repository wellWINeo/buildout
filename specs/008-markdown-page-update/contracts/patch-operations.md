# Contract: Patch Operations

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

All five operation types share the same discriminant field `"op"` in JSON. Operations are
deserialized by a `JsonConverter<PatchOperation>` in `Buildout.Core`.

---

## `replace_block`

**JSON discriminant**: `"op": "replace_block"`

| Field | Type | Required | Constraint |
|-------|------|----------|------------|
| `anchor` | `string` | Yes | A block ID present in the snapshot, or the literal `"root"` |
| `markdown` | `string` | Yes | New Markdown for the block and its tree descendants |

**Effect**: Replace the anchored block's subtree with the parsed `markdown`. When
`anchor == "root"`, replace the page's child-block list; a leading `# Heading` in `markdown`
is consumed as the new page title per spec 006 FR-005.

**Errors**:
- `patch.unknown_anchor` — `anchor` not present in the snapshot (and is not `"root"`).
- `patch.unsupported_block_touched` — `anchor` refers to an opaque block (attempt to replace
  an unsupported-block placeholder).

**Container boundary**: `replace_block` replaces the targeted subtree in its entirety; it
does not scan siblings. The large-delete guard applies (FR-013).

---

## `replace_section`

**JSON discriminant**: `"op": "replace_section"`

| Field | Type | Required | Constraint |
|-------|------|----------|------------|
| `anchor` | `string` | Yes | A block ID of a **heading** block in the snapshot |
| `markdown` | `string` | Yes | Replacement Markdown |

**Effect**: Replace the heading block plus all sibling blocks following it (in the heading's
parent's children list) up to — but not including — the first sibling heading of
equal-or-higher level, or the end of the parent's children list. The scan is confined to
the heading's **immediate-sibling list** (same parent); container boundaries are never crossed.
The new `markdown` is parsed and inserted in place.

**Errors**:
- `patch.unknown_anchor` — `anchor` not present.
- `patch.section_anchor_not_heading` — the anchored block is not a heading (includes the
  page-root sentinel; `anchor == "root"` is always rejected by this operation).
- `patch.unsupported_block_touched` — the section extent includes an opaque block that would
  be deleted.

---

## `search_replace`

**JSON discriminant**: `"op": "search_replace"`

| Field | Type | Required | Constraint |
|-------|------|----------|------------|
| `old_str` | `string` | Yes | Non-empty |
| `new_str` | `string` | Yes | May be empty (deletes the matched span) |

**Effect**: Exact, case-sensitive, first-occurrence replacement of `old_str` with `new_str`
in the current anchored-Markdown string. Anchor comments may be included in `old_str` for
disambiguation.

**Errors**:
- `patch.ambiguous_match` — `old_str` appears more than once in the anchored Markdown.
  Carries `{ old_str, match_count }`.
- `patch.no_match` — `old_str` appears zero times.

**Notes**:
- Multi-block spans are allowed as long as `old_str` is unique.
- After replacement, the full anchored Markdown is re-parsed. If the result fails to parse
  as valid CommonMark, a validation exception names the operation index.
- If `new_str` is empty and the replacement would delete an opaque anchor →
  `patch.unsupported_block_touched`.

---

## `append_section`

**JSON discriminant**: `"op": "append_section"`

| Field | Type | Required | Constraint |
|-------|------|----------|------------|
| `anchor` | `string?` | No | A block ID of a **container** block, `"root"`, or omitted |
| `markdown` | `string` | Yes | Content to append as children |

**Effect**: Append the parsed `markdown` as children of the anchored container block. When
`anchor` is omitted or equals `"root"`, append at the end of the page's children. Existing
children of the anchored block keep their block IDs.

**Errors**:
- `patch.unknown_anchor` — `anchor` is present but not in the snapshot.
- `patch.anchor_not_container` — the anchored block is a leaf type (paragraph, code, divider,
  image, plain non-toggle heading, etc.). Carries `{ anchor, block_type }`. Suggests using
  `insert_after_block` for sibling-level insertion.

**Container test**: `BlockToMarkdownRegistry.Resolve(block).RecurseChildren == true`.

---

## `insert_after_block`

**JSON discriminant**: `"op": "insert_after_block"`

| Field | Type | Required | Constraint |
|-------|------|----------|------------|
| `anchor` | `string` | Yes | A block ID of any block in the snapshot (heading or otherwise) |
| `markdown` | `string` | Yes | Content to insert |

**Effect**: Insert the parsed `markdown` as the next siblings of the anchored block in the
anchored block's parent's children list, immediately after the anchored block. Accepts any
block type as anchor (heading, paragraph, code, list item, etc.). The literal `"root"` is NOT
accepted (the page-root sentinel has no sibling list).

**Errors**:
- `patch.unknown_anchor` — `anchor` not present in the snapshot; or `anchor == "root"`.
- `patch.unsupported_block_touched` — `anchor` refers to an opaque block (opaque blocks must
  not be used as insertion anchors; use the preceding or following non-opaque block instead).

---

## Shared Constraints (all operations)

- **Order**: operations are applied in index order. Each operation receives the state
  produced by the previous operation (FR-008).
- **Fail-fast**: the first failing operation aborts processing; no buildin write calls are
  issued, and the error carries the index of the failing operation.
- **Reorder detection** (Reconciler, post-application): any anchor whose sibling position or
  parent differs between the original tree and the patched tree → `patch.reorder_not_supported`.
  This check runs after all operations have been applied, before any write call.
- **Validation-class failures** (malformed Markdown, empty `Operations` list, missing required
  fields) are distinct from `patch-rejected` errors; they surface before any operation runs.
