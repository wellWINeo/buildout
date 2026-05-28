# Phase 0 Research: Tree Command

This document resolves the open technical questions implied by the spec and the
plan's Technical Context. No `NEEDS CLARIFICATION` markers remain after this
phase — the spec's Clarifications section already pinned the user-facing
decisions; the items below cover the implementation-shaping choices that fall
out of those clarifications.

## 1. Source of the buildin.ai web URL for a node

- **Decision**: Read the `Url` field that the existing `IBuildinClient` already
  populates on the `Page` and `Database` records (`PageMapper` and
  `DatabaseMapper` both copy it from the generated API response). For every
  child page or child database discovered during traversal, the service calls
  `GetPageAsync` / `GetDatabaseAsync` to obtain the `Url` (and, in the database
  case, the `Title`).
- **Rationale**: FR-004 mandates the buildin.ai web URL (the same URL the
  workspace presents to users). The API already returns it as part of the
  Page/Database payload; no URL templating is required, and templating would
  risk diverging from the canonical URL the workspace assigns (workspace
  slug, dashed-id formatting, future migrations).
- **Alternatives considered**:
  - *Construct the URL from the UUID via a known pattern* — rejected: the
    project has no documented URL template; relying on a guess would silently
    produce broken links if buildin changes its URL scheme.
  - *Render only the title in ASCII and skip the URL* — rejected: FR-004 makes
    the link mandatory; without it the output is not pasteable as a clickable
    map.

## 2. Discovery mechanism for descendants

- **Decision**: Two cases, both via existing `IBuildinClient` methods:
  - **Page root or page node**: `GetBlockChildrenAsync(pageId)` with
    pagination (existing helper pattern); filter to `ChildPageBlock` and
    `ChildDatabaseBlock`; descend in returned order.
  - **Database root or database node**: `QueryDatabaseAsync(dbId, new
    QueryDatabaseRequest())` with pagination; each result row corresponds to a
    page record whose ID is recoverable from the generated
    `QueryDatabaseResponse_page.Id`. Map each row to a page node; the page's
    name comes from its title property and the URL from `GetPageAsync` on
    that ID. Records are rendered identically to child pages — the spec
    explicitly treats them as siblings (Edge Cases — "Tree command invoked on a
    database root").
- **Rationale**: FR-013 forbids inventing a new relationship model. These two
  API calls are the same ones the read tool and database view tool already use,
  so the abstraction (`IBuildinClient`) is unchanged.
- **Alternatives considered**:
  - *Use search with an ancestor filter* — rejected: the spec's traversal is
    by parent/child link, not by search scoring; semantics differ at depth
    boundaries and on archived nodes.

## 3. Naming source per node

- **Decision**:
  - **Root**: the `Page.Title` / `Database.Title` of the fetched root (rich
    text rendered to plain text via the existing `ITitleRenderer`).
  - **Child page during traversal**: the `Title` field on the
    `ChildPageBlock` is available without an extra fetch, but the spec
    requires the buildin.ai URL — which forces a `GetPageAsync` anyway — so
    the service uses the fully-fetched `Page.Title` for consistency. The
    `ChildPageBlock.Title` is used as a fallback if the child fetch fails
    (so the failure case can still render a meaningful name where possible —
    see Decision 5).
  - **Child database during traversal**: `ChildDatabaseBlock.Title` provides
    the name; `GetDatabaseAsync` provides the URL. (Same fallback logic on
    failure.)
- **Rationale**: One consistent "fetch the node, take its title" rule keeps
  the renderer pure; the `ChildPageBlock.Title` is preserved only as a
  diagnostic-friendly fallback when the child fetch errors.
- **Alternatives considered**:
  - *Trust the child block titles and skip per-child fetches* — rejected: that
    avoids URL acquisition (FR-004), so it is not viable.

## 4. Cache integration

- **Decision**: The traversal service depends on `IBuildinClient` directly. The
  read cache introduced in feature 012 (`IPageReadCache` /
  `IPageContentProvider`) is layered above `IBuildinClient` for the
  page-markdown read path only; it caches a fully assembled page-plus-blocks
  snapshot keyed by page ID. For the tree's narrower needs (only the page/database
  record and the immediate block children), introducing a new caching layer or
  rewiring through `IPageContentProvider` would be overreach: the existing
  client does not duplicate calls within a single traversal, and the spec's
  Assumptions explicitly accept the current performance envelope. If a future
  iteration shows the tree is the dominant cache-miss source, a tree-specific
  caching layer can be added without changing the public contract.
