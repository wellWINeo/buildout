# Implementation Plan: Page Delete and Restore

**Branch**: `009-delete-restore-page` | **Date**: 2026-05-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/009-delete-restore-page/spec.md`

## Summary

Add `delete` and `restore` to buildout's page-lifecycle surface. Both are exposed as separate
MCP tools (`delete_page`, `restore_page`) and separate CLI commands (`buildout delete`,
`buildout restore`), and both resolve to a single buildin call: `PATCH /v1/pages/{page_id}`
with the `archived` flag set to the target value. No new buildin endpoint is involved —
buildin has no dedicated DELETE for pages, and restore is the same PATCH with
`archived: false`.

A new core service, `IPageLifecycle`, owns the operation end-to-end with two methods:
`DeleteAsync(pageId)` and `RestoreAsync(pageId)`. Each method (a) reads the current page via
`IBuildinClient.GetPageAsync`, (b) short-circuits to a no-op outcome if the page is already in
the target state, (c) otherwise issues `UpdatePageAsync` with a `UpdatePageRequest` whose only
populated field is `archived`, and (d) returns a `PageLifecycleOutcome` carrying the updated
`Page`, a `Changed` boolean, and (on failure) a `FailureClass`. The service is wrapped in
spec 007's `OperationRecorder` with operation names `page_delete` and `page_restore`.

The CLI adds two `Spectre.Console.Cli` commands. Each command takes one positional page-ID
argument and a `--print summary|json` flag (default `summary`), mirroring spec 006/008's CLI
convention. The MCP server adds two `[McpServerTool]` handlers, each with a single
`page_id` parameter and a `CallToolResult` containing a `ResourceLinkBlock` plus a textual
status line (`changed: true|false`, `archived: true|false`). No new buildin client methods,
no new NuGet dependencies, no new metric names. Two-tool / two-command separation is enforced
at the presentation layer; the core exposes one cohesive `IPageLifecycle` interface.

## Technical Context

**Language/Version**: C# / .NET 10. Inherited from features 001–008.

**Primary Dependencies**: All existing. No new in-tree capabilities are exercised:

- **`IBuildinClient.GetPageAsync` + `IBuildinClient.UpdatePageAsync`** — already on the
  interface from feature 001 and used unchanged by features 006 and 008. The plan adds
  zero methods to `IBuildinClient` (Principle V).
- **`Spectre.Console.Cli`** — already wired in `Program.cs` from feature 001 and extended by
  features 004 and 008. Two new commands are registered through the existing
  `app.Configure(config => config.AddCommand<...>(...))` pipeline.
- **`ModelContextProtocol` SDK** — already present in MCP from feature 001 and extended by
  features 003, 005, 006, 008. The two new tools follow the `CreatePageToolHandler` pattern:
  `[McpServerToolType]` class, `[McpServerTool(Name = "...")]` method returning
  `Task<CallToolResult>`, manual `BuildoutMeter.McpToolInvocationsTotal` /
  `McpToolDuration` recording for spec 007 parity.
- **`OperationRecorder`** — already present in `Buildout.Core.Diagnostics` from feature 007.
  The new operations register through it under the names `page_delete` and `page_restore`,
  emitting through the existing `buildout.operations.total` /
  `buildout.operation.duration` instruments and the existing `error_type` tag vocabulary.

**Storage**: N/A. The only state mutation is the buildin page's `archived` flag itself.

**Testing**: xUnit v3 + NSubstitute, WireMock-based integration harness — all inherited from
prior features. New categories:

- Unit tests for `PageLifecycle` covering: happy-path delete, happy-path restore, no-op
  short-circuit (delete on already-archived, restore on already-active — both must issue
  zero `UpdatePageAsync` calls), FailureClass mapping for 401/403/404/5xx/transport.
- Unit tests asserting the `UpdatePageRequest` sent to `IBuildinClient.UpdatePageAsync`
  populates **only** the `archived` field (FR-003 contract; properties/icon/cover all
  unset).
- Integration tests (CLI + MCP) hitting WireMock stubs that toggle `archived` on the
  mock `GET` response between calls, exercising both happy paths, idempotent no-ops, and
  every error class.
- Cheap-LLM integration test verifying that an LLM correctly selects `delete_page` for a
  "remove this page" prompt and `restore_page` for an "undo the deletion" prompt across a
  small benchmark of phrasings (SC-006).

**Target Platform**: Same as existing — .NET 10 console processes.

**Project Type**: Internal feature touching three existing projects (`Buildout.Core`,
`Buildout.Cli`, `Buildout.Mcp`) plus their two test projects. No new projects.

**Performance Goals**: Each operation issues at most two buildin calls (one `GetPageAsync` +
zero or one `UpdatePageAsync`), so steady-state cost is dominated by buildin network latency.
WireMock-fixture round-trips for a single delete or restore complete under 100 ms excluding
injected latency. The no-op short-circuit path issues exactly one buildin call and matches
the latency of a bare `getPage`.

**Constraints**:

- No real buildin network calls in tests (Constitution IV).
- `UpdatePageRequest` sent by either method MUST populate only `archived` — never
  `properties`, `icon`, or `cover` (FR-003 in spec).
- No interactive confirmation prompts in either CLI command (FR-012 in spec).
- No new metric names, span names, or `error_type` values (FR-010 in spec) — reuse the
  spec 007 vocabulary verbatim.
- No retries on 5xx (FR-011 in spec) — surface as transport error and exit.

**Scale/Scope**:

- 1 new core service interface + implementation (`IPageLifecycle` / `PageLifecycle`) under a
  new `Buildout.Core.PageLifecycle` namespace.
- 1 new outcome record (`PageLifecycleOutcome`) reusing the existing `FailureClass` enum
  (already defined in `Buildout.Core.Markdown.Authoring`; relocate or reference).
- 2 new CLI commands (`DeleteCommand`, `RestoreCommand`) + their `Settings` classes.
- 2 new MCP tool handlers (`DeletePageToolHandler`, `RestorePageToolHandler`).
- ~2 new unit-test files; ~4 new integration-test files; 1 cheap-LLM tool-selection test.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|-----------|------------|-------|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | `IPageLifecycle` owns the full sequence: pre-read, no-op short-circuit, request shaping (archived-only PATCH), `OperationRecorder` instrumentation, FailureClass classification. `Buildout.Cli`'s `DeleteCommand` / `RestoreCommand` only resolve arguments, call the interface, and render the outcome. `Buildout.Mcp`'s `DeletePageToolHandler` / `RestorePageToolHandler` only translate tool args, call the interface, and shape `CallToolResult`. Neither presentation layer touches `IBuildinClient` directly. |
| II | LLM-Friendly Output Fidelity | ✅ PASS (N/A) | This feature does not render Markdown or convert blocks. The MCP tools return a small JSON payload (page id, archived flag, changed flag). No block-type compatibility matrix is touched. |
| III | Bidirectional Round-Trip Testing | ✅ PASS (N/A) | No block↔Markdown conversion is involved. Round-trip discipline applies to converters; this feature touches none. The integration suite still includes a delete→restore→read chain that asserts final-state symmetry as a behavioural round-trip (SC-004). |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | Tasks (Phase 2) are ordered red-first. Every method on `IPageLifecycle`, each CLI command, and each MCP tool has a failing test before its implementation. No real buildin host. No tests skipped or disabled. |
| V | Buildin API Abstraction | ✅ PASS | `PageLifecycle` consumes the existing `IBuildinClient` interface: `GetPageAsync`, `UpdatePageAsync`. **No new methods added to `IBuildinClient`.** A future User-API client implementation slots in unchanged. |
| VI | Non-Destructive Editing | ✅ PASS | Destructive intent is surfaced three ways: (a) the CLI verb `delete` and MCP tool name `delete_page` name the operation explicitly, (b) the MCP tool description (per FR-005) states the operation is reversible and cross-references `restore_page`, (c) two separate tools/commands prevent the LLM-accident vector of toggling state through a boolean parameter (FR-006). The operation does not rewrite, reorder, or remove any block — it flips a single server-side flag. The buildin server's cascade behaviour on archive is preserved as-is and is not synthesised client-side (per spec edge-cases). |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | New code respects `Directory.Build.props`. |
| `ModelContextProtocol` SDK | ✅ | Two new tools declared via `[McpServerTool(Name="delete_page"\|"restore_page")]` returning `Task<CallToolResult>`. |
| `Spectre.Console.Cli` | ✅ | Two new commands registered via `config.AddCommand<DeleteCommand>("delete")` and `config.AddCommand<RestoreCommand>("restore")`. |
| Solution layout (5 projects) | ✅ | No new projects. |
| Bot API as one impl of `IBuildinClient` | ✅ | Service uses the interface only. |
| Secrets from env/config | ✅ | No new secrets. |

**Gate result (pre-Phase 0)**: PASS — no violations.

**Re-check after Phase 1 design**: PASS — Phase 1 design preserves all gates.
`Complexity Tracking` table remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/009-delete-restore-page/
├── plan.md                  # This file (/speckit-plan output)
├── spec.md                  # /speckit-specify output
├── research.md              # Phase 0 output (this command)
├── data-model.md            # Phase 1 output (this command)
├── quickstart.md            # Phase 1 output (this command)
├── contracts/               # Phase 1 output
│   ├── core-lifecycle.md        # IPageLifecycle surface and error taxonomy
│   ├── cli-delete-restore.md    # buildout delete + buildout restore command surfaces
│   ├── mcp-delete-restore.md    # delete_page + restore_page tool schemas
│   ├── buildin-endpoints.md     # WireMock stubs ↔ IBuildinClient methods (GET + PATCH /v1/pages)
│   └── observability.md         # Spec 007 integration: operation names + error_type values reused
├── checklists/
│   └── requirements.md      # Spec quality checklist (already created)
└── tasks.md                 # Phase 2 (/speckit-tasks output — NOT this command)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    PageLifecycle/                                        # NEW namespace
      IPageLifecycle.cs                                   # NEW: DeleteAsync + RestoreAsync
      PageLifecycle.cs                                    # NEW: orchestration implementation
      PageLifecycleOutcome.cs                             # NEW: { PageId, Archived, Changed, FailureClass?, UnderlyingException? }
    Markdown/Authoring/CreatePageOutcome.cs               # MODIFIED (read-only): the FailureClass enum here
                                                          # is *reused* by PageLifecycleOutcome. No code change to this file;
                                                          # the new outcome references the existing enum via a using statement.
    DependencyInjection/
      ServiceCollectionExtensions.cs                      # MODIFIED: register IPageLifecycle -> PageLifecycle

  Buildout.Cli/
    Commands/
      DeleteCommand.cs                                    # NEW: buildout delete <page_id>
      DeleteSettings.cs                                   # NEW: PageId positional + --print summary|json
      RestoreCommand.cs                                   # NEW: buildout restore <page_id>
      RestoreSettings.cs                                  # NEW: PageId positional + --print summary|json
    Program.cs                                            # MODIFIED: config.AddCommand<DeleteCommand>("delete"),
                                                          # config.AddCommand<RestoreCommand>("restore")

  Buildout.Mcp/
    Tools/
      DeletePageToolHandler.cs                            # NEW: [McpServerTool(Name = "delete_page")]
      RestorePageToolHandler.cs                           # NEW: [McpServerTool(Name = "restore_page")]
    Program.cs                                            # MODIFIED: .WithTools<DeletePageToolHandler>()
                                                          # and .WithTools<RestorePageToolHandler>()

tests/
  Buildout.UnitTests/
    PageLifecycle/                                        # NEW directory (mirrors src namespace)
      PageLifecycleTests.cs                               # NEW: happy paths, no-op short-circuit (zero PATCH calls),
                                                          # archived-only request body assertion, FailureClass mapping
                                                          # for 401/403/404/5xx/transport, operation-name registration
      PageLifecycleOutcomeTests.cs                        # NEW: record equality + serialisation round-trip if needed

  Buildout.IntegrationTests/
    Buildin/
      BuildinStubs.cs                                     # MODIFIED: RegisterUpdatePage (PATCH /v1/pages/{id})
                                                          # already exists from spec 008; add toggleable-archived
                                                          # GET stub helper if not present
    Cli/
      DeleteCommandTests.cs                               # NEW: happy path, idempotent no-op, page_not_found,
                                                          # permission_denied, transport — all error classes
      RestoreCommandTests.cs                              # NEW: symmetric to DeleteCommandTests
    Mcp/
      DeletePageToolTests.cs                              # NEW: happy path + every error class + idempotent no-op
      RestorePageToolTests.cs                             # NEW: symmetric to DeletePageToolTests
      ToolSelectionWithCheapLlmTests.cs                   # MODIFIED OR NEW: extend the spec 007 / 008 cheap-LLM
                                                          # tool-selection test with "delete this page" and
                                                          # "undo the delete" benchmark prompts (SC-006)
    Cross/
      DeleteRestoreSymmetryTests.cs                       # NEW: CLI delete → CLI restore round-trip leaves
                                                          # archive state unchanged (SC-004); CLI --print json
                                                          # output byte-equals MCP tool result for the same input
```

