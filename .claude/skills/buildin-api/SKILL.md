# Buildin API Direct Call Skill

Use this skill to make raw HTTP calls to the Buildin REST API — to inspect
responses, verify MCP tool output matches the underlying data, or debug
unexpected behavior.

Base URL: `https://api.buildin.ai`  
Auth: `Authorization: Bearer <token>`

---

## Token Resolution

Read the token in this order:

1. **Env var**: `$Buildout__BotToken`
2. **Config file**: `~/.config/buildout/config.json` → `BotToken` field
3. **Flag**: `--config` path passed to `buildout-mcp`

Extract from config file:

```bash
TOKEN=$(python3 -c "import json,sys; print(json.load(open('$HOME/.config/buildout/config.json'))['BotToken'])")
# or
TOKEN=$(jq -r '.BotToken' ~/.config/buildout/config.json)
```

Throughout this skill, `$TOKEN` refers to the resolved bot token.

---

## Endpoints

### Verify token — `GET /v1/users/me`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  https://api.buildin.ai/v1/users/me | jq .
```

Expected: `{"object":"user","id":"...","type":"person",...}`. 401 means bad token.

---

### Search pages — `POST /v1/search`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -X POST https://api.buildin.ai/v1/search \
  -d '{"query":"","page_size":10}' | jq .
```

Response shape: `{object:"list", results:[{id, object:"page", properties:{title:{...}}}], has_more, next_cursor}`

Compare with MCP `search` tool: the MCP formatter converts this to
`<id>\t<object_type>\t<title>` lines.

---

### Get page metadata — `GET /v1/pages/{page_id}`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  https://api.buildin.ai/v1/pages/$PAGE_ID | jq .
```

Returns page object with `id`, `archived`, `properties`, `parent`, `url`.

---

### Get page blocks (content) — `GET /v1/blocks/{page_id}/children`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  "https://api.buildin.ai/v1/blocks/$PAGE_ID/children?page_size=100" | jq .
```

Returns `{results:[{id, type, data:{rich_text:[...]}, has_children, ...}]}`.
This is the raw block tree that `get_page_markdown` converts to Markdown.

To fetch all children recursively for a deep page:

```bash
START=""
while true; do
  RESP=$(curl -s -H "Authorization: Bearer $TOKEN" \
    "https://api.buildin.ai/v1/blocks/$PAGE_ID/children?page_size=100${START:+&start_cursor=$START}")
  echo "$RESP" | jq '.results[]'
  HAS_MORE=$(echo "$RESP" | jq -r '.has_more')
  [ "$HAS_MORE" = "false" ] && break
  START=$(echo "$RESP" | jq -r '.next_cursor')
done
```

---

### Archive (soft-delete) a page — `PATCH /v1/pages/{page_id}`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -X PATCH https://api.buildin.ai/v1/pages/$PAGE_ID \
  -d '{"archived":true}' | jq '{id:.id, archived:.archived}'
```

Restore: same call with `"archived":false`.

---

### Create a page — `POST /v1/pages`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -X POST https://api.buildin.ai/v1/pages \
  -d '{
    "parent": {"type":"page_id","page_id":"'$PARENT_ID'"},
    "properties": {
      "title": {
        "type":"title",
        "title":[{"type":"text","text":{"content":"API Test Page"}}]
      }
    }
  }' | jq '{id:.id, archived:.archived}'
```

---

### Get a database — `GET /v1/databases/{database_id}`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  https://api.buildin.ai/v1/databases/$DB_ID | jq '{id:.id, title:.title[0].plain_text}'
```

---

### Query database records — `POST /v1/databases/{database_id}/query`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -X POST https://api.buildin.ai/v1/databases/$DB_ID/query \
  -d '{"page_size":50}' | jq '.results | length'
```

---

### Append blocks to a page — `PATCH /v1/blocks/{page_id}/children`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -X PATCH https://api.buildin.ai/v1/blocks/$PAGE_ID/children \
  -d '{
    "children": [
      {"type":"paragraph","data":{"rich_text":[{"type":"text","text":{"content":"Hello from API"}}]}}
    ]
  }' | jq '.results[0].id'
```

---

### Delete (archive) a block — `DELETE /v1/blocks/{block_id}`

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  -X DELETE https://api.buildin.ai/v1/blocks/$BLOCK_ID | jq .
```

---

## Comparing API Output vs MCP Tool Output

### `search` parity check

```bash
# Raw API
curl -s -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -X POST https://api.buildin.ai/v1/search \
  -d '{"query":"<term>"}' \
  | jq -r '.results[] | "\(.id)\t\(.object)\t\(.properties.title.title[0].plain_text // "(untitled)")"'

# MCP tool (via skill mcp-qa-test, or call directly)
# mcp__buildin__search query="<term>"
```

Both should produce matching `<id>\t<type>\t<title>` lines (same IDs, same
order). Minor whitespace differences are acceptable.

### `get_page_markdown` — verify Markdown reflects block content

1. Call `GET /v1/blocks/$PAGE_ID/children` and note the `rich_text` content of
   each block.
2. Call `mcp__buildin__get_page_markdown page_id=$PAGE_ID`.
3. Confirm each block's plain text appears somewhere in the `Markdown` field.

### `database_view` — verify record count

```bash
# Raw count
curl -s -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -X POST https://api.buildin.ai/v1/databases/$DB_ID/query \
  -d '{"page_size":100}' | jq '.results | length'

# MCP output: count rows in the rendered table
```

The table row count should equal (or be consistent with) the API result count.

---

## Error Codes

| HTTP status | API error code | Meaning |
|-------------|---------------|---------|
| 400 | `validation_error` | Bad request body |
| 401 | `unauthorized` | Missing or invalid token |
| 403 | `forbidden` | Token lacks permission |
| 404 | `not_found` | Page/block/database not found |
| 429 | `rate_limit` | Slow down |
| 500 | `internal_error` | Server error |

All errors return `{"object":"error","status":<n>,"code":"<code>","message":"<text>"}`.
