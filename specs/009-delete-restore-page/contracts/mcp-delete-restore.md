# Contract — MCP: `delete_page` and `restore_page` tools

## Tool surface

### `delete_page`

| Field | Value |
|--|--|
| Tool name | `delete_page` |
| Input parameter | `page_id: string` (required) |
| Description | "Archive (soft-delete) a buildin page. The page becomes hidden from normal browse and search; its blocks, comments, and backlinks are preserved. Reversible via the `restore_page` tool." |

### `restore_page`

| Field | Value |
|--|--|
| Tool name | `restore_page` |
| Input parameter | `page_id: string` (required) |
| Description | "Restore (un-archive) a previously archived buildin page. The page becomes visible to normal browse and search again. Use this tool to undo a previous `delete_page` call." |

The descriptions are deliberately discriminating (see research.md R7): each names its
direction, names its inverse, and surfaces synonyms ("archive" on the delete side,
"un-archive" on the restore side) that LLMs will emit in user prompts.

## Tool-handler classes

```csharp
namespace Buildout.Mcp.Tools;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Buildout.Core.PageLifecycle;

[McpServerToolType]
public sealed class DeletePageToolHandler
{
    private readonly IPageLifecycle _lifecycle;

    public DeletePageToolHandler(IPageLifecycle lifecycle) => _lifecycle = lifecycle;

    [McpServerTool(Name = "delete_page")]
    [Description("Archive (soft-delete) a buildin page. ... Reversible via the `restore_page` tool.")]
    public async Task<CallToolResult> DeletePageAsync(
        [Description("Buildin page id to archive.")] string page_id,
        CancellationToken cancellationToken = default);
}
```

`RestorePageToolHandler` is the symmetric counterpart. Both handler classes call into
`IPageLifecycle.DeleteAsync` / `RestoreAsync` respectively and translate the outcome.

## Result shape

On success, each tool returns:

```csharp
new CallToolResult
{
    IsError = false,
    Content =
    [
        new ResourceLinkBlock
        {
            Uri = $"buildin://{outcome.PageId}",
            Name = outcome.PageId, // resolved title is not re-fetched
        },
        new TextContentBlock
        {
            Text = JsonSerializer.Serialize(new
            {
                page_id = outcome.PageId,
                archived = outcome.Archived,   // true after delete, false after restore
                changed = outcome.Changed,
            }),
        },
    ],
};
```

The two-block content array is deliberate (research.md R5): the resource link supports
chained tool calls; the text block carries the JSON payload the LLM reads.

## Error mapping

On `outcome.FailureClass != null`, the handler throws `McpProtocolException`:

| `FailureClass` | `McpErrorCode` | Message format |
|--|--|--|
| `NotFound` | `ResourceNotFound` | `"Page '{page_id}' was not found."` |
| `Auth` | `InternalError` | `$"Authentication error: {underlyingException.Message}"` |
| `Transport` | `InternalError` | `$"Transport error: {underlyingException.Message}"` |
| `Unexpected` | `InternalError` | `$"Unexpected error: {underlyingException.Message}"` |

Matches `CreatePageToolHandler`'s mapping for spec 006 — no new MCP error codes are
introduced.

## Tool-level instrumentation

Both handlers wrap the lifecycle call in a `Stopwatch` and emit the
`BuildoutMeter.McpToolInvocationsTotal` + `McpToolDuration` instruments with `tool` and
`outcome` tags, matching `CreatePageToolHandler` (`tool=delete_page|restore_page`,
`outcome=success|failure`). No new metric is introduced.

## Registration

In `Buildout.Mcp.Program.cs`:

```csharp
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithResources<PageResourceHandler>()
    .WithTools<SearchToolHandler>()
    .WithTools<DatabaseViewToolHandler>()
    .WithTools<CreatePageToolHandler>()
    .WithTools<GetPageMarkdownToolHandler>()
    .WithTools<UpdatePageToolHandler>()
    .WithTools<DeletePageToolHandler>()        // NEW
    .WithTools<RestorePageToolHandler>();      // NEW
```

DI registration is automatic — `[McpServerToolType]` classes are picked up by the SDK's
service-scan. The class is `sealed` per the spec 006 convention.

## Verification

- `DeletePageToolTests` (MCP integration) covers: success state-change, success no-op,
  every error class → correct `McpErrorCode`, content blocks shape, instrumentation tags.
- `RestorePageToolTests` is the symmetric counterpart.
- `ToolSelectionWithCheapLlmTests` extends the spec 007/008 fixture with the 10-prompt
  benchmark from research.md R7 and asserts ≥ 9/10 correct selections (SC-006).
- `DeleteRestoreSymmetryTests` (Cross) compares the JSON in the `TextContentBlock` to
  the CLI's `--print json` output for the same page state (SC-004).
