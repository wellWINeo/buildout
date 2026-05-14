# Contract: `create_page` (MCP tool)

**Feature**: [spec.md](../spec.md) · [plan.md](../plan.md)

The MCP surface for page creation. Thin adapter over `IPageCreator`
(see `core-creator.md`). This is the **one** MCP tool in the project
that deliberately diverges from the byte-identical CLI/MCP body
invariant — its success response is a structured `ResourceLinkBlock`,
not a text body (spec FR-014).

---

## Tool descriptor

| Field | Value |
|---|---|
| `Name` | `create_page` |
| `Description` | `Create a new buildin page from a Markdown document. Returns a resource_link pointing at buildin://<new_page_id>.` |

## Input schema

| Field | Type | Required | Notes |
|---|---|---|---|
| `parent_id` | string | yes | Buildin page id or database id. |
| `markdown` | string | yes | Document body. May start with `# Title`. |
| `title` | string | no | Overrides leading-H1 title. |
| `icon` | string | no | Single emoji grapheme cluster, or `http(s)://` URL. |
| `cover_url` | string | no | `http(s)://` URL. |
| `properties` | object<string,string> | no | Only meaningful when `parent_id` resolves to a database. Same plain-string serialisations as CLI `--property` (R6). |

No `print` field. The response shape is fixed.

---

## Return shape

Handler signature:

```csharp
[McpServerToolType]
public sealed class CreatePageToolHandler
{
    [McpServerTool(Name = "create_page")]
    [Description("Create a new buildin page from a Markdown document. Returns a resource_link pointing at buildin://<new_page_id>.")]
    public async Task<CallToolResult> CreatePageAsync(
        [Description("Buildin page id or database id under which to create the new page.")] string parent_id,
        [Description("Markdown body of the new page. May start with a leading '# Title'.")] string markdown,
        [Description("Optional page title. Overrides leading '# Title' when set.")] string? title = null,
        [Description("Optional icon: a single emoji or an absolute URL.")] string? icon = null,
        [Description("Optional cover image URL.")] string? cover_url = null,
        [Description("Optional database-property values. Keys are property names; values are plain-string serialisations.")] IReadOnlyDictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);
}
```

On success the handler returns:

```csharp
new CallToolResult
{
    IsError = false,
    Content =
    [
        new ResourceLinkBlock
        {
            Uri  = $"buildin://{outcome.NewPageId}",
            Name = resolvedTitle,
        },
    ],
};
```

The MCP client (LLM or otherwise) recovers the new page id by stripping
the `buildin://` prefix from `Uri`, and can immediately read the page
back via the existing `buildin://{page_id}` resource registered by
`PageResourceHandler`.

---

## Error mapping

`McpProtocolException` is thrown with the codes below; the SDK
serialises them to the JSON-RPC error path. Mirrors the existing
mapping used by `SearchToolHandler` and `DatabaseViewToolHandler`.

| Failure class | MCP error code | Message |
|---|---|---|
| Validation | `InvalidParams` | The specific validation message from `cli-create.md`. |
| NotFound (probe) | `ResourceNotFound` | `Parent '<parent_id>' was not found as a page or a database.` |
| Auth (probe or write) | `InternalError` | `Authentication error: <buildin message>` |
| Transport | `InternalError` | `Transport error: <buildin message>` |
| Unexpected | `InternalError` | `Unexpected error: <buildin message>` |
| Partial creation | `InternalError` | `Partial creation: page <new_page_id> exists but appendBlockChildren failed after <K> of <N> top-level batches: <underlying message>` |

For the partial-creation case the new page id is recoverable from the
message body by the same shell rule as the CLI's stderr (R8) — the
first whitespace-separated token after `page` on the line.

---

## Deliberate divergence from CLI

Read tools in this project (`page` resource, `search`, `database_view`)
all return a text body byte-identical to their CLI plain-mode
counterparts. `create_page` does not, by design (spec FR-014
clarification, option D).

- CLI `--print id` returns `<new_page_id>\n` as plain stdout text.
- MCP `create_page` returns a `ResourceLinkBlock` whose `Uri` carries
  the same id.

Both surfaces carry the same *identifier*; the *wire form* differs.
Equivalence is asserted by
`tests/Buildout.IntegrationTests/Cross/CreatePageIdEquivalenceTests.cs`,
which extracts the id from each shape and compares.

---

## Examples

```jsonc
// Request
{
  "name": "create_page",
  "arguments": {
    "parent_id": "00000000-0000-0000-0000-0000000000aa",
    "markdown": "# My Page\n\nA paragraph.\n\n- one\n- two\n"
  }
}

// Response (success)
{
  "content": [
    {
      "type": "resource_link",
      "uri":  "buildin://00000000-0000-0000-0000-0000000000bb",
      "name": "My Page"
    }
  ],
  "isError": false
}

// Response (validation error — JSON-RPC error path)
{
  "code":    -32602,                  // InvalidParams
  "message": "Cannot determine the new page's title: no leading '# Title' heading found and --title was not provided."
}
```
