# Phase 0 — Research: Page Search

This document records the technical decisions resolved before
implementation begins. Every "NEEDS CLARIFICATION" item the Technical
Context surfaced is addressed here. Decisions are deliberately
framework-/library-specific where helpful so the `/speckit-tasks` step has
no ambiguity.

## R1 — Which buildin search endpoint to call

**Decision**: Use **`IBuildinClient.SearchPagesAsync`**, which wraps
`POST /v1/search` (the typed endpoint that returns
`V1SearchResponse → V1SearchPageResult[]` with `Id`, `Object`,
`Properties.Title`, `Parent`, `Archived`, timestamps). Do **not** use
`IBuildinClient.SearchAsync` (which wraps `POST /v1/pages/search` and
returns an untyped `IReadOnlyList<object>`).

**Rationale**:

- `SearchPagesAsync` returns a typed shape with the fields the service
  needs (`Title` for display, `Parent` for the ancestor-walk filter,
  `Archived` for default exclusion). The untyped `SearchAsync` would
  force the service to either re-parse opaque objects or limit its
  surface area to ID-only.
- `V1SearchPageResult.Object` is the discriminator that distinguishes
  page-shaped from database-shaped results — both are returned from the
  same endpoint, both already exposed through the typed model, both
  surface uniformly in the formatter.
- The `IBuildinClient.SearchPagesAsync` method is already declared on
  the public interface from feature 001; `BotBuildinClient.SearchPagesAsync`
  is already implemented and wired through the Kiota generated client.

**Alternatives considered**:

- *Use `SearchAsync` (the `/v1/pages/search` variant)* — rejected: the
  endpoint returns `IReadOnlyList<object>`, which forces either reflection
  or a parallel typed mapper duplicating work the typed endpoint already
  does correctly.
- *Bypass `IBuildinClient` and call the Kiota generated API directly*
  — rejected: violates Constitution Principle V (all buildin HTTP must
  go through the typed client interface).
- *Add a new endpoint wrapper that exposes filter / sort knobs* —
  rejected: the spec defers re-ranking, sorting, and filter knobs.
  Adding parameters now would create dead surface area; they will be
  added incrementally when their consumers ship.

**Implementation invariants**:

