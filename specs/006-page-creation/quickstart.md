# Quickstart: Page Creation from Markdown

**Feature**: [spec.md](./spec.md) · [plan.md](./plan.md)

Three short paths from a fresh checkout to a created page.

---

## 1. Author a Markdown file and create the page (CLI)

```sh
# Author
cat > notes.md <<'EOF'
# Meeting notes — 2026-05-13

- Topic A
- Topic B

```text
some code
```
EOF

# Set the bot token (same env var features 001–005 use)
export Buildin__BotToken=<your-bot-token>

# Create under an existing page parent
dotnet run --project src/Buildout.Cli -- \
    create --parent <page_id> notes.md
# → prints the new page id on stdout

# Read it back to confirm what was written
dotnet run --project src/Buildout.Cli -- get <new_id>
```

## 2. Pipe markdown directly (CLI, stdin)

```sh
echo "# Hello\n\nWorld" |
    dotnet run --project src/Buildout.Cli -- create --parent <page_id> -
```

Use `-` as the positional argument to read Markdown from stdin (capped
at 16 MiB).

## 3. Create from an MCP client

From a process speaking MCP (e.g. an LLM agent), call the new
`create_page` tool:

```jsonc
{
  "name": "create_page",
  "arguments": {
    "parent_id": "<page_id_or_database_id>",
    "markdown":  "# Hello\n\nWorld"
  }
}
```

The successful response is a single `resource_link` content block
whose `uri` is `buildin://<new_page_id>`. Pipe that URI into the
existing `buildin://{page_id}` resource to read the page back —
the two MCP calls together form a full round-trip from authored
Markdown to rendered Markdown.

---

## Run the tests

```sh
# Markdown→blocks unit tests + round-trip suite
dotnet test tests/Buildout.UnitTests

# WireMock integration (mock buildin) — runs `create` and `create_page` end-to-end
dotnet test tests/Buildout.IntegrationTests
```

All tests run offline. No real buildin host is contacted (constitution
Principle IV; spec FR-016).
