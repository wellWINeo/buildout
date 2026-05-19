# Phase 0 Research — Page Delete and Restore

## R1: `UpdatePageRequest` PATCH semantics — archived-only body is non-destructive

**Decision**: Send `PATCH /v1/pages/{page_id}` with a body whose only populated field is
`archived`. The body MUST NOT include `properties`, `icon`, or `cover`.

**Rationale**:

- `openapi.json:1030–1037` defines `UpdatePageRequest` with every field marked optional and
  no `nullable: true` declarations on `properties`/`icon`/`cover`. The Notion-family
  convention this API follows is "only fields present in the request are touched". Omitted
  fields are left as-is.
- The Kiota-generated `Buildout.Core.Buildin.Generated.Models.UpdatePageRequest` represents
  every field as a nullable C# property; the serializer only emits properties whose value
  is non-null. Setting `request.Archived = true` and leaving other properties at their
  default (null) produces a JSON body containing exactly `{"archived": true}`.
- This is the same primitive spec 008's `Reconciler` relies on when it calls
  `UpdateBlockAsync` with `data: null` to flip only `archived`. The pattern is established.

**Alternatives considered**:

- *Send a full `UpdatePageRequest` echoing the previously-fetched properties* — rejected:
  it duplicates server state into a write, increases payload size, and risks
  property-format drift between the read and write cycles.
- *Use a `DELETE` HTTP verb against `/v1/pages/{page_id}`* — rejected: the OpenAPI document
  does not declare it (only `GET` and `PATCH` exist for that path), so it would 404.

**Verification path**: `PageLifecycleTests` asserts on the serialised request body shape
sent through a recording `IBuildinClient` substitute; an integration test against WireMock
asserts the response page still carries its original `properties` after a delete/restore
cycle.

## R2: `FailureClass` enum location — reuse the existing enum

**Decision**: `PageLifecycleOutcome` references the existing
`Buildout.Core.Markdown.Authoring.FailureClass` enum via a `using` statement. The enum is
not moved, renamed, or duplicated.

**Rationale**:

- The enum vocabulary (`Validation`, `NotFound`, `Auth`, `Transport`, `Unexpected`,
  `Partial`) is already adequate. Page lifecycle ops use `NotFound`, `Auth`, `Transport`,
  `Unexpected` — never `Validation` (no body to validate beyond `page_id`) and never
  `Partial` (the op is a single PATCH, atomic at the server).
- Moving the enum into a shared `Buildout.Core.Common` namespace is a multi-file refactor
  that touches `CreatePageOutcome`, `CreatePageToolHandler`, every spec 006 test, and the
  CLI `CreateCommand`. None of that is required by feature 009; bundling it would expand
  scope without changing behaviour. The constitution's spec-kit-driven workflow prefers
  small specs over omnibus refactors.

**Alternatives considered**:

- *Move the enum to `Buildout.Core/Common/FailureClass.cs`* — rejected: scope creep
  outside the feature. A future "shared error taxonomy" spec can do this cleanly.
- *Define a separate `LifecycleFailureClass` enum* — rejected: duplicates an enum that has
  the right values; forces presentation-layer code to switch on two enum types when
  unifying error rendering across `CreateCommand`/`DeleteCommand`/`RestoreCommand`.

**Verification path**: compilation. The `PageLifecycleOutcome.FailureClass` property is
typed as `FailureClass?`; references resolve to the existing type.

## R3: Operation-name choice for `OperationRecorder`

**Decision**: Use `page_delete` and `page_restore` as the `operation` label values on
spec 007's existing `buildout.operations.total` and `buildout.operation.duration`
instruments.

**Rationale**:

- Spec 007's convention is lowercase snake-case verb names (`page_create`, `page_read`,
  `page_update`, `database_view_render`, `search`, `mcp.tool.<name>`). `page_delete` /
  `page_restore` fit the established pattern.
- Two separate operation names (rather than one `page_archive` with a direction tag) lets
  dashboards filter deletes vs restores with a flat label match instead of a compound
  predicate.
- Operation names appear in log lines through `OperationRecorder`'s
  `Operation {Operation} completed/failed` messages, so users grepping
  `Operation page_delete failed` will find delete failures without parsing tag values.

