# Contract — MCP resource `buildin://{page_id}`

The first MCP surface to land in buildout. Exposed by `Buildout.Mcp` and
backed by `IPageMarkdownRenderer`.

## Resource template

| Field | Value |
|---|---|
| URI scheme + template | `buildin://{page_id}` |
| Template type | Resource template (paramterized URI), advertised in `resources/templates/list`. |
| `name` | `buildin-page` |
| `description` | `Read a buildin page rendered as CommonMark / GFM Markdown. The page title appears as the H1; supported blocks include paragraphs, headings, lists, todos, code, quotes, and dividers. Unsupported blocks (images, embeds, tables, child pages, etc.) are replaced by visible placeholder comments at their original position.` |
| `mimeType` | `text/markdown` |

`{page_id}` is the buildin page id in any form `IBuildinClient` accepts.

## Read response

| Aspect | Value |
|---|---|
| Number of resource contents returned | Exactly one |
| Content kind | Text (`TextResourceContents`) |
| `mimeType` | `text/markdown` |
| `uri` | The exact request URI (`buildin://<page_id>`), echoed |
| `text` | The output of `IPageMarkdownRenderer.RenderAsync(pageId, ct)` |

The body is byte-identical to what `Buildout.Cli` writes to stdout in non-TTY
mode for the same page (FR-008, SC-003).

## Error mapping (FR-012)

The handler catches `BuildinApiException` from the renderer and maps the
typed `BuildinError` to an MCP-protocol error (returned as a JSON-RPC
`error`, not a 200 with a body):

| `BuildinError` | MCP error | Message hint |
|---|---|---|
| `NotFound` (404) | `-32002` (resource-not-found) | `"Page <page_id> not found."` |
| `Unauthorized` / `Forbidden` (401/403) | `-32603` (internal) with explicit message | `"Authentication failure: <buildin message>."` Distinct from transport. |
| `TransportError` (network, timeout, 5xx) | `-32603` (internal) | `"Transport failure contacting buildin: <message>."` |
| `UnknownError` / unmapped `ApiError` | `-32603` (internal) | `"Unexpected buildin response: <message>."` |

Cancellation (`OperationCanceledException`) is surfaced as the SDK's standard
cancellation error and is not caught by the handler.

## Implementation shape

Located at `src/Buildout.Mcp/Resources/PageResourceHandler.cs`.

```text
internal sealed class PageResourceHandler
{
    private readonly IPageMarkdownRenderer _renderer;
    private readonly ILogger<PageResourceHandler> _logger;

    public PageResourceHandler(IPageMarkdownRenderer renderer,
                               ILogger<PageResourceHandler> logger) { … }

    [McpServerResource(UriTemplate = "buildin://{page_id}",
                       Name = "buildin-page",
                       MimeType = "text/markdown")]
    public async Task<ReadResourceResult> ReadAsync(
        string page_id,
        CancellationToken ct)
    { … }
}
```

(Exact attribute / API names follow `ModelContextProtocol` 1.2.0; if the SDK
prefers builder-style registration, the handler's public method shape is
unchanged and `Program.cs` registers it via `WithResource(...)` instead.)

`Program.cs` is updated to:

1. Build the host with `Microsoft.Extensions.Hosting`.
2. Register `Buildout.Core` services via `AddBuildoutCore(...)`.
3. Register the MCP server with the SDK and the `PageResourceHandler`.
4. Run with the stdio transport by default; HTTP transport selectable by
   future config (out of scope for this feature — but the registration code
   path leaves room for it, satisfying the constitution's "both transports
   MUST be supported" with a one-line addition later).

## Test obligations

| Test class | Path | Purpose |
|---|---|---|
| `PageResourceTests.ListsTemplate` | `tests/Buildout.IntegrationTests/Mcp/PageResourceTests.cs` | The MCP server advertises one resource template `buildin://{page_id}` with the documented name, description, and MIME type. |
| `PageResourceTests.ReadHappyPath` | (same file) | Reading `buildin://<known-id>` returns one text resource with `text/markdown` MIME type and body matching the renderer's output for the same fixture. |
| `PageResourceTests.ReadNotFound` | (same file) | When the renderer throws `BuildinApiException(NotFound)`, the server returns an MCP error with the `-32002` code (or SDK's resource-not-found equivalent), not a 200. |
| `PageResourceTests.ReadAuthFailure` | (same file) | Authentication failures map to a distinct MCP error with the documented message, not transport. |
| `PageResourceTests.ReadTransportFailure` | (same file) | Transport failures map to a generic internal error distinct from auth and not-found. |
| `PageReadingLlmTests.HaikuAnswersSupportedBlockQuestions` | `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs` | One Haiku 4.5 call. Prompt: the rendered Markdown plus a question per supported block type. Asserts the LLM's answers contain the expected facts (per spec SC-002). Skipped if `ANTHROPIC_API_KEY` unset. |

The MCP integration tests use the SDK's in-process server/client harness (R12).
The cheap-LLM test uses the in-process MCP client to read the resource, then
sends the result to Anthropic — exactly one Haiku call total per test run.

## Out-of-scope (deferred)

- HTTP transport wiring (constitution-mandated to exist; mechanical to add
  later).
- Resource discovery beyond the single page template (e.g. listing all
  accessible pages) — a separate feature.
- Page metadata in the resource body (timestamps, author, parent chain) —
  spec assumption.
