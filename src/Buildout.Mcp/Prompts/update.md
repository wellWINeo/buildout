# Page Update Workflow

When a user asks to update a wiki page, follow these steps to apply changes
safely and correctly.

The `update_page` tool applies patch operations to a page's blocks. It is
non-destructive — each operation targets specific anchored blocks, leaving
untargeted blocks untouched.

## Step 1: Verify Current State

Call `get_page_markdown` with the page ID to fetch the current snapshot. This
returns:

- **Anchored Markdown** — each block is preceded by a comment like
  `<!-- buildin:block:abc123 -->` that identifies its anchor.
- **Revision token** — required for the subsequent `update_page` call. Stale
  tokens cause conflict errors (exit code 7).

## Step 2: Prepare the Update

Examine the anchored Markdown and decide which blocks to modify. Build a JSON
array of patch operations targeting those anchors.

| op | Fields | Description |
|----|--------|-------------|
| `replace_block` | `anchor`, `markdown` | Replace one anchored block |
| `replace_section` | `anchor`, `markdown` | Replace heading + all children |
| `search_replace` | `old_str`, `new_str` | Find-and-replace text across page |
| `append_section` | `anchor` (opt), `markdown` | Append after section or at end |
| `insert_after_block` | `anchor`, `markdown` | Insert after a specific block |

Use `dry_run: true` to preview changes before committing.

## Step 3: Execute the Update

Call `update_page` with the page ID, revision token, and ops array. The tool
returns the updated page state on success.

## Step 4: Verify the Update

Re-fetch the page with `get_page_markdown` and confirm the modifications
appear as intended.

## Important Considerations

### Partial Updates

You only need to include operations for blocks you want to modify — all other
blocks remain untouched. Never include all blocks as a bulk replace.

### Adding New Content

Use `append_section` to add content at the end of a section, or
`insert_after_block` to insert at a precise location. Both accept full
Markdown for the new blocks.

### Fixing Errors

If `update_page` returns exit code 7 (revision conflict), the page was
modified after you fetched it. Re-run `get_page_markdown` to get a fresh
revision token and rebuild your ops array before retrying.

## Critical Rules

- Never fabricate anchors — always extract them from `get_page_markdown` output.
- Always get a fresh revision token before every update call.
- For large deletions, set `allow_large_delete: true`.
- Ops JSON uses `snake_case` names (`old_str`, `new_str`, not `oldStr`).
- Stdin mode (`--ops -`) reads up to 16 MB.
