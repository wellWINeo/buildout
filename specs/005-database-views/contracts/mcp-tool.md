# Contract: `database_view` MCP Tool (Buildout.Mcp)

## Tool surface

```text
Tool name: database_view
Description: Render a buildin database as plain text in a chosen view
             style. Read-only. View styles are produced client-side
             from the rows returned by the database query endpoint.

Arguments:
  database_id     (string, required)    The buildin database id.
  style           (string, optional)    One of: table, board, gallery,
                                         list, calendar, timeline.
                                         Defaults to "table".
  group_by        (string, optional)    Property name to group by.
                                         Required when style="board".
  date_property   (string, optional)    Property name carrying a date.
                                         Required when style is
                                         "calendar" or "timeline".
```

## Registration

In `src/Buildout.Mcp/Program.cs`, append the new handler to the
existing builder chain:

```csharp
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithResources<PageResourceHandler>()
    .WithTools<SearchToolHandler>()
    .WithTools<DatabaseViewToolHandler>();
```

## Handler shape

```csharp
[McpServerToolType]
public sealed class DatabaseViewToolHandler
{
    private readonly IDatabaseViewRenderer _renderer;

    public DatabaseViewToolHandler(IDatabaseViewRenderer renderer)
        => _renderer = renderer;

    [McpServerTool(Name = "database_view")]
    [Description("Render a buildin database as plain text in a chosen view style. Read-only.")]
    public async Task<string> RenderAsync(
        string database_id,
        string? style = null,
        string? group_by = null,
        string? date_property = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DatabaseViewRequest(
                database_id,
                ParseStyle(style),         // throws DatabaseViewValidationException
                group_by,
                date_property);
            return await _renderer.RenderAsync(request, cancellationToken);
        }
        catch (DatabaseViewValidationException ex)
        {
            throw new McpProtocolException(McpErrorCode.InvalidParams, ex.Message);
        }
        catch (BuildinApiException ex)
        {
            throw MapBuildinError(ex);     // see error-class table below
        }
    }
}
```

## Byte-identity contract

For any inputs `(database_id, style, group_by, date_property)`,
the string returned by this tool MUST equal — byte-for-byte — the
plain-mode output produced by the equivalent CLI invocation
`buildout db view <database_id> [--style ...] [...]` against the
same fixture (no trailing newline differences, no whitespace
fixups, no encoding differences). Verified by the parity test in
`tests/Buildout.IntegrationTests/Cross/DatabaseViewParityTests.cs`.

## Error-class mapping (reused from existing MCP tools)

| Cause                                        | MCP error code        |
|----------------------------------------------|-----------------------|
| `DatabaseViewValidationException`            | `InvalidParams`       |
| Buildin 404                                  | `ResourceNotFound`    |
| Buildin 401 / 403                            | `InternalError` (existing convention; matches `PageResourceHandler`) |
| Buildin transport / timeout                  | `InternalError`       |
| Generic `BuildinApiException`                | `InternalError`       |
| Cancellation                                 | propagated as-is      |

## Read-only declaration

The tool description MUST state that the operation is read-only and
that it follows pagination to exhaustion. This is part of the
contract because the constitution (Principle VI) requires
destructive operations to advertise themselves; the symmetric
guarantee — read-only operations advertising as such — is the
natural extension and helps LLM callers reason about safety.
