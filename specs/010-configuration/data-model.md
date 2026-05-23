# Phase 1 Data Model: Unified Configuration

This document captures the bound-options shapes, the loader's return
contract, and the option/validator pairings produced by the feature.
All shapes are in-memory and per-process; nothing is persisted by the
feature itself.

## Bound options

### `BuildinClientOptions` (existing — modified)

Location: `src/Buildout.Core/Buildin/BuildinClientOptions.cs`.

| Property | Type | Default | Config key (env / JSON) |
|----------|------|---------|--------------------------|
| `BotToken` | `string` | `""` (validator rejects empty) | `Buildout__BotToken` / `BotToken` |
| `BaseUrl` | `Uri` | `https://api.buildin.ai/` | `Buildout__BaseUrl` / `BaseUrl` |
| `HttpTimeout` | `TimeSpan` | `00:00:30` | `Buildout__Http__Timeout` / `Http:Timeout` (remapped — see R4) |
| `UnsafeAllowInsecure` | `bool` | `false` | `Buildout__Http__UnsafeAllowInsecure` / `Http:UnsafeAllowInsecure` (remapped — see R4) |

The C# property names stay as they are today. The config keys
`Http:Timeout` and `Http:UnsafeAllowInsecure` are projected onto
`HttpTimeout` and `UnsafeAllowInsecure` by `HttpSectionRemapSource`
before binding. See [research.md R4](./research.md#r4--mapping-config-keys-httptimeout-to-c-properties-httptimeout).

**Validator**: existing `BuildinClientOptionsValidator` retains its
rules verbatim. Failure messages are updated to reference the new
config keys (e.g., the timeout failure now says `Http:Timeout must be
a positive duration.` not `HttpTimeout must be a positive duration.`).

### `TelemetryOptions` (new)

Location: `src/Buildout.Core/Diagnostics/TelemetryOptions.cs`.

| Property | Type | Default | Config key |
|----------|------|---------|------------|
| `Enabled` | `bool` | `false` | `Buildout__Telemetry__Enabled` / `Telemetry:Enabled` |
| `OtlpEndpoint` | `Uri` | `http://localhost:4318` | `Buildout__Telemetry__OtlpEndpoint` / `Telemetry:OtlpEndpoint` |

`OtlpEndpoint` additionally falls back to the legacy
`OTEL_EXPORTER_OTLP_ENDPOINT` env var at a precedence layer BELOW the
`Buildout__`-prefixed env-var layer (see
[research.md R5](./research.md#r5--otel_exporter_otlp_endpoint-low-precedence-fallback)).

**Validator**: new `TelemetryOptionsValidator`:

- `OtlpEndpoint` MUST be an absolute URI.
- `OtlpEndpoint` MUST use `http` or `https` scheme.
- No constraint on `Enabled`.

### `LimitationsOptions` (new — renamed from `PageEditorOptions`)

Location: `src/Buildout.Core/Markdown/Editing/LimitationsOptions.cs`.

| Property | Type | Default | Config key |
|----------|------|---------|------------|
| `LargeDeleteThreshold` | `int` | `10` | `Buildout__Limitations__LargeDeleteThreshold` / `Limitations:LargeDeleteThreshold` |

**Validator**: new `LimitationsOptionsValidator`:

- `LargeDeleteThreshold` MUST be `>= 0`.

The existing `PageEditorOptions` class is kept in place as an
`[Obsolete("Use LimitationsOptions. PageEditorOptions will be removed in a
future release.")]` thin forwarder so any third-party reference at the
type-name level keeps compiling with a warning. The CLR-level type is
retained only for one release window and then removed (tracked outside
this feature).

## Loader contract

### `BuildoutConfiguration.Build(string[] args)`

Location: `src/Buildout.Core/Configuration/BuildoutConfiguration.cs`.

```csharp
public static class BuildoutConfiguration
{
    public static (IConfiguration Configuration, string[] ResidualArgs) Build(string[] args);
}
```

**Inputs**:

- `args` — the unmodified `string[]` the presentation entry point
  received from the OS / launcher.

**Outputs**:

- `Configuration` — a fully-assembled, frozen
  `Microsoft.Extensions.Configuration.IConfiguration` carrying the
  layered chain described in FR-002.
- `ResidualArgs` — `args` with the `--config <value>` /
  `--config=<value>` / `-c <value>` / `-c=<value>` tokens removed, in
  original order. If `args` had no config flag, `ResidualArgs` is
  reference-equal to `args`.

**Side effects**:

- May read one file from disk
  (`~/.config/buildout/config.json` or the `--config` path).
- Reads any env var with prefix `Buildout__` and the legacy
  `OTEL_EXPORTER_OTLP_ENDPOINT`.
- Calls `UnknownKeyAuditor.Audit(IConfiguration)` which logs a single
  warning per unknown key via a logger acquired through a small
  fallback chain (DI logger if available, otherwise
  `Console.Error.WriteLine`).

**Errors** (all surface as `BuildoutConfigurationException`):

- `--config` was supplied but the path does not exist or is not a file.
- A loaded JSON file is not valid JSON (parse error from
  `Microsoft.Extensions.Configuration.Json`).
- A loaded JSON file is not readable (permission denied surfaced as
  `IOException` wrapped in `BuildoutConfigurationException`).

**Validator failures** are raised separately, via
`OptionsValidationException` thrown by `ValidateOnStart` during
`builder.Build()`, not by `Build` itself.

### `BuildoutConfigurationException`

Location: `src/Buildout.Core/Configuration/BuildoutConfigurationException.cs`.

Single exception type carrying:

- `Message`: human-readable error text suitable for printing to
  stderr verbatim.
- `Path`: the offending file path (when relevant), null otherwise.
- `InnerException`: the underlying `FileNotFoundException`,
  `IOException`, `JsonException`, etc. (preserved for diagnostics).

## Configuration provider chain (assembly order)

Lower entries override higher entries (Microsoft.Extensions.Configuration
"last wins" semantics):

1. In-code defaults (the property defaults on `BuildinClientOptions`,
   `TelemetryOptions`, `LimitationsOptions`).
2. JSON file at `~/.config/buildout/config.json` if it exists AND no
   `--config` flag was supplied. `optional: true`.
3. JSON file at the `--config <path>` location if the flag was
   supplied. `optional: false` — missing file is a hard error.
4. `LegacyOtelEndpointSource` (contributes `Telemetry:OtlpEndpoint`
   from `OTEL_EXPORTER_OTLP_ENDPOINT` if set).
5. `EnvironmentVariablesConfigurationProvider` with prefix
   `Buildout__`.
6. `HttpSectionRemapSource` (projects `Http:Timeout` → `HttpTimeout`
   and `Http:UnsafeAllowInsecure` → `UnsafeAllowInsecure`; runs LAST
   so it sees the fully-merged value of `Http:Timeout` regardless of
   which earlier layer set it).

## Internal types

### `ConfigFlagParser`

Location: `src/Buildout.Core/Configuration/ConfigFlagParser.cs`.

```csharp
internal static class ConfigFlagParser
{
    public static (string? ConfigPath, string[] Residual) Extract(string[] args);
}
```

Pure function; no side effects. Recognises the four supported forms
(`--config <v>`, `--config=<v>`, `-c <v>`, `-c=<v>`). Last occurrence
wins on duplicates.

### `LegacyOtelEndpointSource`

Location: `src/Buildout.Core/Configuration/LegacyOtelEndpointSource.cs`.

`IConfigurationSource` + `IConfigurationProvider` pair contributing
exactly one key (`Telemetry:OtlpEndpoint`) if and only if the
process env var `OTEL_EXPORTER_OTLP_ENDPOINT` is set and non-empty.

### `HttpSectionRemapSource`

Location: `src/Buildout.Core/Configuration/HttpSectionRemapSource.cs`.

`IConfigurationSource` + `IConfigurationProvider` pair. The provider
holds a back-reference to the parent `IConfigurationRoot`; at `Load()`
time it re-reads `Http:Timeout` and `Http:UnsafeAllowInsecure` from
the root and projects them as `HttpTimeout` and `UnsafeAllowInsecure`.
Re-runs whenever the root signals a reload (which it won't, since the
loader is single-shot — but the implementation supports it for
testability).

### `UnknownKeyAuditor`

Location: `src/Buildout.Core/Configuration/UnknownKeyAuditor.cs`.

```csharp
internal static class UnknownKeyAuditor
{
    public static void Audit(IConfiguration configuration, ILogger logger);
}
```

Walks the configuration's flat key set, diffs against the canonical
schema (the union of the FR-009 keys), and emits one warning per
extraneous key. For keys in the `LegacyKeyHints` table, the warning
text additionally names the new replacement key.

## Relationships

```text
Buildout.Cli/Program.cs               Buildout.Mcp/Program.cs
        |                                       |
        v                                       v
   BuildoutConfiguration.Build(args) — same helper, same chain
        |
        +---> ConfigFlagParser.Extract(args) -> (path?, residual)
        |
        +---> ConfigurationBuilder
        |         |
        |         +-- (defaults via options binder)
        |         +-- AddJsonFile(default-or-override, optional)
        |         +-- LegacyOtelEndpointSource
        |         +-- AddEnvironmentVariables(prefix: "Buildout__")
        |         +-- HttpSectionRemapSource
        |
        +---> UnknownKeyAuditor.Audit(config, logger)
        |
        +---> return (IConfiguration, residualArgs)

ServiceCollectionExtensions.AddBuildoutCore + AddBuildinClient
        |
        +---> services.AddOptions<BuildinClientOptions>()
        |        .Bind(configuration).ValidateOnStart()
        +---> services.AddOptions<TelemetryOptions>()
        |        .Bind(configuration.GetSection("Telemetry")).ValidateOnStart()
        +---> services.AddOptions<LimitationsOptions>()
                 .Bind(configuration.GetSection("Limitations")).ValidateOnStart()
```

## State

The loader is stateless. No globals, no static caches, no
process-wide singletons beyond the DI container's own
`IOptions<T>` registrations. A subsequent call to
`BuildoutConfiguration.Build` with different `args` produces an
independent `IConfiguration` — used by tests to exercise the
loader in isolation from the running presentation.