**Alternatives considered**:

- *Single operation name `page_archive` with `direction=delete|restore` tag* — rejected:
  more fragile for dashboard queries, and "archive" is the buildin-side concept, not the
  user-facing verb.
- *`page.delete` / `page.restore` (dot-separated)* — rejected: spec 007 uses snake-case
  for operation labels, dots only for hierarchical concepts like `mcp.tool.*`. Mixing
  styles is a small inconsistency that adds up.

**Verification path**: `PageLifecycleTests` injects an `ILogger<PageLifecycle>` substitute,
calls each method, and asserts the log lines contain `page_delete` / `page_restore` as the
operation token.

## R4: `error_type` vocabulary — reuse existing values

**Decision**: Map `BuildinApiException` to spec 007's existing `error_type` values:

| Buildin condition | `error_type` value | `FailureClass` |
|--|--|--|
| `ApiError { StatusCode: 404 }` | `not_found` | `NotFound` |
| `ApiError { StatusCode: 401 or 403 }` | `auth` | `Auth` |
| `TransportError` | `transport` | `Transport` |
| `ApiError` (other 4xx/5xx) | `unexpected` | `Unexpected` |
| `UnknownError` | `unexpected` | `Unexpected` |

Successful state-changing call: `recorder.Succeed()` with tag `changed=true`.
Successful no-op short-circuit: `recorder.Succeed()` with tag `changed=false`.

**Rationale**:

- These four `error_type` values are already in use by spec 006 (`CreatePageToolHandler`'s
  failure mapping) and spec 007 (the recorder's vocabulary). Reusing them keeps the
  dashboards and alerting unified across all page operations.
- The `changed` tag is the only new dimension introduced by this feature. It is a tag, not
  an operation name, so it does not inflate the operation-label cardinality on the
  outcome side. It distinguishes a no-op short-circuit from a real state change without
  needing a separate metric.

**Alternatives considered**:

- *Introduce a new `error_type = "no_op"` for short-circuits* — rejected: a no-op is a
  *success*, not an error. Encoding it as an error class would confuse spec 007's
  outcome=`success|failure` dimension.
- *Introduce a new metric `buildout.page.lifecycle.changed.total`* — rejected: it can be
  derived from `buildout.operations.total{operation="page_delete|page_restore", changed="true|false"}`.
  Adding a metric for a derivable signal violates the spec 007 principle of not
  multiplying instruments.

**Verification path**: contract test `observability.md` enumerates the table; unit tests
assert on tag values via a recording `ILogger`.

## R5: MCP `CallToolResult` shape

**Decision**: Each tool returns a `CallToolResult` whose `Content` is a two-block list:

1. `ResourceLinkBlock { Uri = "buildin://{page_id}", Name = "<resolved title or page_id>" }` —
   for chained tool calls and human resolution.
2. `TextContentBlock { Text = "<single-line JSON>" }` where the JSON is:
   ```json
   {"page_id":"...","archived":true,"changed":true}
   ```

On failure: throw `McpProtocolException` with `McpErrorCode.ResourceNotFound` (404),
`McpErrorCode.InvalidParams` (validation), `McpErrorCode.InternalError` (auth, transport,
unexpected) — matching `CreatePageToolHandler`'s mapping.

**Rationale**:

- The dual-block payload mirrors spec 006's `CreatePageToolHandler` (which returns a
  `ResourceLinkBlock`) and spec 008's `update_page` (which returns a `TextContentBlock`
  with a JSON summary). This feature combines both because it has both an identity to
  link (the page) and a small structured result (archived/changed) the LLM should read.
- The JSON is single-line and snake-cased to match the spec 008 update-result
  serialisation. Single-line keeps it cheap to embed in subsequent prompts.

**Alternatives considered**:

- *Return only a `TextContentBlock`* — rejected: loses the resource link affordance that
  chained MCP clients use to immediately re-read the affected page.
- *Return only a `ResourceLinkBlock`* — rejected: the LLM cannot read `changed` from the
  link alone, and SC-003's "changed flag is observable" requirement would not be testable
  on the MCP side without re-fetching the page.

