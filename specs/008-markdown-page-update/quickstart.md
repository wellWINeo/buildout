# Quickstart: Markdown Page Update via Patch Operations

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

---

## Scenario 1 — CLI: fetch, edit, patch

**Step 1** — Fetch the page in edit mode:

```sh
dotnet run --project src/Buildout.Cli -- get <page_id> --editing --print json > snapshot.json
```

The JSON file contains `{ "markdown": "...", "revision": "1a2b3c4d", "unknown_block_ids": [] }`.

**Step 2** — Author the operations file. For example, to replace the section under `## API`:

```json
[
  {
    "op": "replace_section",
    "anchor": "<block-id-of-the-api-heading>",
    "markdown": "## API\n\nNew API description goes here.\n"
  }
]
```

(The anchor ID is visible in `snapshot.json`'s `markdown` field as
`<!-- buildin:block:<id> -->` on the line before the heading.)

**Step 3** — Preview the change without committing (dry-run):

```sh
REVISION=$(cat snapshot.json | jq -r .revision)
dotnet run --project src/Buildout.Cli -- update \
  --page <page_id> \
  --revision "$REVISION" \
  --ops ops.json \
  --dry-run \
  --print json
```

The response shows the reconciliation summary with `post_edit_markdown` included, and no
buildin write calls are issued.

**Step 4** — Commit the patch:

```sh
dotnet run --project src/Buildout.Cli -- update \
  --page <page_id> \
  --revision "$REVISION" \
  --ops ops.json
```

Output:
```
Reconciled page <page_id>: 4 preserved, 0 updated, 1 new, 2 deleted
Revision: 2b3c4d5e
```

**Step 5** — Verify the result:

```sh
dotnet run --project src/Buildout.Cli -- get <page_id>
```

---

## Scenario 2 — CLI: pipe operations from stdin

When the operations list is small, skip the file:

```sh
dotnet run --project src/Buildout.Cli -- update \
  --page <page_id> \
  --revision 1a2b3c4d \
  --ops - <<'EOF'
[
  {
    "op": "search_replace",
    "old_str": "old text",
    "new_str": "new text"
  }
]
EOF
```

---

## Scenario 3 — MCP: LLM-native round-trip

From an MCP client (e.g., Claude or another LLM with tool access):

**Fetch** the page for editing:
```
tool: get_page_markdown
input: { "page_id": "<id>" }
```

Response:
```json
{
  "markdown": "<!-- buildin:root -->\n# My Page\n\n<!-- buildin:block:abc -->...",
  "revision": "1a2b3c4d",
  "unknown_block_ids": []
}
```

The LLM reads the anchored Markdown, identifies the anchor of the block it wants to change,
and composes the patch operations.

**Update** the page:
```
tool: update_page
input: {
  "page_id": "<id>",
  "revision": "1a2b3c4d",
  "operations": [
    {
      "op": "replace_block",
      "anchor": "abc",
      "markdown": "Updated paragraph content."
    }
  ]
}
```

Response:
```json
{
  "preserved_blocks": 7,
  "updated_blocks": 1,
  "new_blocks": 0,
  "deleted_blocks": 0,
  "ambiguous_matches": 0,
  "new_revision": "2b3c4d5e"
}
```

**Verify** by re-fetching:
```
tool: get_page_markdown
input: { "page_id": "<id>" }
```

The anchor `<!-- buildin:block:abc -->` is still present in the new snapshot (the block ID
was preserved because only its payload changed, not its position). The new revision
(`2b3c4d5e`) replaces the old one for the next edit cycle.
