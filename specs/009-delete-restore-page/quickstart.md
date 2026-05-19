# Quickstart — Page Delete and Restore

Three end-to-end scenarios exercising the two CLI commands and the two MCP tools.

Prerequisites: a configured `BUILDIN__BOT_TOKEN` and a buildin page the bot can edit.
Each scenario uses `<page_id>` as a placeholder for that page's UUID.

## Scenario 1 — Delete a page from the CLI

### 1.1 Soft-delete the page (state change)

```bash
buildout delete <page_id>
```

Expected stdout:

```
Deleted page <page_id>: archived=true (changed=true)
```

Expected exit code: `0`.

### 1.2 Verify with `get`

```bash
buildout get <page_id> --print summary | head -5
```

Buildin search and listing surfaces no longer return the page. The page's body content
(blocks, comments, backlinks) is preserved server-side; only `archived` flipped.

### 1.3 Idempotent re-delete (no-op)

```bash
buildout delete <page_id>
```

Expected stdout:

```
Deleted page <page_id>: archived=true (changed=false, no-op)
```

Expected exit code: `0`. **No PATCH was issued** — observable in the spec 007
metric series `buildout.operations.total{operation="page_delete", changed="false"}`.

### 1.4 JSON output mode

```bash
buildout delete <page_id> --print json
```

For a state-changing call:

```json
{"pageId":"<page_id>","archived":true,"changed":true}
```

For a no-op:

```json
{"pageId":"<page_id>","archived":true,"changed":false}
```

## Scenario 2 — Restore a page from the CLI

### 2.1 Un-archive the page deleted in scenario 1

```bash
buildout restore <page_id>
```

Expected stdout:

```
Restored page <page_id>: archived=false (changed=true)
```

Expected exit code: `0`. The page reappears in buildin's normal browse/search.

### 2.2 Pipe-style chaining (SC-004)

```bash
buildout delete <page_id> && buildout restore <page_id>
echo "Final exit: $?"
```

Both commands exit 0; the page ends up in its pre-script state (active). A clean
delete-then-restore round-trip leaves the page indistinguishable from its starting
state, except for an updated `last_edited_at` timestamp.

### 2.3 Error: page not found

```bash
buildout delete 00000000-0000-0000-0000-000000000000
```

Expected stderr:

```
Delete failed [NotFound]: Page 00000000-0000-0000-0000-000000000000 not found.
```

Expected exit code: `3`. The CLI does not retry.

## Scenario 3 — LLM-native round-trip via MCP

This scenario demonstrates the full LLM workflow against the buildout MCP server.

### 3.1 Server connection

The MCP server is spawned as a subprocess by the LLM host (Claude Desktop, an Agent SDK
runtime, etc.) per the spec 001 stdio transport.

### 3.2 LLM dialog (sketch)

```text
USER:    Please archive my page <page_id>. I want to clean up my workspace.
LLM:     I will use the delete_page tool.
         → tool call: delete_page({ page_id: "<page_id>" })
         ← tool result:
             ResourceLink: buildin://<page_id>
             Text: {"page_id":"<page_id>","archived":true,"changed":true}
LLM:     The page has been archived. It is no longer visible in browse views but its
         contents are preserved. You can restore it with restore_page later.

USER:    Actually, I changed my mind. Undo that.
LLM:     I will use restore_page to undo the delete.
         → tool call: restore_page({ page_id: "<page_id>" })
         ← tool result:
             ResourceLink: buildin://<page_id>
             Text: {"page_id":"<page_id>","archived":false,"changed":true}
LLM:     The page has been restored.
```

### 3.3 Cross-verification (SC-001/SC-002)

After `delete_page` returns, the LLM can call `get_page_markdown` (spec 008) for the
same page; the response's page-object will show `archived: true`. After `restore_page`,
the same call returns `archived: false`.

### 3.4 Idempotency (SC-003)

The LLM is safe to issue a second `delete_page` call on the same already-archived page;
the tool returns `{"page_id":"...","archived":true,"changed":false}` and no buildin
write is issued. The LLM can branch on `changed` to know whether to surface "I archived
the page" or "the page was already archived" to the user.

### 3.5 Error class observability (SC-005)

If the LLM passes a page ID it does not have permission to edit, the tool throws
`McpProtocolException` with `McpErrorCode.InternalError` and a message starting with
`Authentication error:`. If it passes a non-existent page ID, it throws
`McpProtocolException` with `McpErrorCode.ResourceNotFound`. The error codes are
distinguishable without parsing the human-readable message.