**Verification path**: `DeletePageToolTests` / `RestorePageToolTests` assert on both
content blocks' presence and shape. The cheap-LLM benchmark in `ToolSelectionWithCheapLlmTests`
checks that the LLM parses the JSON's `changed` field correctly across a few sample prompts
("did the delete change anything?").

## R6: CLI `--print summary|json` wire form

**Decision**:

`--print summary` (default):

```
Deleted page <page_id>: archived=true (changed=true)
```

Or for no-op:

```
Deleted page <page_id>: archived=true (changed=false, no-op)
```

Symmetric for `restore`:

```
Restored page <page_id>: archived=false (changed=true)
Restored page <page_id>: archived=false (changed=false, no-op)
```

`--print json`:

```json
{"pageId":"...","archived":true,"changed":true}
```

On failure (both modes write to stderr):
- `summary`: a single line `Delete failed [<failure_class>]: <message>` (or `Restore failed`).
- `json`: `{"pageId":"...","archived":null,"changed":false,"failureClass":"NotFound","errorMessage":"..."}`.

**Rationale**:

- Mirrors spec 008's `UpdateCommand` print pattern (`--print summary|json`, summary on
  stdout, errors on stderr, exit codes carrying error class).
- camelCase JSON matches `OutputJsonOptions` in `UpdateCommand` — the same
  `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }` instance
  pattern is used.
- The "no-op" suffix in summary mode is explicit so a developer reading terminal output
  knows the call did not change state without parsing the `changed=false` token.

**Alternatives considered**:

- *Snake_case JSON* — rejected: spec 008's existing `UpdateCommand` JSON output uses
  camelCase. Mixing styles across CLI commands would be a paper-cut for shell-script
  consumers.
- *Silent on no-op (just exit 0)* — rejected: SC-003 and FR-006 require that callers can
  distinguish no-op from real state change without re-reading the page. Print must carry
  the `changed` signal.

**Verification path**: `DeleteCommandTests` and `RestoreCommandTests` capture stdout/stderr
and assert on the documented format strings.

## R7: Cheap-LLM tool-selection benchmark prompts (SC-006)

**Decision**: Extend the spec 007/008 cheap-LLM tool-selection test fixture with the
following 10 prompts. Pass criterion: ≥ 9/10 correct tool selections across one run on
the existing cheap-LLM model in the integration suite.

| # | Prompt | Expected tool |
|--|--|--|
| 1 | "Delete the page at <id>." | `delete_page` |
| 2 | "Archive this page: <id>." | `delete_page` |
| 3 | "Please remove the page <id> from my workspace." | `delete_page` |
| 4 | "Trash the page with id <id>." | `delete_page` |
| 5 | "Soft-delete <id>." | `delete_page` |
| 6 | "Restore the deleted page <id>." | `restore_page` |
| 7 | "Undo the delete of page <id>." | `restore_page` |
| 8 | "Un-archive page <id>." | `restore_page` |
| 9 | "Bring page <id> back from the trash." | `restore_page` |
| 10 | "Recover the archived page <id>." | `restore_page` |

**Rationale**:

- The two tools' descriptions must be discriminating enough that a small model selects
  the right one across the casual phrasings users actually emit. Five phrasings per
  direction covers the obvious lexical variants (delete/remove/archive/trash/soft-delete
  on one side; restore/undo/un-archive/recover on the other).
- "Archive" appears on the delete side because the buildin API's underlying verb is
  "archive" and an LLM trained on Notion-family terminology may emit it. The
  `delete_page` description must surface this synonym in its tool documentation
  (per FR-005).

**Alternatives considered**:

- *Skip the LLM-selection test* — rejected: spec 007 established this as a quality gate
  for new MCP tools (any tool that two-tool-pair adds should be tested under selection
  ambiguity). Skipping would create a precedent for skipping the gate.
- *Test 50 prompts* — rejected: cheap-LLM time-to-result on the existing harness is the
  dominant test cost; 10 prompts × 1 run is the smallest set that catches the
  obvious-failure cases without inflating CI duration.

**Verification path**: `ToolSelectionWithCheapLlmTests` (extends an existing fixture)
asserts the model's tool-name selection matches the expected column for each prompt; the
total correct count is ≥ 9.
