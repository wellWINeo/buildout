# Page Update Workflow

The update_page tool applies patch operations to a page's blocks. It is non-destructive — each operation targets specific anchored blocks, leaving untargeted blocks untouched.

## Workflow

1. Call `get_page_markdown` with the page ID to get an editing snapshot (includes revision token and anchored markdown)
2. Examine the anchored markdown — each block has a comment like `<!-- block-id:abc123 -->` identifying its anchor
3. Build a JSON array of patch operations (see below)
4. Call `update_page` with the page ID, revision token, and ops array

## Patch Operations

Each operation is a JSON object with an `op` field:

| op | Fields | Description |
|----|--------|-------------|
| `replace_block` | `anchor`, `markdown` | Replace one anchored block |
| `replace_section` | `anchor`, `markdown` | Replace section (heading + children) |
| `search_replace` | `old_str`, `new_str` | Find-and-replace across page |
| `append_section` | `anchor` (optional), `markdown` | Append blocks after section or at page end |
| `insert_after_block` | `anchor`, `markdown` | Insert after a specific block |

## Critical Rules

- Always get a fresh revision token before updating — stale tokens cause conflicts
- Never guess or fabricate anchors — extract them from the get_page_markdown output
- Use `dry_run: true` to preview changes when unsure
- For large deletions, set `allow_large_delete: true` (the tool will reject otherwise)