- The service's only buildin call is `SearchPagesAsync(new PageSearchRequest
  { Query = q, StartCursor = cursor })`. `Filter`, `Sort`, and `PageSize`
  are left null in v1.
- The buildin response field `Object` is preserved on every
  `SearchMatch` so the formatter can label page vs database results.

## R2 — Mapping fix for `MapV1SearchResponse`

**Decision**: Extend `BotBuildinClient.MapV1SearchResponse` to populate
two additional fields on each emitted `Page`:

1. **`Title`** — derived from `V1SearchPageResult.Properties.Title.Title`
   (a `List<RichTextItem>`). Mapped through the existing `MapRichText`
   helper. Result is `IReadOnlyList<RichText>?`. `null` when the
   `properties.title` object is missing entirely; empty when present but
   carries zero rich-text items.
2. **`Parent`** — derived from `V1SearchPageResult.Parent` (a Kiota
   composed-type wrapper around `ParentBlockId | ParentDatabaseId |
   ParentPageId | ParentSpaceId`). Mapped through the existing
   `MapParent` helper. Result is `Parent?` (the hand-written
   discriminated union: `ParentDatabase`, `ParentPage`, `ParentBlock`,
   `ParentWorkspace`).

The other existing fields (`Id`, `CreatedAt`, `LastEditedAt`, `Archived`)
stay unchanged.

**Rationale**:

- Without `Title`, the formatter cannot produce a useful display line —
  it would have to fall back to "(untitled)" for every match, defeating
  the feature.
- Without `Parent`, the ancestor-walk filter (FR-004) has nothing to
  walk. Implementing the filter without parent metadata would require
  separate `GetPageAsync` calls per match, which would multiply network
  calls and violate the spec's correctness-without-throughput stance.
- `MapPage` (the existing mapper used by `GetPageAsync`) already does
  the same title extraction via the `ExtractTitle` helper. The fix here
  is to call into the same logic for the search response, with a sibling
  helper that takes `Gen.V1SearchPageResult_properties` instead of
  `Gen.Page_properties`. Both helpers are tiny and structurally
  identical — duplication is the lower-friction option vs introducing a
  generic over Kiota's generated property classes.

**Alternatives considered**:

- *Make a separate `SearchedPage` model rather than reusing `Page`* —
  rejected: the search-typed result and the page-typed result share the
  same conceptual shape (id, title, parent, archived). Two parallel
  records would be redundant and would force the formatter to handle
  two source types. Reusing `Page` keeps the domain model uniform.
- *Walk the parent chain via a runtime side call instead of populating
  `Parent`* — rejected for the reasons given above (network amplification).
- *Edit the Kiota-generated code to fold `Title`/`Parent` into a single
  fast accessor* — rejected: generated code is hand-edit-free
  (Constitution Principle V, plan §Constraints).

**Implementation invariants**:

- The mapping fix is exclusively additive — every `Page` record currently
  emitted continues to be emitted; only previously-defaulted fields are
  filled.
- A new `BotBuildinClientTests` row covers the title-extraction and
  parent-mapping behaviour; existing tests remain green.

## R3 — Ancestor-walk filter for the `--page` / `page_id` scope

**Decision**: Implement the scope filter as a pure, in-memory function
`AncestorScopeFilter.IsDescendantOf(SearchMatch match, string scopePageId,
IReadOnlyDictionary<string, Parent?> parentLookup)` that walks the
`Parent` chain starting from `match.PageId` until either:

- the current node's id equals `scopePageId` (match is in scope), or
- the current node's parent is null, a `ParentWorkspace`, a
  `ParentDatabase`, or a `ParentBlock` whose id is not present in
  `parentLookup` (match is out of scope), or
- a cycle is detected via a `HashSet<string> visited` (match is out of
  scope; cycle is logged at debug level).

The `parentLookup` is built inside `PageSearchService` from the merged
search response itself: every `SearchMatch` contributes
`(match.PageId → match.Parent)`. Pages whose ancestor chain runs through
a node *not* in the search response are NOT walked further — the filter
treats them as out of scope. This is correct because matches that are
descendants of the scope page MUST themselves be in the search response
(buildin returns all matches for the query); their parents may not be —
in which case those parents are reachable only by their `Parent.Id`,
which suffices for the chain walk so long as ancestors are themselves
matches OR the scope page itself is the immediate parent.

For the case where the scope page's descendants are in the search
response but the *intermediate* ancestors are not, the filter performs
on-demand `GetPageAsync(parentId)` calls to fetch missing ancestors,
caching results in `parentLookup` to avoid repeat fetches. This is the
single dynamic part of the filter.

**Rationale**:

- Buildin's `/v1/search` does not accept a parent filter, so the scope
  has to be applied client-side.
- Pre-fetching the entire workspace tree to build a global ancestor
  index would be O(workspace size), wildly disproportionate, and
  redundant on every search call. On-demand walk is O(matches × tree
  depth), which is small in practice.
- The early termination (scope-id reached) and cycle defence keep the
  worst case bounded even on adversarial parent metadata.
- The filter is pure given its three inputs, so it is trivially
  unit-testable without HTTP.

**Alternatives considered**:

- *Walk via repeated `GetPageAsync` calls without seeding `parentLookup`
  from the search response* — rejected: every match would incur at
  least one extra `GetPageAsync` even when the search response already
  carries enough information.
- *Force the scope page's full subtree to be enumerated upfront via
  `GetBlockChildrenAsync` recursion before searching* — rejected: this
  would replicate feature 002's pagination loop for the entire subtree
  before the filter could begin, violating the spec's pagination-stance.
  Search inverts the natural direction: matches first, then filter.
- *Skip the filter and return everything when `pageId` is provided,
  documenting that scoping is "best effort"* — rejected: spec FR-004
  is unambiguous; a "best effort" filter would silently leak unrelated
  matches into a scoped query.

**Implementation invariants**:

- `AncestorScopeFilter` takes `IBuildinClient` as a dependency and does
  the on-demand `GetPageAsync` calls itself when a parent id is not in
  `parentLookup`; the service hands it the seeded dictionary at the
  start of filtering.
- The filter uses a per-call `HashSet<string> visited` for cycle
  defence and never falls into an infinite loop even on malformed
  parent metadata.
- A `GetPageAsync` failure for a missing ancestor (`NotFound`,
  `Forbidden`) is treated as "match is out of scope" — the filter
  swallows and logs at debug level. Transport / unexpected errors are
  re-thrown unchanged so the surface-level error mapping kicks in.
- Match order is preserved: the filter is `Where`-shaped and never
  re-orders.

## R4 — Plain-mode result-body line format

**Decision**: Three tab-separated columns per match, terminated by
`\n`. Empty body (zero lines) for zero matches.

```text
<page_id>\t<object_type>\t<title>\n
```

- `<page_id>` is the buildin-supplied UUID stringified as
  `Guid.ToString("D")` (lowercase, hyphenated). Consistent with the
  buildin URL form and with feature 002's `buildin://{page_id}` resource
  template.
