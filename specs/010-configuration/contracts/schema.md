# Contract: Configuration Schema (Source of Truth)

This document is the authoritative key inventory the docs-lint test
diffs `docs/configuration.md` against (SC-005). Any change here MUST
also update `docs/configuration.md` AND the corresponding options
class in source, in the same PR.

## Canonical key table

| Key (JSON path / config-key syntax) | Type | Default | Required | Validation |
|--------------------------------------|------|---------|----------|------------|
| `BotToken` | `string` | — | **yes** | non-empty / non-whitespace |
| `BaseUrl` | URI string | `https://api.buildin.ai/` | no | absolute URI; HTTPS unless `Http:UnsafeAllowInsecure=true` |
| `Http:Timeout` | `TimeSpan` (`HH:MM:SS`) | `00:00:30` | no | > `00:00:00` |
| `Http:UnsafeAllowInsecure` | `bool` | `false` | no | — |
| `Limitations:LargeDeleteThreshold` | `int` | `10` | no | `>= 0` |
| `Telemetry:Enabled` | `bool` | `false` | no | — |
| `Telemetry:OtlpEndpoint` | URI string | `http://localhost:4318` | no | absolute URI; `http`/`https` scheme only |

## Env-var form (reference)

Each key's env-var form is `Buildout__` + the key with `:` replaced
by `__`:

| Config key | Env var |
|------------|---------|
| `BotToken` | `Buildout__BotToken` |
| `BaseUrl` | `Buildout__BaseUrl` |
| `Http:Timeout` | `Buildout__Http__Timeout` |
| `Http:UnsafeAllowInsecure` | `Buildout__Http__UnsafeAllowInsecure` |
| `Limitations:LargeDeleteThreshold` | `Buildout__Limitations__LargeDeleteThreshold` |
| `Telemetry:Enabled` | `Buildout__Telemetry__Enabled` |
| `Telemetry:OtlpEndpoint` | `Buildout__Telemetry__OtlpEndpoint` |

## JSON form (reference)

```json
{
  "BotToken": "ntn_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "BaseUrl": "https://api.buildin.ai/",
  "Http": {
    "Timeout": "00:00:30",
    "UnsafeAllowInsecure": false
  },
  "Limitations": {
    "LargeDeleteThreshold": 10
  },
  "Telemetry": {
    "Enabled": false,
    "OtlpEndpoint": "http://localhost:4318"
  }
}
```

## Implementation property mapping

For readers of the source code: the C# property names diverge from
the JSON `Http` section by design (see
[research.md R4](../research.md#r4--mapping-config-keys-httptimeout-to-c-properties-httptimeout)).
The `HttpSectionRemapSource` projects between them at loader-build
time.

| Config key | C# property on bound type |
|------------|---------------------------|
| `BotToken` | `BuildinClientOptions.BotToken` |
| `BaseUrl` | `BuildinClientOptions.BaseUrl` |
| `Http:Timeout` | `BuildinClientOptions.HttpTimeout` (via remap) |
| `Http:UnsafeAllowInsecure` | `BuildinClientOptions.UnsafeAllowInsecure` (via remap) |
| `Limitations:LargeDeleteThreshold` | `LimitationsOptions.LargeDeleteThreshold` |
| `Telemetry:Enabled` | `TelemetryOptions.Enabled` |
| `Telemetry:OtlpEndpoint` | `TelemetryOptions.OtlpEndpoint` |

## Unknown keys

Any root-level JSON key or `Buildout__`-prefixed env var not in the
canonical table above yields a single startup warning logged by
`UnknownKeyAuditor`. Unknown keys are not loaded into any options
instance. Known legacy keys (see
[migration.md](./migration.md)) get a warning that names the new
key.
