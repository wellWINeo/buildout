# Contract — Buildin endpoints used by this feature

## Endpoints

| Method | Path | OperationId | `IBuildinClient` method | Used by |
|--|--|--|--|--|
| `GET` | `/v1/pages/{page_id}` | `getPage` | `GetPageAsync(string pageId, ...)` | Pre-read in both `DeleteAsync` and `RestoreAsync` (P1 in `core-lifecycle.md`). |
| `PATCH` | `/v1/pages/{page_id}` | `updatePage` | `UpdatePageAsync(string pageId, UpdatePageRequest request, ...)` | Issued only when the pre-read shows the page is not in the target state (P2 in `core-lifecycle.md`). |

**No new methods are added to `IBuildinClient`.** Both methods exist as of feature 001
and have been used by features 006 and 008. Per Constitution Principle V, the service
talks to the interface only.

## Request body for the PATCH

```json
{"archived": true}
```

or

```json
{"archived": false}
```

**No other top-level fields are present.** `properties`, `icon`, `cover` are unset and
therefore omitted by the Kiota-generated serializer, which causes buildin's server to
leave them as-is (research.md R1).

## Response shape

The PATCH and GET responses both return the buildin `Page` schema
(`openapi.json` `#/components/schemas/Page`), which the existing
`BotBuildinClient.MapPage` already projects into `Buildout.Core.Buildin.Models.Page`.
The lifecycle service only consumes the `Archived`, `Id`, and (for log/telemetry
purposes) `Url` fields.

## WireMock stubs (integration tests)

The `BuildinStubs` helper in `tests/Buildout.IntegrationTests/Buildin/` already includes
a `RegisterUpdatePage` builder from spec 008's update implementation. This feature adds
two helpers if not already present:

### `RegisterGetPageArchived(pageId, archived)`

Returns a JSON page body with the given `archived` flag. Used as the pre-read stub.

### `RegisterUpdatePageToggleArchived(pageId)`

Records the inbound PATCH body, asserts it contains exactly the `archived` field with
no `properties`/`icon`/`cover`, and returns a page body with the `archived` value set
to whatever the request specified. Used for the state-changing path.

### Error stubs (already in the harness)

- `RegisterGetPageNotFound(pageId)` → 404 from spec 002.
- `RegisterPatchPageNotFound(pageId)` → 404 from spec 008.
- `RegisterPatchPageAuthFailure(pageId)` → 401/403 from spec 008.
- `RegisterPatchPageServerError(pageId)` → 5xx from spec 008.

The transport-error case is covered by the existing
`Buildout.IntegrationTests/Buildin/TransportFailureSimulation.cs` fixture.

## Call sequence

### Happy path (state change)

```text
PageLifecycle.DeleteAsync(pageId)
  ├─ IBuildinClient.GetPageAsync(pageId)       → Page { Archived = false, ... }
  ├─ IBuildinClient.UpdatePageAsync(pageId, { Archived = true })  → Page { Archived = true, ... }
  └─ return PageLifecycleOutcome { Archived = true, Changed = true }
```

### No-op short-circuit

```text
PageLifecycle.DeleteAsync(pageId)
  ├─ IBuildinClient.GetPageAsync(pageId)       → Page { Archived = true, ... }
  └─ return PageLifecycleOutcome { Archived = true, Changed = false }
```

Zero PATCH calls. Verifiable by counting calls on a recording `IBuildinClient` substitute
and by asserting on the WireMock journal in integration tests.

### Error during pre-read

```text
PageLifecycle.DeleteAsync(pageId)
  ├─ IBuildinClient.GetPageAsync(pageId)       → throws BuildinApiException(ApiError 404)
  └─ return PageLifecycleOutcome { FailureClass = NotFound, UnderlyingException = ... }
```

### Error during PATCH

```text
PageLifecycle.DeleteAsync(pageId)
  ├─ IBuildinClient.GetPageAsync(pageId)       → Page { Archived = false, ... }
  ├─ IBuildinClient.UpdatePageAsync(...)       → throws BuildinApiException(...)
  └─ return PageLifecycleOutcome { FailureClass = ..., UnderlyingException = ... }
```

In both error scenarios, the outcome's `Changed` is `false` and `Archived` is `null`.