- `<object_type>` is `page` or `database` (lowercase, no other values
  in v1). Source: `V1SearchPageResult.Object`.
- `<title>` is the page title rendered as plain text (no Markdown
  markup, no inline annotations) by `TitleRenderer`. Empty titles are
  rendered as the literal placeholder `(untitled)`. Tab characters
  inside titles are replaced with a single space (the only escape rule;
  newlines are not possible in buildin titles).

**Rationale**:

- Tab-separated values are the lowest-friction shell-pipeable format:
  every Unix utility (`awk -F'\t'`, `cut -f`, `column -t`) handles them
  natively, and the format survives `head`, `tail`, `grep` unchanged.
- Putting the `<page_id>` first satisfies SC-008's
  `awk '{print $1}' | xargs buildout get` pipeline, since `awk`'s
  default field separator splits on whitespace including tabs and
  prints the first column.
- Including `<object_type>` between id and title gives users (human and
  LLM) an immediately visible cue when a database appears in results
  alongside pages, without complicating the awk pipeline (column 1 is
  still the id).
- "(untitled)" matches the spec's stated edge-case wording (FR-005, edge
  cases). The literal single-space tab replacement keeps each line as a
  three-column TSV line for any consumer that splits on `\t`.

**Alternatives considered**:

- *Two-column form (`<id>\t<title>`)* — rejected: hides the page-vs-
  database distinction the formatter has perfectly good information to
  surface. Adding the column later would silently break consumers that
  parse on column count.
- *JSON Lines* — rejected: the spec calls for shell-pipeable text;
  JSON requires `jq` for awk-style extraction, raising the bar for the
  primary user (a human in a terminal). LLM consumers can still parse
  TSV trivially.
- *Spectre.Console-styled output even in plain mode* — rejected: spec
  FR-010 forbids escape codes when stdout is non-TTY; plain mode must
  be raw text by construction.
- *Whitespace-padded columns to align visually in plain mode* —
  rejected: alignment depends on terminal width; padding bytes vary
  with title length and would break the byte-identical CLI/MCP
  invariant (FR-014).

**Implementation invariants**:

- The formatter takes `IReadOnlyList<SearchMatch>` and returns a
  `string`. It never reads any Spectre.Console / `IAnsiConsole` /
  `Console.Out` surface.
- The formatter is deterministic given a fixed input list — same input
  → byte-identical output.
- Trailing newline rule: each match line ends with `\n`. Zero matches
  → empty string (no trailing newline). This means `body.Length == 0`
  is the unambiguous "no matches" signal.

## R5 — Styled-mode CLI rendering

