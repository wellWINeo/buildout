# Quickstart — Page Search

How to demo this feature locally once it's implemented. This document is
the "Definition of Demonstrable" referenced by the verification step
before merge.

## Prerequisites

- .NET 10 SDK installed (already required by feature 001).
- A buildin Bot API token in your environment:
  - `export BUILDOUT__BUILDIN__BOT_TOKEN=<your-token>`
- At least one page in your workspace whose title or body contains a
  query string you can grep for (e.g. a page titled "Quarterly
  Revenue").

Optional (for the cheap-LLM integration test that chains search →
read):

- `export ANTHROPIC_API_KEY=<your-anthropic-key>` — without this, the
  `LlmCanFindAndReadPage` test is skipped.

## CLI demo

### Plain mode (pipe / redirect)

```bash
dotnet run --project src/Buildout.Cli -- search "quarterly revenue"
```

Each line is `<page_id>\t<object_type>\t<title>`:

```text
6f1b2a3c-1234-4abc-8def-0123456789ab	page	Quarterly Revenue Report
9c8d7e6f-5432-4abc-8def-fedcba987654	page	Marketing Plan (Q3)
```

Pipe to `awk` to extract IDs:

```bash
dotnet run --project src/Buildout.Cli -- search "quarterly revenue" \
    | awk -F'\t' '$2=="page" {print $1}'
```

Chain with `buildout get` to read the first match:

```bash
dotnet run --project src/Buildout.Cli -- search "quarterly revenue" \
    | head -1 \
    | awk -F'\t' '{print $1}' \
    | xargs -I{} dotnet run --project src/Buildout.Cli -- get {}
```

This is the SC-008 demonstration line. It must work end-to-end without
further configuration.

### Styled mode (interactive terminal)

```bash
dotnet run --project src/Buildout.Cli -- search "quarterly revenue"
```

When stdout is a TTY, the same matches render in a Spectre.Console
table with three columns (ID / Type / Title). Zero matches render a
single `No matches.` styled line.

### Scoped search

```bash
dotnet run --project src/Buildout.Cli -- search "Q3" --page <root_page_id>
```

Only pages that are `<root_page_id>` itself or descendants of it appear
in the output.

### Failure modes

| Scenario | Expected exit code | Stderr |
|---|---|---|
| `buildout search ""` | 2 | `Query must be non-empty.` |
| `buildout search foo --page <missing>` | 3 | `Page not found: <missing>` |
| Bad token | 4 | `Authentication failure: …` |
| Buildin host unreachable | 5 | `Transport failure: …` |
| Other buildin error | 6 | `Unexpected buildin error: …` |

## MCP demo

Start the MCP server over stdio (this is how Claude Desktop, Claude
Code's MCP integration, and any MCP-capable LLM client invoke it):

```bash
dotnet run --project src/Buildout.Mcp
```

From an MCP client:

1. **List tools**: should advertise a `search` tool with the
   description from `contracts/mcp-search-tool.md`.
2. **Invoke**: `search({ query: "quarterly revenue" })` returns a
   single text content block whose body matches the CLI plain-mode
   output for the same query.
3. **Chain with the resource**: pick a page id from a result line and
   read `buildin://<page_id>` (the resource template from feature
   002). The two surfaces compose: search to discover, resource to
   read.

The MCP `search` tool's body and the CLI plain-mode stdout are
**byte-identical** for the same query and same buildin response. This
is verified automatically by `SearchToolTests.ToolResultBody_
EqualsCliPlainBody`; the human demo can compare them with `diff`:

```bash
dotnet run --project src/Buildout.Cli -- search "quarterly revenue" \
    > /tmp/cli-search.txt
# (drive the MCP tool from a client, save the body to /tmp/mcp-search.txt)
diff /tmp/cli-search.txt /tmp/mcp-search.txt   # → no output (identical)
```

## Test suite

Run the full feature test suite locally:

```bash
dotnet test
```

Expected: all tests green. With `ANTHROPIC_API_KEY` set, the
cheap-LLM `LlmCanFindAndReadPage` test runs and asserts the LLM
chains `search` → `buildin://`. Without the key, that single test is
reported as skipped; everything else still passes. The cumulative
suite (features 001 + 002 + 003) completes well under 30 seconds on a
developer laptop with no buildin network.

## Definition of done

The feature is demonstrable iff:

- [ ] `buildout search "<some-real-query>"` returns at least one
      match for a known query, in plain TSV form, with no escape
      codes.
- [ ] `buildout search "<some-real-query>"` in a TTY renders a
      `Spectre.Console.Table` with bordered columns.
- [ ] `buildout search "<query>" --page <root>` strictly reduces the
      result set vs unscoped.
- [ ] `buildout search "" → exit 2` and the message reaches stderr.
- [ ] The CLI plain-mode output and MCP tool body are byte-identical
      for the same query (verified by automated test, demonstrable by
      `diff`).
- [ ] `dotnet test` passes; `LlmCanFindAndReadPage` either passes
      (key present) or is reported as skipped (key absent).
- [ ] `git diff` shows zero changes inside
      `src/Buildout.Core/Buildin/Generated/`.
