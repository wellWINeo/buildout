# Phase 1 — Data Model: Page Search

This document captures the data shapes that flow through the search
pipeline, plus the additive mapping fix to `Buildout.Core/Buildin/`. All
new shapes live in `Buildout.Core` — none in presentation projects.

## Entities

### `SearchMatch` (new)

Located at `src/Buildout.Core/Search/SearchMatch.cs`.

```text
public sealed record SearchMatch
{
    public required string PageId { get; init; }
    public required SearchObjectType ObjectType { get; init; }
    public required string DisplayTitle { get; init; }
    public Parent? Parent { get; init; }
    public bool Archived { get; init; }
}

public enum SearchObjectType { Page, Database }
```

| Field | Type | Notes |
|---|---|---|
| `PageId` | `string` | Buildin page UUID, lowercase + hyphenated (`Guid.ToString("D")`). Always populated. |
| `ObjectType` | `SearchObjectType` enum | `Page` or `Database`. Mapped from `V1SearchPageResult.Object` ("page" → `Page`; "database" → `Database`; anything else triggers an `UnknownError`-class `BuildinApiException` to surface unexpected schema changes — but in practice buildin only sends those two values). |
| `DisplayTitle` | `string` | Plain-text title. `(untitled)` placeholder when buildin's title was null or empty. Tab characters replaced with single spaces (the only escape rule). Never null. |
| `Parent` | `Parent?` | Reused hand-written discriminated union (`ParentDatabase`, `ParentPage`, `ParentBlock`, `ParentWorkspace`) from feature 001. Optional — buildin may omit parent metadata in odd cases; the scope filter treats `null` parent as "out of scope" once the chain exits. |
| `Archived` | `bool` | Mirrors `Page.Archived`. The service filters archived matches *out* by default, so any `SearchMatch` reaching the formatter has `Archived == false`. The field is retained on the record so a future opt-in can light up without a model change. |

**Construction**: `SearchService` constructs `SearchMatch` records by
calling `TitleRenderer.RenderPlain(page.Title)` for the display title,
and copying `Id` / `Parent` / `Archived` directly from the `Page`
returned by the (fixed) `BotBuildinClient.MapV1SearchResponse`. The
`ObjectType` value is sourced from `V1SearchPageResult.Object` via the
mapping update in R2.

### `Page` (modified — additive mapping fix)

Located at `src/Buildout.Core/Buildin/Models/Page.cs`. **No changes to
the record itself** — `Title` and `Parent` are already present from
feature 002. The change is in `BotBuildinClient.MapV1SearchResponse`,
which currently leaves both fields unset on emitted records.

After the fix, `MapV1SearchResponse` populates:

| Field | Source in Kiota response | Notes |
|---|---|---|
| `Title` | `V1SearchPageResult.Properties?.Title?.Title` (a `List<RichTextItem>`) | Mapped via the existing `MapRichText` helper (used by `MapPage` / `ExtractTitle` already). Result `IReadOnlyList<RichText>?`; null when the `properties.title` shape is absent on the wire. |
| `Parent` | `V1SearchPageResult.Parent` (composed-type wrapper) | Mapped via the existing `MapParent` helper. Result is a hand-written `Parent?` discriminator (`ParentDatabase`, `ParentPage`, `ParentBlock`, `ParentWorkspace`). |

`Id`, `CreatedAt`, `LastEditedAt`, and `Archived` continue to be
populated as today. No fields are removed; no field semantics change.
The mapping fix is verified by an extension to
`tests/Buildout.UnitTests/Buildin/BotBuildinClientTests.cs`.

### `SearchObjectType` (new — small enum)

Located in the same file as `SearchMatch`. `Page` and `Database` only
in v1. The serialiser inside the formatter uses
`ObjectType.ToString().ToLowerInvariant()` — `"page"` and `"database"`,
matching the buildin wire form.

## Service-internal shapes

These types exist only inside `Buildout.Core/Search/Internal/` and are
not part of the public surface. They are documented for readers; they
may be refactored freely.

### `AncestorScopeFilter`

```text
internal sealed class AncestorScopeFilter
{
    public AncestorScopeFilter(IBuildinClient client, ILogger<AncestorScopeFilter> logger);

    public async ValueTask<bool> IsInScopeAsync(
        SearchMatch match,
        string scopePageId,
        Dictionary<string, Parent?> parentLookup,
        CancellationToken ct);
}
```