**Structure Decision**: A new top-level namespace `Buildout.Core.PageLifecycle/` is added
alongside the existing top-level non-Markdown namespaces (`Search/`, `DatabaseViews/`,
`Diagnostics/`, `Properties/`). Page archive/restore is not a Markdown-conversion concern, so
nesting it under `Markdown/` would mis-categorise it. The CLI and MCP additions follow the
exact patterns of `CreateCommand` / `CreatePageToolHandler`. No new projects. No new NuGet
dependencies.

## Phase 0: Research (output: research.md)

Items unknown at the start of `/speckit-plan` and resolved in `research.md`:

- **R1 — `UpdatePageRequest` PATCH semantics (verify archived-only PATCH is non-destructive
  for properties/icon/cover)**: confirm against the buildin OpenAPI document and against
  the existing Kiota-generated `UpdatePageRequest` model that unset properties on the
  request body do **not** clear server-side values. Decision: based on `openapi.json:1030`
  (the `UpdatePageRequest` schema marks every field as optional without a "null clears"
  semantic) and buildin's PATCH convention, sending a body of `{"archived": true}` leaves
  `properties`, `icon`, and `cover` untouched on the server. The unit test in FR-003 asserts
  the request body shape; an integration test against WireMock asserts the response shape
  hasn't lost properties. No alternative considered necessary.

