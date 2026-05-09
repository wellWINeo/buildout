# Contract: `IDatabaseViewRenderer` (Buildout.Core)

The single core entry point that both the CLI and the MCP adapters
consume. Lives in `src/Buildout.Core/DatabaseViews/IDatabaseViewRenderer.cs`.

## Surface

```csharp
public interface IDatabaseViewRenderer
{
    Task<string> RenderAsync(
        DatabaseViewRequest request,
        CancellationToken cancellationToken = default);

    Task<string> RenderInlineAsync(
        string databaseId,
        CancellationToken cancellationToken = default);
}
```

`RenderAsync` is used by the `db view` CLI command and the
`database_view` MCP tool. `RenderInlineAsync` is used exclusively by
`ChildDatabaseConverter` to expand `child_database` blocks during
page reads; it always renders the table style, emits a `##
<database title>` sub-section heading instead of the standalone
metadata header, and accepts no style / group-by / date-property
parameters.

The returned string is **always plain** (no terminal escape codes).
Callers that want TTY styling layer it on top.

## Behavior

1. **Validate** `request` against the rules in `data-model.md`. On
   failure throw `DatabaseViewValidationException` carrying the
   offending field name and a list of valid alternatives derived
   from the schema where applicable.
2. **Fetch** the database via `IBuildinClient.GetDatabaseAsync`.
3. **Paginate** rows via `IBuildinClient.QueryDatabaseAsync` until
   `HasMore == false`, accumulating all rows into a single
   `IReadOnlyList<DatabaseRow>` in the order returned.
4. **Dispatch** to the matching `IDatabaseViewStyle` strategy
   keyed on `request.Style`.
5. **Return** the strategy's rendered string, prefixed by the
   metadata header line(s) defined in `design-sketches.md`.

The renderer MUST NOT call any other `IBuildinClient` method. This
is asserted by the WireMock contract test
(`buildin-endpoints.md`).

## Error semantics

| Source                                     | Type surfaced                       | Notes                            |
|--------------------------------------------|-------------------------------------|----------------------------------|
| Validation failure                         | `DatabaseViewValidationException`   | thrown before any network call   |
| Buildin 404 on `GET database`              | `BuildinApiException` (existing)    | propagated unchanged             |
| Buildin 401 / 403                          | `BuildinApiException`               | propagated unchanged             |
| Buildin transport / timeout                | `BuildinApiException` subclass      | propagated unchanged             |
| Cancellation                               | `OperationCanceledException`        | propagated; partial rows discarded |

The renderer never returns a partially rendered string on error: it
either returns the complete output or throws.

## Dependencies (DI)

Registered in `ServiceCollectionExtensions.AddBuildoutCore` alongside
the existing renderers. New singletons:

- `IDatabaseViewRenderer` → `DatabaseViewRenderer`
- `IPropertyValueFormatter` → `PropertyValueFormatter`
- One singleton per style strategy keyed by `DatabaseViewStyle`,
  resolved via a `Func<DatabaseViewStyle, IDatabaseViewStyle>` or a
  keyed-singleton lookup table.

The renderer takes the following constructor dependencies:

- `IBuildinClient`
- `IPropertyValueFormatter`
- `IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>` (or
  equivalent style lookup)

## Read-only invariant

The implementation MUST NOT call any of the following
`IBuildinClient` methods on any code path:
`CreatePageAsync`, `UpdatePageAsync`, `UpdateBlockAsync`,
`DeleteBlockAsync`, `AppendBlockChildrenAsync`,
`CreateDatabaseAsync`, `UpdateDatabaseAsync`. Asserted by SC-007 /
the contract test under `tests/Buildout.IntegrationTests/Cross/`.

The same invariant applies to `RenderInlineAsync` and to the new
`ChildDatabaseConverter`. The page-read pipeline now triggers
`GET database` + `POST query` per `child_database` block found, but
no other buildin method.

## Per-block error isolation (inline path only)

`RenderInlineAsync` MAY throw on the same error classes as
`RenderAsync`. The expectation is that `ChildDatabaseConverter`
catches those exceptions and emits the placeholder lines documented
in `data-model.md` rather than propagating them. The page-read
pipeline therefore continues even when individual `child_database`
blocks fail to expand. The standalone `db view` command and the
`database_view` MCP tool still surface those exceptions normally
(they have no per-block isolation requirement).
