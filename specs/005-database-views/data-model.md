# Data Model: Database Views (Read-Only)

This document captures only the **in-memory shapes** introduced by
this feature. No new buildin entities are added. No new
`IBuildinClient` methods or DTOs are added.

The renderer consumes existing types unchanged:

- `Database` (`src/Buildout.Core/Buildin/Models/Database.cs`)
- `PropertySchema` and 14 sealed subclasses
- `PropertyValue` and 14 sealed subclasses (one per property kind)
- `QueryDatabaseRequest` and `QueryDatabaseResult`
  (`Results`, `HasMore`, `NextCursor`)

## New types (all internal to `Buildout.Core/DatabaseViews/`)

### `DatabaseViewStyle` (enum)

```csharp
public enum DatabaseViewStyle
{
    Table,
    Board,
    Gallery,
    List,
    Calendar,
    Timeline,
}
```

`Table` is the default when no style is specified by the caller.

### `DatabaseViewRequest` (record)

The validated input to the renderer.

```csharp
public sealed record DatabaseViewRequest(
    string DatabaseId,
    DatabaseViewStyle Style,
    string? GroupByProperty,    // required iff Style == Board
    string? DateProperty);      // required iff Style ∈ {Calendar, Timeline}
```

**Validation rules** (enforced by the renderer before any network call):

- `DatabaseId` MUST be non-empty.
- `Style` MUST be a valid enum member.
- If `Style == Board`, `GroupByProperty` MUST be non-null and MUST
  name a property in the database's schema whose type is one of:
  `select`, `multi-select`, `checkbox` (R6). Buildin "status"
  columns are exposed as ordinary `select` properties and are
  therefore valid via the `select` rule.
- If `Style ∈ {Calendar, Timeline}`, `DateProperty` MUST be non-null
  and MUST name a property whose type is one of: `date`,
  `created_time`, `last_edited_time` (R7).
- For other styles, `GroupByProperty` and `DateProperty` MAY be null
  and MUST be ignored.

Validation failures throw a single new exception type
`DatabaseViewValidationException` (extends `ArgumentException` for
ergonomics) carrying the offending field name and the valid
alternatives derived from the schema. The CLI and MCP adapters map
this to their respective validation-error surfaces (exit code 2 /
MCP `InvalidParams`).

### `DatabaseViewResult` (record)

Internal result of the renderer's orchestration phase, fed into the
chosen style strategy.

```csharp
internal sealed record DatabaseViewResult(
    Database Database,
    IReadOnlyList<DatabaseRow> Rows);
```

### `DatabaseRow` (record)

```csharp
internal sealed record DatabaseRow(
    string PageId,
    IReadOnlyDictionary<string, PropertyValue> Properties);
```

`PageId` is captured for stable test assertions; the renderer does
not display it unless explicitly requested by a future option (out
of scope here).

### `CellBudget` (record)

```csharp
internal sealed record CellBudget(
    int MaxCharacters,         // 24 (R5)
    string EllipsisMarker);    // "…"
```

Truncation rule: if `value.Length > MaxCharacters`, take the first
`MaxCharacters - EllipsisMarker.Length` characters and append the
marker. Whitespace is *not* trimmed before measuring, so trailing
spaces still count.

### Per-style intermediate shapes

Internal to each style strategy. None of these escape `Buildout.Core`.

- `TableLayout` — a chosen list of column property names, in display
  order, and the per-column header strings; plus a `bool Stacked`
  computed from R5.
- `BoardGroup` — `(string Label, IReadOnlyList<DatabaseRow> Rows)`.
  Groups are produced in the order they first appear, except for
  the `(none)` group which is appended last.
- `GalleryCard` — `(string Title, IReadOnlyList<(string Name, string
  Value)> SecondaryProperties, string CoverPlaceholder)`. At most
  three secondary properties per card.
- `CalendarBucket` — `(DateOnly? Date, IReadOnlyList<DatabaseRow>
  Rows)`. `null` denotes the `(undated)` bucket; rendered last.
- `TimelineEntry` — `(DateOnly Start, DateOnly? End, DatabaseRow
  Row)`. Sorted ascending by `Start`. Entries without a start date
  are placed under an `(undated)` bucket at the end.