- **R2 — Where the `FailureClass` enum lives**: it is currently defined in
  `Buildout.Core.Markdown.Authoring.CreatePageOutcome.cs` alongside `CreatePageOutcome` and
  `PartialCreationException`. The new `PageLifecycleOutcome` reuses the same enum
  vocabulary verbatim (`Validation`, `NotFound`, `Auth`, `Transport`, `Unexpected` —
  `Partial` is unused because lifecycle ops are not multi-step). Decision: reference the
  existing enum from `PageLifecycleOutcome` with a `using` statement; do not move or
  duplicate the enum. Rationale: moving the enum is a multi-file refactor unrelated to the
  feature and would churn spec 006/008 code without benefit. If a future spec wants a
  shared error namespace, that's a separate constitution-touching refactor.

- **R3 — Operation-name choice for `OperationRecorder`**: spec 007's existing operation
  vocabulary uses lowercase snake-case verbs (`page_create`, `page_read`, `page_update`).
  Decision: `page_delete` and `page_restore`. These appear as `operation` label values on
  `buildout.operations.total` and `buildout.operation.duration` and as the leading log token
  in spec 007's `Operation {Operation} completed/failed` messages. No new metric names.

- **R4 — `error_type` vocabulary**: spec 006/007/008 already use `auth`, `transport`,
  `not_found`, `unexpected` (and spec 008 adds `patch.*` classes, irrelevant here).
  Decision: reuse `auth` (401/403), `not_found` (404), `transport` (TransportError),
  `unexpected` (other ApiError / UnknownError). For the no-op short-circuit, `recorder.Succeed()`
  is called with an additional tag `changed=false` so dashboards can distinguish no-op calls
  from state-changing calls without inventing a new outcome value. The
  `OperationRecorder.SetTag("changed", false)` call is sufficient because the recorder
  already propagates arbitrary tags through `BuildTagList`.

