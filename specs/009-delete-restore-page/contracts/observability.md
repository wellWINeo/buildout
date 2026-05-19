# Contract — Observability (spec 007 integration)

This feature extends spec 007's existing operation and tool inventories with two
operation-label values and two tool-label values. **No new metric names, span names, or
`error_type` values are introduced** (FR-010 in spec.md).

## Operation-level instrumentation (core)

Both `PageLifecycle.DeleteAsync` and `PageLifecycle.RestoreAsync` are wrapped in
`OperationRecorder` per spec 007.

| Operation name | Tags on success | Tags on failure |
|--|--|--|
| `page_delete` | `outcome=success`, `changed=true|false`, `status_code=200` | `outcome=failure`, `error_type=…`, `status_code=<code>` |
| `page_restore` | `outcome=success`, `changed=true|false`, `status_code=200` | `outcome=failure`, `error_type=…`, `status_code=<code>` |

These appear on the existing spec 007 instruments:

- `buildout.operations.total` (counter)
- `buildout.operation.duration` (histogram, seconds)

### `error_type` vocabulary (reused, no additions)

| Mapped from | `error_type` |
|--|--|
| `BuildinApiException(ApiError { StatusCode: 404 })` | `not_found` |
| `BuildinApiException(ApiError { StatusCode: 401 or 403 })` | `auth` |
| `BuildinApiException(TransportError)` | `transport` |
| `BuildinApiException(ApiError)` (other 4xx/5xx) | `unexpected` |
| `BuildinApiException(UnknownError)` | `unexpected` |
| Uncaught exception (`OperationRecorder.Dispose` fallback) | `unknown` |

These four values (`not_found`, `auth`, `transport`, `unexpected`) plus the
`unknown` fallback are already in use by spec 006/007/008. No new values.

### New tag: `changed`

The `changed` boolean tag is a new operation-level dimension introduced by this feature.
It distinguishes state-changing calls (`changed=true`) from idempotent no-op
short-circuits (`changed=false`) without inflating the `operation` cardinality.

This tag is added to the recorder via `recorder.SetTag("changed", changed)` and flows
through `OperationRecorder.BuildTagList` onto both the counter and histogram. The
existing tag-allowlist in `BuildTagList` already passes arbitrary tags (everything
except `outcome` and `duration_ms`), so no code change to `OperationRecorder` is
required.

## Tool-level instrumentation (MCP)

Both `DeletePageToolHandler` and `RestorePageToolHandler` emit the existing MCP
instruments per the spec 006 `CreatePageToolHandler` pattern.

| Tool name | Tags on success | Tags on failure |
|--|--|--|
| `delete_page` | `tool=delete_page`, `outcome=success` | `tool=delete_page`, `outcome=failure` |
| `restore_page` | `tool=restore_page`, `outcome=success` | `tool=restore_page`, `outcome=failure` |

These appear on the existing instruments:

- `buildout.mcp.tool.invocations.total` (counter)
- `buildout.mcp.tool.duration` (histogram)

## Logs

`OperationRecorder` emits `LogDebug` on start, `LogInformation` on success, `LogError`
on failure (spec 007's existing template strings). The two new operation names appear
verbatim:

```text
Operation page_delete started
Operation page_delete completed in 87.42ms changed=true status_code=200
Operation page_delete failed with error_type not_found status_code 404 in 42.10ms
Operation page_restore completed in 73.18ms changed=false status_code=200
```

No new log-template strings are added. Operators grepping `Operation page_delete` see
both successes and failures.

## Verification

- `PageLifecycleTests` injects a `Microsoft.Extensions.Logging.Testing.FakeLogger` (or
  the existing `RecordingLogger` test helper from spec 007 tests) and asserts the
  emitted log lines and their tags. Specifically: that `changed` appears as a tag on
  the success path with the correct value; that `error_type` matches the mapping table
  on each failure path.
- `Buildout.IntegrationTests/Diagnostics/PageLifecycleMetricsTests.cs` (if added)
  asserts on metric emissions using the existing `Buildout.IntegrationTests/Diagnostics`
  helpers (e.g. `MetricCollectorScope`). Optional — coverage of the same signal exists
  through the unit-test assertions.