**Behaviour**:

1. If `match.PageId == scopePageId` → in scope (true).
2. Otherwise walk via `match.Parent` → ancestor chain:
   - If current node id is `scopePageId` → true.
   - If current node parent is `null`, `ParentWorkspace`, or
     `ParentDatabase` (databases are not buildin pages) → false.
   - If current node parent is `ParentPage(id)` or `ParentBlock(id)`:
     - If `id` is in `parentLookup` → step to it.
     - Else fetch via `IBuildinClient.GetPageAsync(id, ct)` (catching
       `NotFound`/`Forbidden` as out-of-scope), populate
       `parentLookup[id]`, then step.
3. Cycle defence: maintain a per-call `HashSet<string> visited`. If the
   current id is already in `visited` → log debug, return false.

`parentLookup` is seeded by `SearchService` from the merged search
response (every match's `(PageId → Parent)` entry) before any filter
call.

### `TitleRenderer`

```text
internal sealed class TitleRenderer : ITitleRenderer
{
    public string RenderPlain(IReadOnlyList<RichText>? title);
}
```

**Behaviour**: walks `title` (a list of `RichText`), concatenates each
item's `Content`, returns the joined string. If `title` is null or
empty or every item has empty/null `Content`, returns the literal
`"(untitled)"`. Tab characters in the result are replaced with a single
space.

Annotations and mentions are NOT rendered — search-result titles are
plain text (no `**bold**`, no `[link](buildin://…)`); buildin already
populates `RichText.Content` with the displayed plain text for
mentions, which is exactly what the formatter needs. (Compare to
`InlineRenderer` from feature 002, which does emit Markdown markup;
that is the right behaviour for body content but the wrong behaviour
for one-line search titles.)

### Formatter line shape

The full grammar lives in `contracts/search-result-format.md`. In
brief:

```text
body  := match-line *
match-line := PAGE_ID TAB OBJECT_TYPE TAB TITLE LF
PAGE_ID   := lowercase-hex 8 "-" 4 "-" 4 "-" 4 "-" 12
OBJECT_TYPE := "page" | "database"
TITLE := any UTF-8, with TABs replaced by single spaces, never empty (placeholder "(untitled)")
TAB := U+0009
LF := U+000A
```

Empty body (`body.Length == 0`) iff zero matches.

## Validation rules

Validation is contract-level:

- **Query non-empty**: `ISearchService.SearchAsync` rejects with
  `ArgumentException("Query must be non-empty.", nameof(query))` when
  `string.IsNullOrWhiteSpace(query)`. SC-006: no buildin call may be
  made.
- **Scope page id**: passed through to the filter without validation
  in core; `IBuildinClient` is the validator. A malformed GUID raises
  whatever the client raises. A non-existent / forbidden scope page
  surfaces via `BuildinApiException(NotFound)` / `Forbidden`, mapped
  per FR-012.
- **Cancellation**: `CancellationToken` flows through every
  `IBuildinClient` call (search, ancestor `GetPageAsync`) and is
  honoured between pages.

## State transitions

Read-only feature. No state transitions in scope.

## Compatibility surface

Every field of `V1SearchPageResult` the search service reads is
documented here. If buildin adds fields (other than ones we explicitly
listed), the service MUST ignore them — the search behaviour does not
depend on additional fields, and any future use must be a deliberate,
spec-driven addition.

| `V1SearchPageResult` field | Used? | How |
|---|---|---|
| `Id` | ✅ | `SearchMatch.PageId` |
| `Object` | ✅ | `SearchMatch.ObjectType` |
| `Properties.Title.Title` | ✅ | `SearchMatch.DisplayTitle` (via `TitleRenderer`) |
| `Parent` | ✅ | `SearchMatch.Parent`; ancestor walk |
| `Archived` | ✅ | filter-out-by-default |
| `CreatedTime` | ➖ | mapped onto `Page.CreatedAt`; not used by the service or formatter in v1 |
| `LastEditedTime` | ➖ | mapped onto `Page.LastEditedAt`; not used by the service or formatter in v1 |
| `Properties.Title.Type` | ➖ | not used (always `"title"`) |
| `AdditionalData` | ➖ | not used |

Fields marked ➖ remain mapped (no behavioural change) so future
features can light them up without re-touching the mapper. They do not
appear in any rendered output for this feature.
