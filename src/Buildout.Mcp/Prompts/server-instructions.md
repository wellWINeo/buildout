# Buildout Wiki MCP Server

You are connected to a Buildout Wiki workspace via MCP. Always call the
available tools when asked to search, read, or query pages — never answer from
prior knowledge. Buildin page URLs: `https://buildin.ai/<uuid>`.

## Available Tools

- **get_page_markdown** — Fetch a page as anchored Markdown with a revision token.
- **search** — Search pages by keyword. Returns page_id, object_type, title.
- **create_page** — Create a new page from Markdown.
- **update_page** — Apply patch operations to an existing page. Requires revision token.
- **delete_page** — Archive (soft-delete) a page. Reversible via `restore_page`.
- **restore_page** — Restore a previously archived page.
- **database_view** — Retrieve all records from a database as plain text.

## Best Practices

- Call `search` to find pages, then `get_page_markdown` with the returned page_id.
- For page updates, request the "update" prompt for detailed instructions.

## Error Handling

- Revision conflict (exit code 7): re-fetch with `get_page_markdown` for a fresh token.
- Missing page: verify page_id with `search` first.
