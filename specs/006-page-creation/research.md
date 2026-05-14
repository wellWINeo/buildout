# Research: Page Creation from Markdown

**Feature**: [spec.md](./spec.md) · [plan.md](./plan.md)
**Date**: 2026-05-13

This document captures the design choices that were unknown at the start
of `/speckit-plan` and the rationale behind the chosen option. Each entry
follows the *Decision / Rationale / Alternatives considered* format.

---

## R1 — CommonMark parser

**Decision**: Use **Markdig 1.1.3**, lifted from `Buildout.Cli` into
`Buildout.Core` (added as a `<PackageReference>` on
`Buildout.Core.csproj`; CLI keeps its own reference for unrelated
terminal-rendering use).

**Rationale**:

- Already present in the solution; the dependency footprint doesn't
  grow.
- CommonMark + GFM extension coverage is complete. GFM task-list
  (`- [ ]` / `- [x]`) maps cleanly to buildin's `to_do` block type
  (FR-003).
- `Markdown.Parse(string, MarkdownPipeline)` produces a
  `MarkdownDocument` AST we walk once; per-block-type parsers consume
  individual AST node types (`HeadingBlock`, `ListBlock`,
  `FencedCodeBlock`, `QuoteBlock`, `ThematicBreakBlock`,
  `ParagraphBlock`). Inline runs are `LiteralInline`, `EmphasisInline`
  (with kind), `CodeInline`, `LinkInline`. All map directly to the
  buildin block / RichText shapes we already model.
- Markdig is mature and battle-tested; no need to budget time for
  parser correctness.

**Alternatives considered**:

- **Hand-roll a CommonMark parser**: rejected. CommonMark is a large
  grammar; reimplementing it for one feature is high-risk and
  high-maintenance.
- **Microsoft.Toolkit.Parsers.Markdown** or other parsers: rejected.
  Markdig has the strongest GFM coverage in the .NET ecosystem and is
  the only one already in our solution.

---

## R2 — Title extraction from the Markdig AST

**Decision**: At the top of `MarkdownToBlocksParser.Parse`, after
`Markdown.Parse(...)` returns the `MarkdownDocument`, take the first
non-trivial child block (skipping any `LinkReferenceDefinitionGroup`).
If it is a `HeadingBlock` with `Level == 1`, capture its inline text as
the candidate title and remove that block from the document before
block-parsing. Otherwise leave the document untouched. Title
resolution is then:

| Explicit `title` param | Leading H1 present | Result |
|---|---|---|
| set | yes | use explicit title; H1 still consumed (so body matches the read side) |
| set | no | use explicit title; no consumption |
| unset | yes | use H1's inline text as title |
| unset | no | validation error before any network call |

**Rationale**:

- Mirrors feature 002's read side, which emits
  `# <title>\n\n<body>`. Consuming the leading H1 on the way back in
  keeps the round-trip symmetric.
- CommonMark treats blank lines as separators, not nodes — Markdig
  emits no "blank line" AST node. The body simply starts at the
  next AST child after the consumed H1.
- The explicit-title-wins rule lets callers override the body's
  apparent title without having to edit the document. Critical when
  the body was produced by another tool that always emits an H1.

**Alternatives considered**:

- **Always require an explicit title**: rejected. The clarified spec
  (FR-005) commits to leading-H1 consumption as the default.
- **Consume the first H1 anywhere in the document, not only the
  leading one**: rejected. That would change body structure
  unpredictably and break round-trip equivalence for documents whose
  intended title is later than the first H1.

---

## R3 — Mention recovery from Markdown links

**Decision**: Post-process the parsed AST. Walk every `LinkInline`
node; if its `Url` starts with `buildin://`, replace the link's
emitted run in the inline-parser output with a buildin mention
`RichText` annotation rather than a link annotation.

- Mention kind: emit a **page mention** by default. Page-vs-database
  disambiguation happens server-side — buildin's payload classifies
  the id and returns an appropriate error if the id isn't a readable
  page. (Feature 002's read side already emits the same URI shape for
  both kinds; round-trip preserves the URI, not the kind.)
- User mentions and date mentions: feature 002's read side
  intentionally drops these to plain text (`@Name`, ISO date), so
  there's nothing to recover. The matrix marks these as one-way-lossy
  on the write side (spec FR-004).

**Rationale**:

- A post-AST scan is one short function; a Markdig pipeline
  extension is more code, more registration surface, and harder to
  test in isolation.
- The URI scheme `buildin://` is already established by feature 002
  for the MCP resource and is unique to this project; no risk of
  confusion with other link targets.

**Alternatives considered**:

- **Custom Markdig inline parser extension**: rejected. Adds
  pipeline-configuration surface for a 10-line transformation.
