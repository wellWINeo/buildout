# Contract: MCP `tree` tool

## Identity

- **Name**: `tree`
- **Handler**: `Buildout.Mcp.Tools.TreeToolHandler`
- **Description (attribute on the handler method)**: a single paragraph that
  states: the tool returns a hierarchical map of a buildin page or database
  and its descendant pages/databases; supports two `format` values (`ascii`
  default, `json`); accepts a `depth` of `1`–`7` (default `3`); skips content
  blocks; uses placeholder names `(untitled)` and `(unavailable)` per the
  spec.

## Parameters

| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `page_id` | `string` | yes | — | UUID of the root page or database. |
| `format` | `string` | no | `"ascii"` | One of `"ascii"`, `"json"`. |
| `depth` | `int` | no | `3` | Integer in `[1, 7]`. |

Parameter names use snake_case to match the rest of the MCP surface
(`get_page_markdown`, `search`, etc.). The handler suppresses CA1707 for these
parameter names exactly as the existing handlers do.

## Result

A single `string` content block:

- **`format=ascii`**: the same plain-text tree the CLI emits, with markdown
  links and box-drawing characters.
- **`format=json`**: a pretty-printed JSON document in the shape defined by
  [`service.md`](./service.md).

The MCP tool does not negotiate output format from any HTTP header — the
`format` parameter is the sole selector (FR-011).

## Errors

| Condition | Error code | Message shape |
|---|---|---|
| `depth` outside `[1, 7]` | `InvalidParams` | `"depth must be between 1 and 7 (inclusive); got {value}"` |
| `format` not `ascii` or `json` | `InvalidParams` | `"format must be 'ascii' or 'json'; got '{value}'"` |
| Root page or database not found | `InvalidParams` | `"page or database not found: {id}"` |
| Authentication failure | `InternalError` | `"Authentication error: {message}"` |
| Transport failure | `InternalError` | `"Transport error: {message}"` |
| Cycle detected | `InternalError` | `"cycle detected in page hierarchy at node {id}"` |
| Any other buildin error | `InternalError` | `"Unexpected buildin error: {message}"` |

All exceptions are caught and re-thrown as `McpProtocolException` instances,
matching the convention used by `GetPageMarkdownToolHandler`.

## Telemetry

The handler records, on both success and failure:

- `BuildoutMeter.McpToolInvocationsTotal` with tags
  `{ "tool": "tree", "outcome": "success"|"failure" }`
- `BuildoutMeter.McpToolDuration` with the same tag set.

This matches the convention every other MCP tool already follows.

## No new prompt

Per Principle VIII, an MCP tool only needs a corresponding named prompt when
its behavior is "complex enough to warrant detailed instructions". The `tree`
tool has three small parameters with clear contracts; its `[Description]`
attribute carries all the instruction an agent needs. The plan reserves the
right to add a `tree.md` prompt in a future PR if the surface grows (e.g.,
filters, multiple roots).
