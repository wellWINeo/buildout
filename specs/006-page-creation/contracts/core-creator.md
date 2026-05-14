# Contract: `IPageCreator` (core)

**Feature**: [spec.md](../spec.md) · [plan.md](../plan.md)

The single core entry point for page creation. CLI and MCP adapters
call this and translate its result; neither may bypass it (Principle I,
spec FR-001).

---

## Surface

```csharp
namespace Buildout.Core.Markdown.Authoring;

public interface IPageCreator
{
    Task<CreatePageOutcome> CreateAsync(
        CreatePageInput input,
        CancellationToken cancellationToken = default);
}
```

`CreatePageInput`, `CreatePageOutcome`, `ParentKind`, and
`PartialCreationException` are documented in [data-model.md](../data-model.md).

---

## Pre-conditions (raised as `ArgumentException` → validation error class)

- `input.ParentId` is non-empty.
- `input.Markdown` is non-null. May be empty *only* if `input.Title`
  is set; otherwise the resolved title would be unknown.
- `input.Title`, after `Trim()`, is non-empty when set.
- `input.Icon`, when set, is either a single grapheme cluster or a
  well-formed absolute URI.
- `input.CoverUrl`, when set, is a well-formed absolute URI.
- `input.Properties`, when non-empty, requires the parent kind probed
  in step 1 to be `Database`. (Otherwise a validation error is
  raised after the probe but before any write call.)
- Property names in `input.Properties` exist in the database schema.
  Property kinds are within the v1 supported set (R6). Values parse
  per their kind.

---

## Operation sequence

1. **Probe parent kind** via `IBuildinClient.GetPageAsync(input.ParentId)`
   first; on `BuildinApiException` with 404, fall back to
   `IBuildinClient.GetDatabaseAsync(input.ParentId)`. Other 4xx/5xx
   surface immediately as the matching failure class (auth, transport,
   unexpected) — no write is attempted.

2. **Validate** property names and values against the schema if the
   parent is a database, and reject `input.Properties` if the parent
   is a page. Validation errors raised here are pre-write; no buildin
   write call has been issued.

3. **Parse the markdown** via `IMarkdownToBlocksParser.Parse(...)`
   into an `AuthoredDocument`. Apply title resolution (R2): explicit
   title wins; otherwise consume the leading H1 if present; otherwise
   validation error.

4. **Build the `CreatePageRequest`**: `Parent` set to
   `ParentPageId(input.ParentId)` or `ParentDatabaseId(input.ParentId)`
   per the probe; `Properties` set to the title property (always) plus
   any database property values; `Children` set to the first 100
   top-level blocks (or fewer if the body is shorter).

5. **Issue `CreatePageAsync(request)`**. On error, propagate the
   buildin failure class. On success, capture the new page's id; this
   is the value returned in `CreatePageOutcome.NewPageId`.

6. **Append remaining top-level blocks**: if the body has more than
   100 top-level blocks, slice the remainder into ≤100-element
   batches and issue `AppendBlockChildrenAsync(newPageId, batch)` for
   each batch in order. Any failure here is a partial-creation
   failure (throws `PartialCreationException`).

7. **Append nested levels** (R4): for every top-level block returned
   by buildin that had children in the in-memory tree, walk the
   children recursively. At each level, batch ≤100 children per
   `AppendBlockChildrenAsync(parent_block_id, batch)`. Any failure
   here is a partial-creation failure (throws
   `PartialCreationException` with the same `NewPageId`).

8. **Return** a `CreatePageOutcome` with `FailureClass = null` on full
   success.

---

## Failure-class taxonomy

| Failure class | Raised by | CLI exit | MCP error code |
|---|---|---|---|
| `Validation` | Pre-write `ArgumentException` (pre-conditions above) | 2 | `InvalidParams` |
| `NotFound` | Probe — both `GetPageAsync` and `GetDatabaseAsync` return 404 | 3 | `ResourceNotFound` |
| `Auth` | Any buildin call returns 401 or 403 | 4 | `InternalError` (mirrors existing tools) |
| `Transport` | Underlying `TransportError` from `BuildinApiException` | 5 | `InternalError` (mirrors existing tools) |
| `Unexpected` | Any other `BuildinApiException` or unhandled exception | 6 | `InternalError` |
| `Partial` | `PartialCreationException` from steps 6/7 | 6 (with stderr id, per R8) | `InternalError` (with id in message, per R8) |

`Validation`, `NotFound`, and `Auth` failure classes raised by
**probe** (step 1) reach the adapter without any write call having been
issued. `Validation` raised by **property parsing** (step 2) also
pre-dates any write.

---

## Constitution alignment

- **Principle I**: all logic in core; adapters call this interface only.
- **Principle V**: consumes the existing `IBuildinClient` only; no new
  methods, no Bot-specific dependencies.
- **Principle VI**: writes one new page; no `updateBlock`,
  `updatePage`, `deleteBlock`, `updateDatabase`, or `createDatabase`
  call appears in any code path. Verified by
  `tests/Buildout.IntegrationTests/Cross/CreatePageReadOnlyOnExistingDataTests.cs`.
