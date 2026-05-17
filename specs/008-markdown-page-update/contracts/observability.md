# Contract: Observability Integration (Spec 007 Extension)

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

This document specifies how feature 008 integrates with spec 007's observability
infrastructure. Per FR-028 and spec 007's design, **no new metric names, span names, or
exporters are introduced**. The feature adds new label values to existing dimensions.

---

## New operation label values

Spec 007's `buildout.operations.total` and `buildout.operation.duration` counters use an
`operation` label. Feature 008 adds:

| New `operation` value | Corresponds to |
|-----------------------|----------------|
| `page_read_editing` | `IPageEditor.FetchForEditAsync` |
| `page_update` | `IPageEditor.UpdateAsync` |

Usage in `PageEditor.cs`:
```csharp
// FetchForEditAsync
using var recorder = OperationRecorder.Start(_logger, "page_read_editing");

// UpdateAsync
using var recorder = OperationRecorder.Start(_logger, "page_update");
```

`buildout.blocks.processed.total` gains:
- `{operation=page_read_editing}` — count of blocks fetched (same as `page_read`)
- `{operation=page_update}` — count of blocks processed (preserved + updated + deleted + new)

---

## New MCP tool label values

Spec 007's `buildout.mcp.tool.invocations.total` and `buildout.mcp.tool.duration` counters
use a `tool` label. Feature 008 adds:

| New `tool` value | Corresponds to |
|------------------|----------------|
| `get_page_markdown` | `GetPageMarkdownToolHandler` |
| `update_page` | `UpdatePageToolHandler` |

These are wired in the MCP tool handlers using the same `OperationRecorder` wrapper pattern
used by `SearchToolHandler`, `DatabaseViewToolHandler`, and `CreatePageToolHandler`.

---

## New `error_type` values (patch-rejected outcomes)

Spec 007's `buildout.operations.total{outcome=failure}` and
`buildout.operation.duration{outcome=failure}` already carry an `error_type` tag. Feature 008
adds the following values:

| `error_type` value | Condition |
|--------------------|-----------|
| `patch.stale_revision` | Caller's revision is outdated |
| `patch.ambiguous_match` | `old_str` matches more than once |
| `patch.no_match` | `old_str` matches zero times |
| `patch.unknown_anchor` | Referenced anchor not in snapshot |
| `patch.section_anchor_not_heading` | `replace_section` anchor is not a heading |
| `patch.anchor_not_container` | `append_section` anchor is a leaf block |
| `patch.reorder_not_supported` | Patch would reorder existing blocks |
| `patch.unsupported_block_touched` | Patch would alter/remove an opaque placeholder |
| `patch.large_delete` | Deletion count exceeds threshold |
| `patch.partial` | Mid-reconciliation buildin write failure |

Usage in `PageEditor.cs`:
```csharp
catch (PatchRejectedException ex)
{
    recorder.Fail(ex.PatchErrorClass);  // e.g., "patch.stale_revision"
    throw;
}
```

---

## Log attributes

Structured log entries (via `OperationRecorder`) include these tags for feature 008 calls:

| Tag | Present for | Value |
|-----|-------------|-------|
| `page_id` | All | Target page UUID |
| `block_count` | `page_read_editing` | Total blocks fetched |
| `error_type` | Failure paths | Patch error class name |
| `preserved_blocks` | `page_update` success | Count |
| `updated_blocks` | `page_update` success | Count |
| `new_blocks` | `page_update` success | Count |
| `deleted_blocks` | `page_update` success | Count |
| `dry_run` | `page_update` when true | `"true"` |

---

## Metrics registry update

The following rows are additions to `specs/007-observability/contracts/metrics-registry.md`:

**`buildout.operations.total` — `operation` label values** (append):
`page_read_editing`, `page_update`

**`buildout.mcp.tool.invocations.total` — `tool` label values** (append):
`get_page_markdown`, `update_page`

**`buildout.api.calls.total` — `method` label values** (append):
`UpdateBlockAsync`, `DeleteBlockAsync` (already declared in `IBuildinClient`; feature 008
is the first to exercise them)

The `error_type` dimension on all failure-path counters now also accepts `patch.*` values
(it was always a free-form string; this documents the valid values).