**Decision**: When stdout is a styled terminal, `SearchCommand` calls
`SearchResultStyledRenderer.Render(body, console)`. The renderer parses
the formatter's body line-by-line, splits each line on `\t` into the
three known columns, and emits a `Spectre.Console.Table` with three
columns (`ID`, `Type`, `Title`). For an empty body (no matches), it
emits a styled "No matches." line — a human cue that the search ran
successfully without distorting the plain-mode "empty body" contract.

**Rationale**:

- Reuses the formatter's body as the single source of truth for both
  modes. Styled mode adds presentation only; it never reorders, drops,
  or fabricates data.
- Tables align titles visually in the terminal regardless of width,
  which is the primary humans-only ergonomic improvement over the raw
  TSV. `Spectre.Console.Table` already handles wrapping, truncation,
  and ANSI capability detection.
- Parsing the formatter's body (rather than the `SearchMatch` list
  directly) keeps the renderer trivially testable against a fixed
  string and matches the structural invariant from feature 002 where
  styled mode reads core-produced text.

**Alternatives considered**:

- *Pass `IReadOnlyList<SearchMatch>` straight into the styled renderer*
  — rejected: would create a separate "rendered styled" path with no
  byte-identical contract back to the formatter, and would risk
  divergence if the formatter's columns change.
- *Use a `Spectre.Console.Tree` or paneled layout* — rejected: search
  results are flat; a tree adds visual chrome without information.