### `PropertyValueFormatter` contract

```csharp
internal interface IPropertyValueFormatter
{
    string Format(PropertyValue value, CellBudget budget);
}
```

Dispatch is on the runtime subclass of `PropertyValue`. The mapping
is exhaustive (R4); a subclass not enumerated falls through to a
documented default `[unsupported]` placeholder, but every subclass
present in the existing models is enumerated explicitly so this
fallthrough is unreachable in practice (asserted by a unit test that
iterates every subclass via reflection).

## Page-read integration

### `ChildDatabaseBlock` (new model)

Added to the existing `Block` discriminated hierarchy in
`src/Buildout.Core/Buildin/Models/Block.cs` alongside paragraph,
heading, list, todo, code, quote, divider, image, embed, and table.

```csharp
public sealed record ChildDatabaseBlock(
    string Id,
    DateTimeOffset CreatedTime,
    DateTimeOffset LastEditedTime,
    bool Archived,
    string DatabaseId,
    string? Title)
    : Block(Id, CreatedTime, LastEditedTime, Archived, "child_database");
```

`DatabaseId` is the id of the embedded database. `Title` is the
denormalized title carried by buildin's payload; it is used for the
inline `## <title>` heading without requiring a `GET database`
round-trip just to know the name. (The renderer still issues
`GET database` to retrieve the schema; `Title` is purely a fast-path
display aid and a fallback when the database is inaccessible.)

### `IDatabaseViewRenderer.RenderInlineAsync` (extension to renderer surface)

Adds one method to the renderer interface:

```csharp
Task<string> RenderInlineAsync(
    string databaseId,
    CancellationToken cancellationToken = default);
```

Behaves like `RenderAsync` with `Style = Table` and no group-by /
date-property, but emits a sub-section heading (`## <title>`) instead
of the standalone view's metadata header. Used exclusively by the
new `ChildDatabaseConverter`.

### `ChildDatabaseConverter` (new converter)

Implements `IBlockToMarkdownConverter`. Registered as a singleton in
`AddBuildoutCore` alongside the existing converters. Constructor
takes `IDatabaseViewRenderer`. On a `ChildDatabaseBlock`:

1. Calls `RenderInlineAsync(block.DatabaseId, ct)`.
2. On `BuildinApiException` (404 / 401 / 403 / transport / generic)
   or `DatabaseViewValidationException`, catches and emits a
   single-line placeholder per the table below.
3. Returns the markdown to the page-render pipeline, which
   substitutes it at the block's position.

| Exception                          | Placeholder line                            |
|------------------------------------|---------------------------------------------|
| 404                                | `[child database: not found — <Title>]`     |
| 401 / 403                          | `[child database: access denied — <Title>]` |
| Transport / timeout                | `[child database: transport error — <Title>]` |
| Generic `BuildinApiException`      | `[child database: not accessible — <Title>]` |
| Malformed block (no `DatabaseId`)  | `[child database: malformed]`               |

`<Title>` falls back to `(unknown)` when the block carries no
`Title`. These placeholders are documented and stable; tests pin
them.

## State transitions

None. The feature is read-only and purely functional: input
parameters → buildin reads → rendered string. There is no persistent
state and no client-observable transition. The page-read integration
preserves this — embedded expansion is just additional reads in the
same code path, never a write.

## Relationships

```text
DatabaseViewRequest ──validated by──▶ DatabaseViewRenderer
                                          │
              ┌───────────────────────────┴────────────────────────────┐
              │                                                        │
              ▼                                                        ▼
   IBuildinClient.GetDatabaseAsync       IBuildinClient.QueryDatabaseAsync (loop)
              │                                                        │
              └─────────────────────► DatabaseViewResult ◄──────────────┘
                                          │
                                          ▼
                                  IDatabaseViewStyle (1 of 6)
                                          │
                                          ▼
                                  IPropertyValueFormatter
                                          │
                                          ▼
                                       string  (the rendered view)
```

The string flows out unchanged to the CLI command (which may layer
TTY styling on top) and to the MCP tool (which returns it verbatim).
