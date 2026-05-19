# Contract — `IPageLifecycle` core surface

## Interface

```csharp
namespace Buildout.Core.PageLifecycle;

public interface IPageLifecycle
{
    /// <summary>
    /// Mark the page as archived (soft-delete). Reversible via <see cref="RestoreAsync"/>.
    ///
    /// Behaviour:
    /// 1. Issue <c>IBuildinClient.GetPageAsync(pageId)</c> to read the current state.
    /// 2. If <c>page.Archived</c> is already <c>true</c>, return
    ///    <c>PageLifecycleOutcome { Archived = true, Changed = false }</c> — no PATCH.
    /// 3. Otherwise, issue <c>IBuildinClient.UpdatePageAsync(pageId, request)</c>
    ///    where <c>request</c> has <c>Archived = true</c> and every other field unset.
    /// 4. Return <c>PageLifecycleOutcome { Archived = true, Changed = true }</c> on success.
    ///
    /// Errors surface as a non-null <c>FailureClass</c>:
    ///   - 404 on either GET or PATCH → <c>FailureClass.NotFound</c>
    ///   - 401/403 on either GET or PATCH → <c>FailureClass.Auth</c>
    ///   - <see cref="TransportError"/> → <c>FailureClass.Transport</c>
    ///   - other <c>ApiError</c> / <c>UnknownError</c> → <c>FailureClass.Unexpected</c>
    /// </summary>
    Task<PageLifecycleOutcome> DeleteAsync(string pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark the page as not archived. Symmetric to <see cref="DeleteAsync"/>:
    /// 1. <c>GetPageAsync</c> first; if already <c>Archived = false</c>, no-op (Changed = false).
    /// 2. Otherwise <c>UpdatePageAsync</c> with <c>Archived = false</c> and every other field unset.
    /// </summary>
    Task<PageLifecycleOutcome> RestoreAsync(string pageId, CancellationToken cancellationToken = default);
}
```

## Implementation contract — `PageLifecycle`

The concrete implementation in `Buildout.Core.PageLifecycle.PageLifecycle` MUST satisfy
every property listed below. Each is asserted by a unit test in
`Buildout.UnitTests/PageLifecycle/PageLifecycleTests.cs`.

### P1: Pre-read invariant

Before any PATCH, the service MUST call `IBuildinClient.GetPageAsync(pageId)` exactly once
and inspect the returned page's `Archived` field. The pre-read result is the source of
truth for the `Changed` decision.

If `GetPageAsync` throws, the service MUST map the exception per the table below and
return the outcome immediately, without issuing the PATCH.

### P2: No-op short-circuit

If the pre-read page has `Archived == targetState`, the service MUST return
`PageLifecycleOutcome { PageId, Archived = targetState, Changed = false, FailureClass = null, UnderlyingException = null }`
and MUST NOT issue `UpdatePageAsync`. Verified by `PageLifecycleTests.NoOpShortCircuit_*`.

### P3: PATCH body shape

When `UpdatePageAsync` is called, the request object MUST satisfy:

- `request.Archived` is set to the target state.
- `request.Properties` is `null`.
- `request.Icon` is `null`.
- `request.Cover` is `null`.

Verified by `PageLifecycleTests.PatchBody_OnlyArchivedFieldSet` using
`NSubstitute.Arg.Is<UpdatePageRequest>(...)` to inspect the call.

### P4: OperationRecorder usage

Both methods MUST start an `OperationRecorder` with operation name `page_delete` /
`page_restore` respectively. The recorder MUST be:

- Set a `changed` tag (`true` on PATCH-issuing path, `false` on no-op short-circuit)
  via `recorder.SetTag("changed", changed)`.
- Set a `status_code` tag with the buildin response status code via
  `recorder.SetTag("status_code", code)` on the success path (`200`) and the failure
  paths (4xx/5xx).
- On success, call `recorder.Succeed()` exactly once before returning.
- On failure, call `recorder.Fail(error_type, status_code)` exactly once with the
  mapped value from the table below.
- Wrapped in a `using` declaration so the `Dispose` fallback catches any uncaught
  exception path and emits `Fail("unknown")`.

Verified by `PageLifecycleTests.OperationRecorder_*` injecting a substitute `ILogger`
and asserting the emitted log lines.

### P5: Error mapping

| Source | Detection | `FailureClass` | `error_type` tag | Exit-code (CLI) | MCP `McpErrorCode` |
|--|--|--|--|--|--|
| `BuildinApiException(ApiError { StatusCode: 404 })` | catch | `NotFound` | `not_found` | 3 | `ResourceNotFound` |
| `BuildinApiException(ApiError { StatusCode: 401 or 403 })` | catch | `Auth` | `auth` | 4 | `InternalError` |
| `BuildinApiException(TransportError)` | catch | `Transport` | `transport` | 5 | `InternalError` |
| `BuildinApiException(ApiError)` (other 4xx/5xx) | catch | `Unexpected` | `unexpected` | 6 | `InternalError` |
| `BuildinApiException(UnknownError)` | catch | `Unexpected` | `unexpected` | 6 | `InternalError` |
| Any other `Exception` | not caught (propagates) | n/a | n/a (recorder.Dispose → `unknown`) | unhandled | n/a |

Other-exception propagation matches `PageCreator`'s behaviour from spec 006 —
non-buildin exceptions bubble; the recorder's `Dispose` finaliser logs `error_type=unknown`.

### P6: No retries

The service MUST NOT retry any buildin call internally. A 5xx surfaces immediately as
`FailureClass.Unexpected` with the underlying exception attached.

### P7: Cancellation

The `cancellationToken` parameter MUST be passed through to both `GetPageAsync` and
`UpdatePageAsync` unchanged. The service MUST NOT swallow `OperationCanceledException`.

## Construction

The implementation depends on:

- `IBuildinClient` — the client interface.
- `ILogger<PageLifecycle>` — for `OperationRecorder.Start(logger, ...)`.

Registered in `ServiceCollectionExtensions.AddBuildoutCore()`:

```csharp
services.AddSingleton<IPageLifecycle, PageLifecycle>();
```

No options object is required. No `IConfiguration` binding is required (the operation
exposes no tunables).
