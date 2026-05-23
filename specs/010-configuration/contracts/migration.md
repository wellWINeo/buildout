# Contract: Migration from Pre-010 Configuration

This document is the source of truth for the migration warnings
emitted by `UnknownKeyAuditor` and for the "Migration from earlier
versions" section in `docs/configuration.md`. Any change to legacy
key handling MUST update both surfaces in the same PR.

## Rename table

| Pre-010 key (env / config) | Pre-010 channel | New key (config-key syntax) | New env var |
|----------------------------|-----------------|------------------------------|-------------|
| `Buildin:BotToken` | JSON only (CLI bound `Buildin:BotToken` via raw env-var provider) | `BotToken` | `Buildout__BotToken` |
| `Buildin:BaseUrl` | JSON only | `BaseUrl` | `Buildout__BaseUrl` |
| `Buildin:HttpTimeout` | JSON only | `Http:Timeout` | `Buildout__Http__Timeout` |
| `Buildin:UnsafeAllowInsecure` | JSON only | `Http:UnsafeAllowInsecure` | `Buildout__Http__UnsafeAllowInsecure` |
| `PageEditor:LargeDeleteThreshold` | JSON only | `Limitations:LargeDeleteThreshold` | `Buildout__Limitations__LargeDeleteThreshold` |
| `BUILDOUT_TELEMETRY_ENABLED` | env var only | `Telemetry:Enabled` | `Buildout__Telemetry__Enabled` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | env var only (industry standard) | `Telemetry:OtlpEndpoint` | `Buildout__Telemetry__OtlpEndpoint` (env var continues to be honoured as low-precedence fallback — see schema.md) |

## Auditor warning text

For each legacy key in the table above (except
`OTEL_EXPORTER_OTLP_ENDPOINT`, which remains supported), the warning
text is:

```text
[buildout-config] Ignored unknown configuration key '<legacy_key>'. \
                  Use '<new_key>' instead (or env var '<new_env>'). \
                  See docs/configuration.md "Migration from earlier versions".
```

For unknown keys NOT in the rename table, the warning is:

```text
[buildout-config] Ignored unknown configuration key '<key>'. \
                  See docs/configuration.md for the supported schema.
```

## Behaviour summary

| Legacy key set | New key also set? | Outcome |
|----------------|-------------------|---------|
| Yes | Yes | New key wins (it lives in the recognised schema; the legacy key has no provider that recognises it). One warning logged naming the legacy key and pointing at the new key. |
| Yes | No | Legacy value is dropped. If the legacy key was the only source of a required value (e.g., the only `BotToken` source), `BuildinClientOptionsValidator.ValidateOnStart` fails with the existing "required" message. Warning logged. |
| No | Yes | No warning. Normal behaviour. |
| No | No | No warning. Normal behaviour. |

The `OTEL_EXPORTER_OTLP_ENDPOINT` env var is a special case: when set
without a corresponding `Buildout__Telemetry__OtlpEndpoint`, it is
HONOURED (not just warned about) via `LegacyOtelEndpointSource`. This
is the only legacy key that retains semantic effect, per FR-009 /
FR-010.

## Compatibility window

This feature does NOT carry a dual-recognition window for the
non-OTel legacy keys. The first release containing 010-configuration
drops support for the old keys outright; users who do not update see
warnings and either get the right defaults (and continue) or hit a
validator failure (and update). This is acceptable because:

- The pre-010 schema was never documented to users.
- The repo's buildout binaries are pre-1.0.
- Carrying dual-recognition logic would persistently dilute Principle
  VII (single loader, single schema).

A `breaking-changes.md` entry SHOULD accompany this feature's release
notes naming each removed key. (Out of scope for this feature's
implementation tasks; tracked at release time.)