- **Rationale**: Honors Principle I (the new code stays in `Buildout.Core`),
  avoids premature optimization, and matches the spec's stated performance
  budget (SC-003).
- **Alternatives considered**:
  - *Reuse `IPageContentProvider`* — rejected: that provider returns the full
    block tree (the read cache's value); the tree command only needs the
    page record's URL and the first-level children's `child_page` /
    `child_database` block IDs.

## 5. Descendant-failure handling

- **Decision**: Wrap each per-descendant `GetPageAsync` /
  `GetDatabaseAsync` / `GetBlockChildrenAsync` / `QueryDatabaseAsync` call in a
  `try` that catches `BuildinApiException`. On failure:
  - Render the node with name `(unavailable)` (FR-012a).
  - URI: if the failing fetch is the URL-acquisition step, use the empty
    string; if the failure is the child-enumeration step on a node whose URL
    we already have, keep that URL.
  - `children: []` — treat as leaf.
  - Log the failure (`LogWarning` with the node identifier and the
    `BuildinApiException` as the inner cause; the existing `BuildoutMeter`
    counter conventions are honored at the tool level only).
- **Rationale**: FR-012 / FR-012a explicitly partition root-failure (abort)
  from descendant-failure (continue). Logging at the service layer keeps the
  presentation thin and gives operators a single place to find traversal
  diagnostics regardless of CLI vs MCP invocation.
- **Alternatives considered**:
  - *Surface a structured "partial result" envelope* — rejected: the spec
    requires the placeholder to appear inline in the tree, not as a
    side-channel.

## 6. Cycle detection

- **Decision**: Maintain a `HashSet<string>` of node IDs already visited in
  the current traversal. If a child's ID is already in the set, throw
  `TreeCycleDetectedException`, which surfaces as a clear error on both CLI
  and MCP (exit code distinct from "not found" / "transport").
- **Rationale**: Edge Cases — "Cycles in the hierarchy" — abort with a clear
  error rather than loop. The spec notes cycles should not occur in practice,
  so this is a defensive guard.
- **Alternatives considered**:
  - *Render the repeated node as `(unavailable)` and continue* — rejected:
    cycles can produce unbounded growth; the spec says "abort with a clear
    error".

## 7. ASCII rendering specifics

- **Decision**: Implement the renderer iteratively over a stack of "is last
  child at this level?" booleans, exactly mirroring the Unix `tree`
  convention:
  - Intermediate child: `├── `
  - Last child:         `└── `
  - Continued branch:   `│   ` (vertical bar + 3 spaces)
  - Closed branch:      `    ` (four spaces)
  Root prints with no prefix. Single-line names only (the API does not return
  multi-line titles; if a title contains a newline, the renderer normalizes
  it to a single space to keep the tree shape intact — this is a
  pragmatic extension of FR-015 and is exercised by a test).
- **Rationale**: Matches FR-005 verbatim and matches the BSD `tree(1)`
  long-tradition convention so the output is recognizable.

## 8. Markdown link escaping

- **Decision**: Use the angle-bracket URI form `[Name](<URL>)` (FR-004) and
  escape `]`, `[`, and `\` in the name with a leading backslash. Angle-bracket
  URIs already tolerate `<` and `>` issues in the path because the bracketed
  form is treated as a literal URI by CommonMark; the name side still requires
  escaping for the closing `]` to keep the link parseable.
- **Rationale**: FR-015 requires that names containing markdown-significant
  characters do not break link syntax. The angle-bracket URI form is the
  least invasive way to handle URL characters; backslash-escaping is the
  CommonMark-specified mechanism for the name side.
- **Alternatives considered**:
  - *Percent-encode the URL ourselves* — rejected: would diverge from the
    canonical workspace URL; the angle-bracket form is preferred per spec
    Assumptions.

## 9. JSON rendering specifics

- **Decision**: Use `System.Text.Json` with
  `JsonNamingPolicy.CamelCase`, write properties in the order `name`, `uri`,
  `children`, and emit `children: []` (never absent) on leaves. UTF-8 by
  default; no BOM. Pretty-printed (`WriteIndented = true`) to remain
  consistent with the project's other JSON-emitting commands and to keep
  the output human-readable when pasted; an MCP consumer that needs compact
  JSON can re-serialize, and tests cover both.
- **Rationale**: FR-006 specifies exactly three fields and requires `children`
  to be present (never elided) on leaves. CamelCase aligns with the JSON
  the editing tool already emits.

## 10. Depth validation

- **Decision**: Centralize the constants in `TreeDepth` (Min = 1, Max = 7,
  Default = 3). `IPageTreeService.BuildAsync` validates the argument and
  throws `TreeDepthOutOfRangeException`. The CLI and the MCP tool surface the
  exception with a clear message naming the valid range (FR-009).
- **Rationale**: Single source of truth for the bounds; the spec ties both
  CLI and MCP to the same range.

## 11. CLI exit-code mapping

- **Decision**: Re-use the same code set as `GetCommand` and `SearchCommand`:
  `0` success, `2` invalid usage (depth out of range, format invalid), `3`
  root page/database not found, `4` auth, `5` transport, `6` other buildin
  error. A new `7` is added for `TreeCycleDetectedException` (a server-data
  anomaly distinct from any of the existing categories).
- **Rationale**: Consistency with the existing CLI surface; cycle detection
  is rare enough to warrant its own code so operators can grep it.

## 12. MCP tool description and error mapping

- **Decision**: The `[Description]` on `TreeToolHandler.TreeAsync` explains
  what the tool returns, that the format/depth parameters are required to be
  one of `ascii`/`json` and 1–7 respectively, and how `(unavailable)` and
  `(untitled)` appear. Exceptions map: not-found → `McpErrorCode.InvalidParams`
  (matches `GetPageMarkdownToolHandler`); auth/transport/other →
  `McpErrorCode.InternalError`; depth/format violation →
  `McpErrorCode.InvalidParams`; cycle → `McpErrorCode.InternalError` with a
  message naming the cycle. `BuildoutMeter.McpToolInvocationsTotal` /
  `McpToolDuration` are recorded with tag `{ "tool": "tree" }` and outcome
  success/failure, identical to the existing tools.
- **Rationale**: Aligns the new tool with the conventions established by the
  other handlers (`GetPageMarkdownToolHandler`, `SearchToolHandler`) so
  reviewers and operators see the same shape across the surface.

## 13. Testing strategy summary

- **Decision**:
  - Unit tests against a mocked `IBuildinClient` cover every FR (FR-001
    through FR-016) and every Edge Case in the spec.
  - Renderer tests are pure-function tests over hand-built `TreeNode`
    instances so renderer behavior is isolated from traversal.
  - The MCP tool gets a cheap-LLM integration test (per the constitution's
    MCP-tool-change merge gate) that exercises the full pipeline and validates
    the tool's description, schema, and error shape.
  - No integration test hits the real buildin.ai (Principle IV).

## Outcome

All implementation-shaping unknowns are resolved. Phase 1 (data model,
contracts, quickstart) proceeds without further clarification.
