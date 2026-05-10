# Quickstart: Database Views

## Render a database as a table (default)

```bash
dotnet run --project src/Buildout.Cli -- db view <database_id>
```

You'll see a header line, an empty line, and a markdown pipe-table
listing every row in the database (paginated to exhaustion under the
hood). Pipe to a file or another command to get plain text with no
terminal escape codes:

```bash
dotnet run --project src/Buildout.Cli -- db view <database_id> > view.md
```

## Switch view style

```bash
# Kanban-like board grouped by a select property
dotnet run --project src/Buildout.Cli -- db view <database_id> --style board --group-by Status

# Card-style listing
dotnet run --project src/Buildout.Cli -- db view <database_id> --style gallery

# One-liner per row
dotnet run --project src/Buildout.Cli -- db view <database_id> --style list

# Date-grouped list (calendar fallback)
dotnet run --project src/Buildout.Cli -- db view <database_id> --style calendar --date-property Due

# Date-range bands (timeline fallback)
dotnet run --project src/Buildout.Cli -- db view <database_id> --style timeline --date-property Phase
```

If you pass an unknown style, an unknown `--group-by` property, or
an unknown `--date-property`, the command exits with code 2 (no
network call is made).

## Same operation over MCP

From an MCP client:

```jsonc
{
  "name": "database_view",
  "arguments": {
    "database_id": "<id>",
    "style": "board",
    "group_by": "Status"
  }
}
```

The body returned is byte-identical to the CLI's plain-mode output
for the same arguments — diff them as a sanity check:

```bash
diff <(dotnet run --project src/Buildout.Cli -- db view ID --style board --group-by Status \
      | cat) \
     <(your-mcp-client call database_view database_id=ID style=board group_by=Status)
```

(Both should produce empty output.)

## Embedded databases inside a page

If a page contains a `child_database` block (a database embedded
inside the page's contents), reading the page now expands that
block inline as a table-style view:

```bash
dotnet run --project src/Buildout.Cli -- get <page_id>
```

You'll see the page's normal markdown, with each `child_database`
block replaced by a `## <database title>` heading and the embedded
database rendered as a markdown pipe-table beneath it. The same
expansion happens when an MCP client reads the
`buildin://<page_id>` resource. Inline expansion always uses the
table style — there are no flags on `get` or on the page resource
that change it.

If an embedded database can't be read (404 / 401 / 403 / transport
error), the surrounding page still renders, with a single-line
placeholder like `[child database: not accessible — <title>]`
substituted at that block's position. Database mentions inside
page text continue to render as references and are not expanded.

## Test it locally

```bash
dotnet test
```

The integration tests run against the WireMock buildin fixture from
feature 004; no real buildin token is needed and no real network
calls are made. The test suite includes:

- A unit test per view style with golden-string assertions.
- An integration test per surface (CLI and MCP).
- A parity test asserting CLI plain output == MCP body.
- A read-only test that fails loudly if rendering ever invokes any
  buildin endpoint other than `GET /v1/databases/{id}` or
  `POST /v1/databases/{id}/query`.