- *Render differently when results are zero (e.g. coloured "no
  matches" suggestion)* — kept simple in v1: emit a single styled
  "No matches." line with no extra suggestions.

**Implementation invariants**:

- The styled renderer accepts `(string body, IAnsiConsole console)` and
  writes via `console.Write(...)`. Tests inject a `TestConsole` and
  assert the captured output.
- The renderer never decides for itself whether to be styled — that
  decision lives in `SearchCommand` against `TerminalCapabilities`
  (reused from feature 002).

## R6 — MCP tool surface

**Decision**: Implement `SearchToolHandler` as an
`[McpServerToolType]`-decorated class with one
`[McpServerTool(Name = "search", ...)]` method. Use the SDK's standard
attribute-driven registration; wire it into `Buildout.Mcp/Program.cs`
via `WithTools<SearchToolHandler>()` alongside the existing
`WithResources<PageResourceHandler>()` from feature 002.

The tool:

- Declares two parameters in its signature: `string query` (required)
  and `string? page_id` (optional, default `null`).
- Returns `string` — the formatter's body. The SDK wraps it in a single
  `TextContent` block in the resulting `CallToolResponse`.
- The tool description identifies it as
  `"Search buildin pages by query. Returns one match per line, tab-
  separated: <page_id>\\t<object_type>\\t<title>. Use buildin://<page_id>
  to read a match."` so an LLM that just listed tools knows both the
  format and the natural follow-up resource.

**Rationale**:

- Attribute-driven registration is exactly the pattern used for
  `PageResourceHandler` (feature 002, R2). Reusing it keeps `Program.cs`
  small and declarative.
- A single string return is the simplest path to a deterministic
  text-content tool result whose body can be byte-compared against the
  CLI plain-mode body (SC-003 / FR-014). Wrapping in a `CallToolResponse`
  by hand would be busywork.
- Including the line-format hint in the tool description teaches the
  LLM how to parse the body and how to chain the read step on its own,
  satisfying SC-002 / SC-008 even before the LLM has access to source.

**Alternatives considered**:

- *Return a structured object with `matches: [{id, type, title}, ...]`*
  — rejected: would diverge from the CLI body, defeating FR-014. A
  structured shape is a future, additive change once we determine the
  CLI also wants structured output (probably never; TSV is enough).
- *Return one MCP content block per match instead of a single block* —
  rejected: clutters the LLM's view with N small text blocks where one
  body suffices, and breaks the byte-identical invariant unless we
  reconcatenate them.
- *Builder-based registration via `WithTools(builder => builder.Add...)`*
  — works, but the attribute form is shorter and matches the resource
  registration style.

**Implementation invariants**:

- The handler depends on `ISearchService`, `ISearchResultFormatter`,
  and a logger; nothing buildin-specific or transport-specific.
- On `BuildinApiException`, the handler maps the typed error category
  to an `McpProtocolException` (404 for the scope page → `ResourceNotFound`;
  401/403 → `InternalError` with "Authentication error"; transport →
  `InternalError` with "Transport error"); it never returns a 200 with
  an error blob in the body (FR-015).
- An empty / whitespace query is rejected before any service call, with
  a clear `McpProtocolException(InvalidParams, "...")` — tests assert
  the buildin client recorded zero calls (SC-006).

## R7 — CLI command surface

**Decision**: `SearchCommand` is an
`AsyncCommand<SearchCommand.Settings>` registered with
`CommandApp.Configure(c => c.AddCommand<SearchCommand>("search"))` —
the same shape as `GetCommand` from feature 002. The `Settings` class:

```text
public sealed class Settings : CommandSettings
{
    [CommandArgument(0, "<query>")]
    public string Query { get; init; } = string.Empty;

    [CommandOption("--page <PAGE_ID>")]
    public string? PageId { get; init; }
}
```

The command:

1. Validates `Query` is non-empty (trim + `IsNullOrWhiteSpace`); on
   failure, writes a styled error to stderr and returns exit code **2**
   (a new code reserved for "invalid arguments"; distinct from feature
   002's 3 / 4 / 5 / 6 mapping).
2. Calls `ISearchService.SearchAsync(Query, PageId, ct)`; receives an
   `IReadOnlyList<SearchMatch>`.
3. Calls `ISearchResultFormatter.Format(matches)` to produce the body.
4. If `TerminalCapabilities.IsStyledStdout` → `SearchResultStyledRenderer.Render(body, console)`.
   Else → `Console.Out.WriteAsync(body)`.
5. Returns exit code 0.
6. Wraps the entire pipeline in the same `BuildinApiException` →
   exit-code mapping `GetCommand` uses (3 / 4 / 5 / 6 for not-found /
   auth / transport / unexpected). Not-found applies only when the
   scope `--page` ID is provided and the buildin client returns 404 for
   the ancestor walk's `GetPageAsync` lookup.

**Rationale**:

- Mirroring `GetCommand`'s structure keeps the CLI predictable and
  reduces the cognitive overhead for users who already know `get`.
- Reserving a separate exit code for "invalid arguments" (the
  empty-query case) matches POSIX conventions and lets shell scripts
  distinguish "I gave a bad query" from "buildin said 401".
- The TTY-aware split is identical in shape to feature 002; the only
  change is the renderer downstream.

**Alternatives considered**:

- *Reuse exit code 6 for invalid arguments* — rejected: 6 is
  "unexpected error" in feature 002's documented mapping. Mixing
  expected-validation failure with unexpected-runtime failure is a
  category error.
- *Read the query from stdin if not on argv* — rejected: out of scope
  and adds shell-quoting subtleties without clear demand.
- *Expose `--include-archived` / `--limit` flags* — rejected: explicitly
  deferred per spec.

**Implementation invariants**:

- The command's only Spectre dependency for non-styled mode is
  `IAnsiConsole` for the stderr error output (red markup); plain stdout
  goes through `Console.Out` directly to guarantee zero escape codes
  (mirrors feature 002 `GetCommand`).
- Exit codes mirror feature 002 except for the new `2` for invalid args.

## R8 — Pagination loop

**Decision**: `SearchService` runs a single `do { … } while (HasMore)`
loop over `IBuildinClient.SearchPagesAsync`, advancing
`request.StartCursor` to the previous response's `NextCursor` until
`HasMore == false`. Append every received `Page` to a single
`List<SearchMatch>` in arrival order, mapping each `Page` to a
`SearchMatch` via `TitleRenderer` for the displayable title.

**Rationale**:

- Mirrors feature 002's pagination shape verbatim. No new abstraction
  for what is a one-method loop.
- The accumulation list is bounded only by buildin's actual match count
  for the query; v1 does not cap it (per FR-002). For real workspaces,
  this is naturally small (rarely thousands).

**Alternatives considered**:

- *Stream matches as they arrive (`IAsyncEnumerable<SearchMatch>`)* —
  rejected: out of scope per FR-002.
- *Concurrent fetching of multiple cursors* — rejected: the buildin
  pagination API is sequential by design (cursor depends on the prior
  response).

**Implementation invariants**:

- The loop is the only place the service touches the buildin client
  for the unscoped phase. The scope filter's on-demand
  `GetPageAsync` calls (R3) happen in a separate phase, after the
  full match list is in hand.
- Archived pages are filtered out *after* the merge but *before* the
  scope filter, so the scope filter never wastes ancestor-walks on
  matches that will be dropped anyway.

## R9 — Empty / whitespace query rejection

**Decision**: `ISearchService.SearchAsync` validates the query at its
boundary and throws `ArgumentException("Query must be non-empty.",
nameof(query))` when the trimmed query is empty. Both presentation
surfaces catch this:

- `SearchCommand` → exit code 2, stderr "Query must be non-empty.".
- `SearchToolHandler` → `McpProtocolException(InvalidParams, "Query
  must be non-empty.")`.

The validation MUST run before any `IBuildinClient` call. SC-006
verifies this by asserting the substituted client recorded zero
interactions.

**Rationale**:

- Centralising the validation in core ensures both surfaces enforce
  it identically without duplication.
- `ArgumentException` is the .NET-idiomatic shape for invalid input at
  a method's contract boundary; presentation can pattern-match on it
  trivially.

**Alternatives considered**:

- *Validate only at each presentation layer* — rejected: duplication;
  divergence likely; harder to test end-to-end.
- *Make `query` non-nullable and let the type system "ensure" non-
  emptiness* — only catches `null`, not whitespace. Runtime check still
  needed.

## R10 — Cheap-LLM test extension

**Decision**: Add a sibling `[Fact] public async Task
LlmCanFindAndReadPage()` in `tests/Buildout.IntegrationTests/Llm/
PageReadingLlmTests.cs`. The test:

1. Skips if `ANTHROPIC_API_KEY` is unset.
2. Prepares an in-process MCP server with a fake `IBuildinClient` that:
   - On `SearchPagesAsync` for query `"quarterly revenue"`, returns
     two matches whose titles are "Q3 Revenue Report" and "Marketing
     Plan" with a known set of page IDs.
   - On `GetPageAsync` for the Q3 page, returns a page with the same
     fixture content the existing test already uses.
   - On `GetBlockChildrenAsync`, returns the corresponding fixture
     blocks.
3. Drives Claude Haiku with a prompt that lists the available tools
   and resources, asks "Which page describes Q3 revenue, and what
   was the total?", and asserts the LLM (a) called `search` with the
   query, (b) read `buildin://<q3_page_id>`, and (c) answered with
   `4.2`.

**Rationale**:

- This is the integration test that satisfies SC-002 / FR-018 — the
  proof that the LLM can chain `search` then `buildin://`.
- Reusing the existing `PageReadingLlmTests` file keeps the LLM
  fixture infra in one place and avoids spinning up a parallel MCP
  client harness.
- Asserting the tool invocations (not just the final answer) catches
  protocol regressions that pure-text answer-checking would miss.

**Alternatives considered**:

- *Replace the existing `LlmCanAnswerQuestionsAboutRenderedPage` test*
  — rejected: that test still exercises the read path independently.
  Both tests provide non-overlapping coverage.
- *Use a different model* — rejected: feature 002 already justified
  Haiku; consistency wins.
- *Skip the chain test and rely on unit-level coverage* — rejected:
  spec FR-018 requires an LLM-driven proof of the chain.

**Implementation invariants**:

- The test is a single LLM call; cost target stays well under one cent.
- The fake `IBuildinClient` records every method invocation; the test
  asserts both invocations happened in the right order.
- The fixture page IDs are constants in the test file; no reliance on
  the live buildin workspace.

## R11 — DI registration

**Decision**: Extend
`Buildout.Core.DependencyInjection.ServiceCollectionExtensions.AddBuildoutCore`
to register the new search seams as singletons, alongside the existing
markdown registrations:

```text
services.AddSingleton<ITitleRenderer, TitleRenderer>();
services.AddSingleton<AncestorScopeFilter>();
services.AddSingleton<ISearchService, SearchService>();
services.AddSingleton<ISearchResultFormatter, SearchResultFormatter>();
```

`AncestorScopeFilter` is registered as its concrete class (no interface)
because no plausible second implementation exists; introducing an
interface would be over-abstraction for a pure helper.

**Rationale**:

- Keeps both presentation projects a single `AddBuildoutCore` call away
  from the new surface. Tests that build a service collection by hand
  call the same extension and pick up the new registrations
  automatically.
- Interfaces only where multiple implementations are plausible (the
  service for testing-via-substitute; the formatter for the byte-
  contract; the title renderer for trivial mocking when not under
  test).

**Alternatives considered**:

- *Register everything as transient* — rejected: all four classes are
  immutable and stateless; singletons amortise construction.
- *Skip the `ITitleRenderer` interface* — rejected: tests for the
  formatter benefit from substituting the title renderer to verify the
  formatter's contract independently of rich-text mapping. Kept the
  interface despite a single implementation.

## R12 — Test mocking strategy (carry-over from feature 002)

**Decision**: Use `NSubstitute` to mock `IBuildinClient` directly in
both unit and integration tests. Do NOT exercise the Kiota
`HttpMessageHandler` layer in this feature's tests — that path is
already covered by feature 001's `MockedHttpHarnessTests`. The
mapping fix to `MapV1SearchResponse` is covered in
`BotBuildinClientTests` against a Kiota-shaped fixture, exactly how
feature 002 covered its three `MapPage` / `MapRichText` /
`MapBlockChildrenResponse` fixes.

**Rationale**:

- Mocking `IBuildinClient` is the cheapest, most readable way to set
  up search-result fixtures across many test cases.
- Exercising the HTTP layer for every search test would be slow and
  redundant — the mapping is covered once, in one focused test file.
- This matches the pattern feature 002 followed for `IBuildinClient`-
  level tests; consistency reduces cognitive load.

**Alternatives considered**:

- *Drive integration tests through a real `BotBuildinClient` against
  recorded HTTP fixtures* — rejected: heavier tooling for no
  proportional gain; feature 001 already proved the HTTP layer.
- *Roll a hand-written test double for `IBuildinClient`* —
  `NSubstitute` is already a project dependency; rolling our own
  doubles would duplicate that dependency.

## Summary table

| Decision | Outcome | Reference |
|---|---|---|
| Search endpoint | `IBuildinClient.SearchPagesAsync` | R1 |
| Mapping fix | `MapV1SearchResponse` populates `Title` + `Parent` | R2 |
| Scope filter | In-memory ancestor walk, on-demand `GetPageAsync` for missing ancestors | R3 |
| Plain body | TSV `<id>\t<type>\t<title>\n`, "(untitled)" for empty title | R4 |
| Styled CLI | `Spectre.Console.Table` over the same body | R5 |
| MCP tool | Attribute-registered, returns the body string verbatim | R6 |
| CLI command | `AsyncCommand<>` mirroring `GetCommand`; new exit code 2 for invalid args | R7 |
| Pagination | Sequential `do…while` on `NextCursor`, archived filtered after | R8 |
| Empty query | `ArgumentException` from core; both surfaces translate | R9 |
| LLM test | Sibling `LlmCanFindAndReadPage` chains search → read | R10 |
| DI | Singletons via `AddBuildoutCore` | R11 |
| Mocking | `NSubstitute` for `IBuildinClient`; HTTP layer untouched | R12 |
