# Quickstart: Tree Command

This guide walks through using the tree feature once it ships, end-to-end on
both the CLI and the MCP server. Assumes the buildin bot token is configured
via the standard configuration loader (`~/.config/buildout/config.json` or
`Buildout__BotToken` env var) — see `docs/configuration.md`.

## Prerequisites

- A buildin workspace you can read.
- The UUID of a page (or database) you want to map. Use
  `buildout-cli search "<keyword>"` if you need to find one.

## CLI

### Get an ASCII tree (default)

```bash
buildout-cli tree 11111111-2222-3333-4444-555555555555
```

Sample output:

```
[Engineering](<https://buildin.ai/...>)
├── [Onboarding](<https://buildin.ai/...>)
│   ├── [Day 1](<https://buildin.ai/...>)
│   └── [Day 2](<https://buildin.ai/...>)
└── [Runbooks](<https://buildin.ai/...>)
    └── [Incidents DB](<https://buildin.ai/...>)
```

You can paste this directly into a Markdown chat, doc, or README — the links
remain clickable.

### Switch to JSON

```bash
buildout-cli tree 11111111-2222-3333-4444-555555555555 --format json
```

Pipe through `jq` for further processing:

```bash
buildout-cli tree <id> --format json | jq '.children[].name'
```

### Limit depth

```bash
buildout-cli tree <id> --depth 1   # root + immediate children only
buildout-cli tree <id> --depth 7   # the deepest the command will go
```

`--depth 0` or `--depth 8` returns exit code `2` with a message naming the
valid range.

### Handle missing or inaccessible roots

```bash
buildout-cli tree 00000000-0000-0000-0000-000000000000
# Page or database not found: 00000000-0000-0000-0000-000000000000
# exit code 3
```

### Partial-tree behavior

If a descendant page or database can't be read mid-traversal (transient
buildin error, rate limit, permission gap), it appears inline:

```
[Engineering](<https://buildin.ai/...>)
├── [Onboarding](<https://buildin.ai/...>)
└── [(unavailable)](<>)
```

The command's exit code is still `0` — the partial result is the intended
behavior. Check the log output for the underlying failure on each
`(unavailable)` node.

## MCP

### Stdio (LLM agent / IDE)

The server exposes a `tree` tool. From an LLM agent the call looks like:

```jsonc
// MCP tools/call request
{
  "name": "tree",
  "arguments": {
    "page_id": "11111111-2222-3333-4444-555555555555",
    "format":  "ascii",
    "depth":   3
  }
}
```

The response content is a single string identical to the CLI's stdout for the
same parameters.

### HTTP

The HTTP transport accepts the same tool call. Note: per FR-011, the
`Accept` HTTP header is **not** honored as a format selector — the `format`
parameter is the only way to choose ASCII vs JSON.

### Errors

| Symptom | Cause | Fix |
|---|---|---|
| `InvalidParams: depth must be between 1 and 7 (inclusive); got N` | Out-of-range depth | Use a value in `[1, 7]`. |
| `InvalidParams: format must be 'ascii' or 'json'; got '<x>'` | Unknown format | Use `"ascii"` or `"json"`. |
| `InvalidParams: page or database not found: <id>` | Bad UUID or inaccessible root | Re-check the UUID; verify the bot has read access. |
| `InternalError: cycle detected in page hierarchy at node <id>` | Server-data anomaly | Report; the spec assumes the workspace tree is acyclic. |

## Validation checklist

Before merging the implementation:

- `buildout-cli tree <known_page> --depth 1` returns exactly the root and its
  direct children.
- `buildout-cli tree <known_page> --format json | jq -e '.children[0].children'`
  returns an array (possibly empty), never `null`.
- `buildout-cli tree <page_with_mixed_blocks>` shows only sub-pages and
  embedded databases — no paragraphs, headings, lists, callouts.
- `buildout-cli tree <database_id>` renders the database as the root and lists
  its records as children.
- All unit tests in `tests/Buildout.UnitTests/PageTree/` pass.
- The MCP integration test in `tests/Buildout.IntegrationTests/Mcp/TreeToolTests.cs`
  passes against the cheap LLM.
- `docs/configuration.md` is unchanged (no new options).
- `Buildout.Cli/Skills/SKILL.md` lists `tree` and references `tree.md`.
