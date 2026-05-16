# Contract: MCP Tools — get_page_markdown + update_page

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

Both tools are registered on `Buildout.Mcp/Program.cs` via `.WithTools<...>()` and follow
the `[McpServerTool]` attribute pattern used by `SearchToolHandler` and
`DatabaseViewToolHandler`. Both return `Task<string>` (auto-wrapped as `TextContentBlock`
containing JSON) per the existing pattern.

---

## `get_page_markdown`

**Registration**:
```csharp
[McpServerTool(Name = "get_page_markdown",
    Description = "Fetch a buildin page as anchored Markdown with a revision token. " +
                  "Use this before update_page to obtain the current snapshot and revision. " +
                  "The returned markdown contains <!-- buildin:block:<id> --> comments that " +
                  "anchor each block — include these anchors in patch operations to target " +
                  "specific blocks precisely.")]
public async Task<string> GetPageMarkdownAsync([Description("The buildin page ID")] string page_id, ...)
```

### Input schema

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `page_id` | `string` | Yes | Buildin page UUID |

### Output (success)

`TextContentBlock` containing:
```json
{
  "markdown": "<!-- buildin:root -->\n# Title\n\n<!-- buildin:block:uuid -->...",
  "revision": "1a2b3c4d",
  "unknown_block_ids": []
}
```

### Error mapping

| Condition | MCP error |
|-----------|-----------|
| Page not found (404) | `InvalidParams` (`page_id` unknown) |
| Auth failure (401/403) | `InternalError` |
| Transport error | `InternalError` |
| Unexpected | `InternalError` |

---

## `update_page`

**Registration**:
```csharp
[McpServerTool(Name = "update_page",
    Description = "DESTRUCTIVE. Apply patch operations to an existing buildin page. " +
                  "Always call get_page_markdown first to obtain the revision token. " +
                  "Supply the revision token from that call to prevent overwriting concurrent edits. " +
                  "Use dry_run=true to preview the reconciliation before committing. " +
                  "Failure modes: patch.stale_revision (re-fetch and retry), " +
                  "patch.ambiguous_match (make old_str unique), " +
                  "patch.no_match (check old_str), " +
                  "patch.unknown_anchor (anchor not in snapshot), " +
                  "patch.section_anchor_not_heading (use replace_block instead), " +
                  "patch.anchor_not_container (use insert_after_block instead), " +
                  "patch.reorder_not_supported (delete + re-insert at new position), " +
                  "patch.unsupported_block_touched (avoid altering opaque placeholders), " +
                  "patch.large_delete (set allow_large_delete=true to acknowledge).")]
public async Task<string> UpdatePageAsync(...) 
```

### Input schema

| Field | Type | Required | Default | Notes |
|-------|------|----------|---------|-------|
| `page_id` | `string` | Yes | — | Target page |
| `revision` | `string` | Yes | — | From `get_page_markdown` |
| `operations` | `array` | Yes | — | Min 1; each element is a patch operation object |
| `dry_run` | `bool` | No | `false` | Preview without committing |
| `allow_large_delete` | `bool` | No | `false` | Bypass large-delete guard |

**Operation object** (same discriminated union as CLI; see `patch-operations.md`):
```json
{ "op": "replace_block",    "anchor": "<id>",  "markdown": "..." }
{ "op": "replace_section",  "anchor": "<id>",  "markdown": "..." }
{ "op": "search_replace",   "old_str": "...",  "new_str": "..." }
{ "op": "append_section",   "anchor": "<id>",  "markdown": "..." }
{ "op": "insert_after_block","anchor": "<id>", "markdown": "..." }
```

### Output (success)

`TextContentBlock` containing the `ReconciliationSummary` JSON:
```json
{
  "preserved_blocks": 5,
  "updated_blocks": 1,
  "new_blocks": 2,
  "deleted_blocks": 0,
  "ambiguous_matches": 0,
  "new_revision": "2b3c4d5e",
  "post_edit_markdown": null
}
```

(`post_edit_markdown` is non-null only when `dry_run = true`.)

### Error mapping

| Condition | MCP error | Notes |
|-----------|-----------|-------|
| `patch.stale_revision` | `InvalidParams` | Message includes `current_revision` |
| `patch.ambiguous_match` | `InvalidParams` | Message includes `old_str`, `match_count` |
| `patch.no_match` | `InvalidParams` | Message includes `old_str` |
| `patch.unknown_anchor` | `InvalidParams` | Message includes `anchor` |
| `patch.section_anchor_not_heading` | `InvalidParams` | Message names the anchor |
| `patch.anchor_not_container` | `InvalidParams` | Message includes `anchor`, `block_type`, suggests `insert_after_block` |
| `patch.reorder_not_supported` | `InvalidParams` | Message names anchor, old + new positions |
| `patch.unsupported_block_touched` | `InvalidParams` | Message names anchor |
| `patch.large_delete` | `InvalidParams` | Message includes `would_delete`, `threshold`, hints `allow_large_delete` |
| `patch.partial` | `InternalError` | Message includes partial revision + failed op index |
| Page not found | `InvalidParams` | |
| Auth failure | `InternalError` | |
| Transport error | `InternalError` | |
| Validation (empty ops, missing fields) | `InvalidParams` | |

All `InvalidParams` errors carry the `patch_error_class` key in their data payload so that
callers can recover programmatically without parsing the message string.

---

## Parity invariant (SC-007)

For the same page state and page ID:

- `get --editing --print json` (CLI) == `get_page_markdown` (MCP), modulo wire encoding.
- `update --print json` (CLI) == `update_page` (MCP) response, modulo wire encoding.
- `get` without `--editing` (CLI) == `buildin://{page_id}` resource body (spec 002 SC-003).

Verified by `Buildout.IntegrationTests/Cross/EditModeParityTests.cs`.