- **R5 — MCP `CallToolResult` shape**: the existing `CreatePageToolHandler` returns a
  `CallToolResult` whose `Content` is a `ResourceLinkBlock` pointing at
  `buildin://{new_page_id}`. Decision: the new tools return a `CallToolResult` whose
  `Content` carries both (a) a `ResourceLinkBlock` pointing at `buildin://{page_id}` for
  caller convenience and (b) a `TextContentBlock` whose body is a single JSON object:
  `{ "page_id": "...", "archived": true|false, "changed": true|false }`. The JSON form is
  what LLMs actually parse; the resource link is for human readers and chained tool calls.

- **R6 — CLI `--print summary|json` wire form**:
  - `summary` (default):
    ```
    Deleted page <page_id>: archived=true (changed=true)
    ```
    or
    ```
    Deleted page <page_id>: archived=true (changed=false, no-op)
    ```
    Symmetric for `restore`.
  - `json`: the full `PageLifecycleOutcome` record serialised with `System.Text.Json` using
    the camelCase `JsonNamingPolicy` already configured in `UpdateCommand` (`OutputJsonOptions`).
    On failure, the JSON object additionally carries `failure_class` and `error_message`.

- **R7 — Cheap-LLM tool-selection benchmark prompt set for SC-006**: extend the existing
  spec 007/008 LLM integration test with 10 prompts: 5 phrasings that should resolve to
  `delete_page` ("delete the page", "archive this page", "remove this page from the
  workspace", "trash this page", "soft-delete this page") and 5 that should resolve to
  `restore_page` ("restore the deleted page", "undo the delete", "un-archive the page",
  "bring this page back from trash", "recover the archived page"). Pass criterion: ≥ 9/10
  correct selections on the existing cheap-LLM model used by spec 007.

## Phase 1: Design & Contracts

### data-model.md

Captures the in-memory shapes the lifecycle service operates over. See `data-model.md`.
Key shapes:

- `PageLifecycleOutcome { PageId: string; Archived: bool; Changed: bool; FailureClass?: FailureClass; UnderlyingException?: Exception }`.
- The existing `Buildout.Core.Buildin.Models.Page` and `UpdatePageRequest` shapes (read-only
  reuse — no changes).

### contracts/

Five contract documents:

- `core-lifecycle.md` — `IPageLifecycle` surface, the `PageLifecycleOutcome` shape, and
  the error taxonomy (`FailureClass` mapping from `BuildinApiException`).
- `cli-delete-restore.md` — `buildout delete <page_id> [--print summary|json]` and
  `buildout restore <page_id> [--print summary|json]` command surfaces, exit codes
  (`0` success including no-op, `3` not_found, `4` auth, `5` transport, `6` unexpected),
  and output schemas for both `--print` modes.
- `mcp-delete-restore.md` — `delete_page` and `restore_page` tool schemas, descriptions
  (with the FR-005 cross-references and reversibility statement), `CallToolResult` content
  blocks, and the error-class mapping to `McpProtocolException` `McpErrorCode` values
  (matching `CreatePageToolHandler`).
- `buildin-endpoints.md` — exhaustive list of buildin endpoints this feature uses:
  `GET /v1/pages/{page_id}` and `PATCH /v1/pages/{page_id}`, the request-body shape used by
  the PATCH (archived-only), and the response-body shape consumed by both.
- `observability.md` — spec 007 extension: the two new `operation` label values
  (`page_delete`, `page_restore`), the two new `tool` label values (`delete_page`,
  `restore_page`), and a note that the existing `error_type` vocabulary is reused
  verbatim with no new values introduced. The `changed=true|false` tag is documented as
  a new operation-level dimension distinguishing state-changing calls from no-op
  short-circuits.

### quickstart.md

Three scenarios:

1. **Delete a page from the CLI**: `buildout delete <page_id>` against a live buildin page;
   show summary and JSON outputs; show the idempotent no-op message on a second run.
2. **Restore a page from the CLI**: `buildout restore <page_id>` for the page deleted in
   scenario 1; show summary and JSON outputs.
3. **LLM-native round-trip via MCP**: agent invokes `delete_page` → `get_page_markdown`
   (returns `archived: true`) → `restore_page` → `get_page_markdown` (returns
   `archived: false`). Demonstrates the SC-001/SC-002 end-to-end story.

### Agent context update

`CLAUDE.md` (project root) currently references `specs/008-markdown-page-update/plan.md`
between the `<!-- SPECKIT START -->` and `<!-- SPECKIT END -->` markers. Phase 1 updates
that link to `specs/009-delete-restore-page/plan.md`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
