# Contract: `IBuildinClient`

This is the **public contract** that `Buildout.Core` exposes for buildin.ai
access. Every downstream feature (read, write, edit) and every presentation
project (`Buildout.Mcp`, `Buildout.Cli`) depends only on this contract and on
the typed models in `Buildout.Core/Buildin/Models/`.

## Scope

- One method per buildin operation declared in `openapi.json` (15 operations
  in v1; see `data-model.md`).
- Inputs are typed request objects or path parameters; bodies are never raw
  JSON.
- Outputs are typed responses; collection responses are paginated via
  `PaginatedList<T>` or operation-specific result wrappers.

## Method shape

```csharp
Task<TResponse> <Operation>Async(
    <typed inputs>,
    CancellationToken cancellationToken = default);
```

- All methods are `async` and accept a `CancellationToken`.
- All methods MUST honour `cancellationToken` — long-running operations cancel
  promptly and propagate `OperationCanceledException`.
- `DELETE` operations return `Task` (no payload).

## Error contract

On failure, methods throw `BuildinApiException` (no `try`-based result types
returned). The exception's `.Error` property is one of:

- `TransportError(Exception Cause)` — DNS, TCP, TLS, timeout, etc.
- `ApiError(int StatusCode, string? Code, string Message, string? RawBody)` —
  buildin returned a structured error response. `Code` and `Message` are
  populated when the response body is recognisable; `RawBody` is preserved for
  diagnostics.
- `UnknownError(int StatusCode, string RawBody)` — response did not match any
  known error schema.

`OperationCanceledException` propagates without wrapping.

## Authentication

Implementations receive credentials at construction (the token does NOT appear
in any method signature). A request-time provider model means rotating /
refreshing tokens does not require recomposing the client.

## Implementations

| Implementation | Auth | Status |
|---|---|---|
| `BotBuildinClient` | bearer (Bot token) | shipped in this feature |
| (future) `OAuthBuildinClient` | OAuth (User API) | not in this feature; interface keeps the door open per Constitution Principle V |

## Consumer-side guarantees

- The interface is **stable across regenerations** of the Kiota client. Adding
  or modifying buildin operations may add methods (additive, MINOR) but
  existing method signatures do not break under regeneration alone.
- Removing or changing a method's signature requires a normal source-edit PR
  on `IBuildinClient` and a corresponding implementation change. It is NOT a
  side-effect of running the regeneration script.
- Method names follow OpenAPI `operationId` (PascalCase + `Async` suffix).

## Test obligations

- Every method has at least one passing unit test that mocks `IRequestAdapter`
  and verifies the typed request → typed response mapping.
- Every method has at least one integration test that exercises the full
  HTTP serialisation path against a custom `HttpMessageHandler`.
- Error-mapping coverage: the three `BuildinError` variants each have at
  least one dedicated test.