- **Server-side classification on every link**: rejected. Would
  require a GET per link before `createPage`, which can't be
  parallelised against parsing and balloons the API-call budget.

---

## R4 — Nested children fanout

**Decision**: After top-level blocks are written (via
`CreatePageRequest.Children` plus follow-up `appendBlockChildren`
batches), the `AppendBatcher` recurses one level at a time. For each
parent block that carried children in the in-memory tree, it issues
`appendBlockChildren(parent_block_id, batch)` with batches of ≤100
children. The recursion is depth-first per top-level subtree but the
batches themselves are flat.

The `AppendBlockChildrenResult` returned by each call carries the
created block ids (in insertion order), which the batcher correlates
to the in-memory subtree so it can address the next level's parents.

**Rationale**:

- Confirmed against `openapi.json`: `AppendBlockChildrenRequest`
  takes a flat `children` array of `{ type, data }` items, and
  `BlockData` has no `children` field.
  `CreatePageRequest.Children` is similarly flat.
- A separate append per nested parent matches the API's actual
  shape; trying to flatten the tree into one request would lose
  parent-child structure.

**Alternatives considered**:

- **Reject nested-children input in v1**: rejected. The spec edge
  case "Nested children (lists with sub-bullets, code blocks inside
  list items)" is explicitly in scope. Lists with sub-bullets are
  common, idiomatic Markdown.
- **One `appendBlockChildren` per individual nested block**:
  rejected. Wastes round-trips when a single parent has many
  children. Batching to 100 mirrors the top-level rule (FR-008).

---

## R5 — Parent kind probing semantics

**Decision**: Sequential probe. `GET /v1/pages/{id}` first. On 404
only, `GET /v1/databases/{id}`. If both 404, the operation surfaces
the parent-not-found failure class before any write call is issued
(spec FR-010). 401/403 / transport on the probe surface as the
corresponding failure classes immediately.

Workspace / space-id parents are deferred from v1: buildin's
`openapi.json` exposes no `GET /v1/spaces/{id}` endpoint, and the
clarified probe sequence has no way to recognise a space id. The
implementation treats workspace-shaped parents as parent-not-found.

**Rationale**:

- The common case (page parent) succeeds on the first probe;
  parallelising would double rate-limit cost for the common path.
- 404 is the only signal that distinguishes "wrong kind" from
  "wrong id under right kind" given the API doesn't return a
  discriminator on the parent itself.
- Sequential is easier to test: one stub per branch.

**Alternatives considered**:

- **Parallel probe (`Task.WhenAll(GET page, GET database)`)**:
  rejected. Doubles the read load on every create and complicates
  error precedence (which 4xx wins?).
- **Probe via `POST /v1/pages` with a no-op body and parse the
  rejection**: rejected. Mutates buildin state on success; violates
  Principle VI and is brittle.

**Spec drift flagged**: spec FR-009 still lists "workspace
identifier" as a valid `--parent` form, but the clarified FR-010
probe sequence has no path to support it. The plan flags this for
the next clarification pass; until then, workspace ids surface as
parent-not-found. `/speckit-tasks` will include a task to tighten
FR-009.

---

## R6 — Property value parsing for `--property name=value`

**Decision**: Per-kind dispatch keyed on the database schema fetched
during the probe.

| Property kind | Accepted string form |
|---|---|
| `title` | passes through as plain text |
| `rich_text` | passes through as plain text |
| `number` | invariant-culture decimal; rejects `,` as decimal sep |
| `select` | exact match against the schema's option name (case-sensitive); unknown option → validation error |
| `multi_select` | comma-separated; whitespace trimmed; each token must match an option name; unknown option → validation error |
| `checkbox` | `true`/`false`/`yes`/`no` (case-insensitive); other → validation error |
| `date` | ISO 8601 (`YYYY-MM-DD` or with time/timezone); rejects ambiguous formats |
| `url` | passes through; minimum well-formedness check via `Uri.TryCreate(..., UriKind.Absolute)` |
| `email` | passes through; non-empty check only (buildin handles further) |
| `phone_number` | passes through as plain text |

Unknown property name → validation error before any network write
call. People / files / relation / rollup / formula property kinds
trigger an "unsupported property kind in v1" validation error.

**Rationale**:

- All 10 supported kinds have a natural single-string representation;
  buildin handles deeper validation server-side.
- Errors are surfaced before any write so partial writes are
  impossible from a property-name typo.
- Splitting `multi_select` on `,` (not `;`) matches user expectations
  and `select` option names rarely contain commas.

**Alternatives considered**:

