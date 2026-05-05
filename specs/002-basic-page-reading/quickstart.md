# Quickstart — Initial Page Reading

How to demo this feature locally once it's implemented. This document is the
"Definition of Demonstrable" referenced by the verification step before merge.

## Prerequisites

- .NET 10 SDK installed (already required by feature 001).
- A buildin Bot API token in your environment:
  - `export BUILDOUT__BUILDIN__BOT_TOKEN=<your-token>`
  - (Configuration binding shape from feature 001; see
    `Buildout.Core/Buildin/BuildinClientOptions.cs`.)
- A page id you can read with that token. Any test page in your buildin
  workspace works; one with a heading, a list, a code block, and a
  paragraph is ideal for showing off the conversion.

Optional (for the cheap-LLM integration test):

- `export ANTHROPIC_API_KEY=<your-anthropic-key>` — without this, the
  `PageReadingLlmTests` are skipped (suite still passes).

## CLI demo

### Plain mode (pipe / redirect)

```bash
dotnet run --project src/Buildout.Cli -- get <page_id> > /tmp/page.md
cat /tmp/page.md
```

Expected:

- File starts with `# <page title>` followed by a blank line.
- Body contains rendered Markdown for the page's supported blocks
  (headings as `##`/`###`/`####`, lists, code fences, etc.).
- Unsupported blocks appear as `<!-- unsupported block: <type> -->`
  comments at their original position.
- File contains zero terminal escape codes.

### Styled mode (interactive terminal)

```bash
dotnet run --project src/Buildout.Cli -- get <page_id>
```

Expected:

- Headings render with bold emphasis.
- Lists render with proper bullets / numbering / task-list checkboxes.
- Fenced code blocks render in a panel with the language tag in the panel
  header.
- Block quotes render indented with a `│` glyph.
- Dividers render as horizontal rules.

### Error cases

```bash
# Non-existent page id
dotnet run --project src/Buildout.Cli -- get 00000000-0000-0000-0000-000000000000
echo "exit=$?"
# Expected: stderr "Page not found: 00000000-…"; exit=3

# Bad token (unset and retry)
unset BUILDOUT__BUILDIN__BOT_TOKEN
dotnet run --project src/Buildout.Cli -- get <page_id>
echo "exit=$?"
# Expected: stderr "Authentication failure: …"; exit=4
```

## MCP demo

The MCP server reads its config from environment variables (same Bot token).
Start the host directly over stdio for a quick smoke test using the SDK's
example client (`dotnet run --project samples/...`) or any MCP inspector tool.

```bash
dotnet run --project src/Buildout.Mcp
```

In a connected MCP client:

1. List resource templates → expect one entry with URI template
   `buildin://{page_id}`, name `buildin-page`, MIME `text/markdown`.
2. Read `buildin://<page_id>` for a known page → expect one text resource
   whose body is byte-identical to the CLI's plain-mode output for the same
   page.
3. Read `buildin://00000000-0000-0000-0000-000000000000` → expect an
   MCP error (resource-not-found code), not a 200 with an error blob.

## Verification checklist

Run before declaring the feature done:

- [ ] `dotnet test` passes with `BUILDOUT__BUILDIN__BOT_TOKEN` unset.
- [ ] Full feature test suite completes in < 30 s on the developer laptop
      (SC-006). With `ANTHROPIC_API_KEY` set, this includes the cheap-LLM
      test; the budget still holds.
- [ ] CLI plain output for a chosen fixture page is byte-identical to the
      MCP resource body for the same page (SC-003) — checked by
      `GetCommandTests.PlainOutputMatchesMcp`.
- [ ] Every supported block type listed in `data-model.md` has a passing
      conversion test (SC-004).
- [ ] Every unsupported block type listed in `data-model.md` has a passing
      placeholder test (SC-004).
- [ ] CLI exit codes 3 / 4 / 5 / 6 are exercised by integration tests
      (SC-007).
- [ ] Output is deterministic — `DeterminismTests` passes.
- [ ] No new commit introduces a network call to `api.buildin.ai` from any
      test (SC-006 spirit; check via the existing mock-only test
      configuration).
