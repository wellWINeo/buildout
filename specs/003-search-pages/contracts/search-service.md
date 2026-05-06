# Contract — `ISearchService`

The single seam between `Buildout.Core` and the two presentation
projects for this feature. Both `Buildout.Mcp` and `Buildout.Cli`
consume only this interface (plus `ISearchResultFormatter`) for search.

## Public surface

Located at `src/Buildout.Core/Search/ISearchService.cs`.

```text
namespace Buildout.Core.Search;

public interface ISearchService
{
    Task<IReadOnlyList<SearchMatch>> SearchAsync(
        string query,
        string? pageId,
        CancellationToken cancellationToken = default);
}
```

The default implementation is `SearchService` in the same namespace,
constructed with `IBuildinClient`, `ITitleRenderer`,
`AncestorScopeFilter`, and `ILogger<SearchService>`. Registration via
`ServiceCollectionExtensions.AddBuildoutCore(...)`.

## Behavioural contract

### Inputs

- `query` — the user's opaque search string. Must be non-null and
  non-whitespace; the service validates and rejects empty queries
  before any buildin call.
- `pageId` — optional buildin page UUID (`null` or omitted means
  "search the entire workspace"). When provided, it MUST take any form
  `IBuildinClient.GetPageAsync` accepts. Validation of the format is
  delegated to the client.
- `cancellationToken` — propagated to every `IBuildinClient` call
  inside the pagination loop and the ancestor walk. The service does
  not call `cancellationToken.ThrowIfCancellationRequested()` itself;
  cancellation arrives as `OperationCanceledException` from the
  client.

### Outputs

A non-null `IReadOnlyList<SearchMatch>`:

- **No matches**: empty list. Successful outcome.
- **Unscoped (pageId is null)**: every non-archived match buildin
  returned for the query, in buildin's response order.
- **Scoped (pageId is non-null)**: a strict subset of the unscoped
  list — only matches that ARE the scope page or are descendants of
  the scope page in buildin's parent hierarchy. Order preserved
  relative to the unscoped subset.

### Validation (FR-008 / SC-006)

The service trims `query` and rejects an empty/whitespace string with
`ArgumentException("Query must be non-empty.", nameof(query))` BEFORE
any `IBuildinClient` call is made. Tests assert this invariant by
counting recorded interactions on the substituted client.

### Pagination (FR-002)

The service:

1. Calls `IBuildinClient.SearchPagesAsync(new PageSearchRequest { Query = query })`.
2. Appends every returned `Page` to a working list, dropping any with
   `Archived == true` (FR-007).
3. If `HasMore == true`, repeats step 1 with `StartCursor =
   NextCursor` and appends.
4. Continues until `HasMore == false`.

`PageSize` is left null (uses the buildin server default). v1 does not
tune page size; spec FR-002 forbids partial fetches.

### Scope filter (FR-004 / R3)

When `pageId` is provided, the service:

1. Builds `parentLookup` by inserting every match's
   `(PageId → Parent)` pair (after pagination, before filtering).
2. For each match, calls `AncestorScopeFilter.IsInScopeAsync(match,
   pageId, parentLookup, ct)`.
3. Keeps only matches the filter returned `true` for.

Order is preserved — the filter is `Where`-shaped, never re-orders.

### Error contract

Errors propagate from `IBuildinClient` as `BuildinApiException` with
the typed `BuildinError` discriminator (`NotFound`, `ApiError`,
`TransportError`, `UnknownError`):

- The service does **not** catch `BuildinApiException`. It bubbles
  out; the caller (`SearchCommand` / `SearchToolHandler`) maps it to
  surface conventions.
- The service does **not** catch `OperationCanceledException`. It
  bubbles out unchanged.
- The service throws `ArgumentException` only for the empty-query
  validation in FR-008.
- The ancestor walk catches `BuildinApiException` with `NotFound` or
  `Forbidden` per scope-fetch and treats the affected match as out of
  scope (R3); transport / unexpected errors during the ancestor walk
  bubble out unchanged.

### Determinism

For a fixed sequence of `IBuildinClient` responses, `SearchAsync` MUST
return a list whose elements compare equal in the same order on
repeat invocations. Verified by a test that calls `SearchAsync` twice
against the same mock and asserts `Assert.Equal(first, second)`.

### Order

Matches are returned in buildin's response order; pagination boundaries
are invisible. The service never sorts, deduplicates, or re-ranks.

## Internal collaborators (informational)

- `SearchService` — orchestrates validate → paginate → filter. The
  only place that touches `IBuildinClient` directly for the unscoped
  phase.
- `TitleRenderer` (via `ITitleRenderer`) — turns
  `IReadOnlyList<RichText>?` into the plain-text `DisplayTitle`. Used
  inside the loop after each `Page` is received, not at the end.
- `AncestorScopeFilter` — applies the descendant filter when
  `pageId` is non-null. May call `IBuildinClient.GetPageAsync` for
  parent ids missing from the seeded lookup.

These are not part of the public surface and may be refactored freely;
only `ISearchService.SearchAsync` is contractual.

## Test obligations

Unit tests live under `tests/Buildout.UnitTests/Search/`:

| Test class | Path | Purpose |
|---|---|---|
| `SearchServiceTests.Validation` | `…/Search/SearchServiceTests.cs` | Empty-query / whitespace-query throws `ArgumentException` and records zero interactions on the substituted client. |
| `SearchServiceTests.Pagination` | (same file) | Multi-page search response is fully drained; merged list contains all pages in arrival order; archived pages excluded. |
| `SearchServiceTests.UnscopedHappyPath` | (same file) | A query returning a typed page with title, parent, archived=false produces a `SearchMatch` with the expected fields. |
| `SearchServiceTests.ScopedFilterApplied` | (same file) | Given a fixture with three matches A/B/C where B and C have `Parent = ParentPage(A_id)`, calling `SearchAsync(q, A_id)` returns A,B,C; calling `SearchAsync(q, "unrelated")` returns []. |
| `AncestorScopeFilterTests.*` | `…/Search/AncestorScopeFilterTests.cs` | Direct match (id == scope), one-hop descendant via seeded lookup, multi-hop via on-demand `GetPageAsync`, missing ancestor (NotFound) → false, cycle in parent metadata → false + debug log, ParentWorkspace / ParentDatabase → false. |
| `TitleRendererTests.*` | `…/Search/TitleRendererTests.cs` | Null title → "(untitled)"; empty list → "(untitled)"; single text segment → segment content; multi-segment → concatenated content; tab characters replaced with single space; mention rich-text uses `Content` (plain text, no markup). |
| `DependencyInjectionTests.SearchSeams` | `…/Search/DependencyInjectionTests.cs` | All four search seams resolve from a `ServiceCollection` after `AddBuildoutCore`. |

Every test in this section uses `NSubstitute` to mock `IBuildinClient`
directly. No HTTP layer is exercised in this feature's unit tests.

## Configuration / DI

`ServiceCollectionExtensions.AddBuildoutCore(...)` (existing) is
extended to register:

```text
services.AddSingleton<ITitleRenderer, TitleRenderer>();
services.AddSingleton<AncestorScopeFilter>();
services.AddSingleton<ISearchService, SearchService>();
services.AddSingleton<ISearchResultFormatter, SearchResultFormatter>();
```

Each presentation project picks up the registrations via the existing
`AddBuildoutCore` call in its `Program.cs`.