- **JSON value syntax (`--property '{"name":"X","number":5}'`)**:
  rejected. Hostile for shell users; clashes with quoting. The MCP
  surface receives `properties` as an object already; the CLI's
  flat `--property name=value` form remains shell-pipeable.
- **Auto-detect kind from value shape**: rejected. Schema-driven
  dispatch is unambiguous and gives better error messages.

---

## R7 — MCP `resource_link` return shape via the SDK

**Decision**: Declare the `[McpServerTool(Name = "create_page")]`
handler as `Task<CallToolResult>`. Populate `result.Content` with a
single `ModelContextProtocol.Protocol.ResourceLinkBlock` whose `Uri`
is `buildin://<new_page_id>` and whose `Name` is the page title
(the one written, per FR-005). Set `result.IsError = false`.

For error cases that map to MCP-protocol errors, continue to throw
`McpProtocolException` with the appropriate `McpErrorCode` — the SDK
serialises that to the JSON-RPC error path (consistent with how
`SearchToolHandler` and `DatabaseViewToolHandler` work today).

**Rationale**:

- The SDK explicitly documents `CallToolResult` as a permitted
  return type when the tool needs to attach non-text content blocks
  or set the `IsError` flag (`ModelContextProtocol.Core.xml`).
- `ResourceLinkBlock` is the precise MCP-spec content type for "here
  is a resource you can read next" — gives the LLM a typed handle
  rather than a string it has to recognise.
- The page title in `Name` is useful for clients that surface the
  link in a UI; the URI alone is enough for chaining into the
  existing `buildin://{page_id}` resource.

**Alternatives considered**:

- **Return `Task<string>` containing the URI** (auto-wrapped as
  `TextContentBlock`): rejected by the spec clarification — chosen
  Option D explicitly preferred the structured content type.
- **Return `Task<ResourceLinkBlock>` directly**: not supported by the
  current SDK return-type binder (only `string`, structured types
  serialized to text, or `CallToolResult` are supported for tool
  methods). Wrapping in `CallToolResult` is the documented path.

---

## R8 — Partial-failure error message format

**Decision**: When `createPage` succeeds but a subsequent
`appendBlockChildren` batch fails, the operation surfaces a
`PartialCreationException(string newPageId, int batchesAppended, int
totalBatches, Exception underlying)`. The CLI maps this to exit code
6 (unexpected, same as feature 002/003 for the "other" failure
class) with stderr:

```text
Partial creation: page <new_page_id> exists but appendBlockChildren failed after <K> of <N> top-level batches: <underlying message>
```

The MCP tool maps it to `McpProtocolException` with code
`InternalError` and a `Message` containing the identical string.

The new page id is the first non-whitespace token after the
literal `page` prefix on the colon-separated key. A shell user can
extract it with `awk -F': ' '/^Partial creation:/{print $2}' | awk
'{print $2}'` — no JSON parsing required.

**Rationale**:

- Spec FR-012 / FR-015 require the partial page id in the error
  output; this format pins exactly how.
- A grep-able prefix (`Partial creation:`) lets shell users branch on
  the partial-vs-full failure case without checking exit codes alone.
- Matching CLI and MCP message bodies makes test fixtures
  cross-checkable.

**Alternatives considered**:

- **Auto-rollback the partial page** (`DELETE /v1/pages/<id>`
  on failure): rejected — spec Assumptions explicitly defer rollback
  to a future, additive feature. Aggressive rollback also masks
  diagnostic data the user might need.
- **Suppress the partial id from the error**: rejected — the spec
  requires it.

---

## R9 — Stdin handling and large documents

**Decision**: When `markdown_source` is `-`, read stdin to a string
with a hard cap of 16 MiB. Documents larger than that are rejected
with a validation error before parsing. Filesystem-path inputs are
not capped — the user supplies the path and bears the responsibility
for size.

**Rationale**:

- Markdig handles multi-MiB documents comfortably; 16 MiB is a
  generous ceiling that still bounds memory in the pathological-
  pipe case.
- Buildin's per-batch limit (100 blocks) is the true throttle for
  large bodies; documents that produce more than a few thousand
  top-level blocks will fan out into many `appendBlockChildren`
  calls. The 16 MiB cap is purely a defense-in-depth bound on the
  CLI process itself.

**Alternatives considered**:

- **No cap on stdin**: rejected — a runaway pipe can OOM the
  process; the cost of bounding it is one variable.
- **Same cap on filesystem paths**: rejected — paths are an
  explicit user choice; capping them would surprise users with
  large authored documents.

---

## Summary

All nine research items resolved. No `NEEDS CLARIFICATION` markers
remain in `plan.md`. Phase 1 can proceed: data-model, contracts, and
quickstart use the decisions above.
