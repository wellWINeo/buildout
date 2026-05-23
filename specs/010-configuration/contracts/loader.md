# Contract: `BuildoutConfiguration.Build(args)`

## Signature

```csharp
namespace Buildout.Core.Configuration;

public static class BuildoutConfiguration
{
    public static (IConfiguration Configuration, string[] ResidualArgs)
        Build(string[] args);
}
```

## Inputs

| Parameter | Type | Notes |
|-----------|------|-------|
| `args` | `string[]` | The unmodified `string[]` the entry point received from the OS / launcher. May be empty. May contain `--config <v>`, `--config=<v>`, `-c <v>`, `-c=<v>` zero or more times. Other elements are preserved verbatim. |

## Outputs

| Field | Type | Notes |
|-------|------|-------|
| `Configuration` | `IConfiguration` | Frozen at the moment `Build` returns. Carries the full FR-002 chain. |
| `ResidualArgs` | `string[]` | `args` with `--config` / `-c` tokens removed (both the flag and its value). Order preserved. If no flag was present, reference-equal to `args`. |

## Layers (lower wins = listed first, higher wins = listed last)

1. **Defaults** — property defaults on `BuildinClientOptions`,
   `TelemetryOptions`, `LimitationsOptions`. Bound by
   `services.AddOptions<T>().Bind(...).ValidateOnStart()` at DI time,
   not by `Build`.
2. **Default JSON file** — `~/.config/buildout/config.json` if it
   exists AND no `--config` flag was supplied. `optional: true`.
3. **CLI-overridden JSON file** — the path supplied via `--config <v>`
   or `-c <v>`. `optional: false`. Replaces (not supplements) layer 2.
4. **Legacy OTel endpoint** — contributes `Telemetry:OtlpEndpoint`
   from `OTEL_EXPORTER_OTLP_ENDPOINT` only if that env var is set and
   non-empty.
5. **`Buildout__`-prefixed environment variables** — standard
   `EnvironmentVariablesConfigurationProvider` with prefix
   `Buildout__`. `__` is the section separator.
6. **HTTP section remap** — projects `Http:Timeout` →
   `HttpTimeout` and `Http:UnsafeAllowInsecure` →
   `UnsafeAllowInsecure` so the existing `BuildinClientOptions`
   property names bind. Runs LAST so it sees the merged value.

## Error taxonomy

All errors surface as `BuildoutConfigurationException` with a
human-readable `Message`:

| Trigger | `Message` example | `Path` |
|---------|-------------------|--------|
| `--config` path does not exist | `Configuration file not found: /etc/buildout/missing.json` | `/etc/buildout/missing.json` |
| `--config` path is a directory | `Configuration path is not a file: /etc/buildout/` | `/etc/buildout/` |
| `--config` file permission denied | `Configuration file is not readable: /etc/buildout/locked.json` | `/etc/buildout/locked.json` |
| JSON parse error in any loaded file | `Configuration file is not valid JSON: /home/user/.config/buildout/config.json (line 4, column 12)` | the offending file's path |

`UnknownKeyAuditor` warnings are NOT errors. Validator failures (from
`ValidateOnStart`) are surfaced as
`OptionsValidationException` by the DI container, not by `Build`.

## Side-effect contract

`Build` is **not** pure:

- Reads up to one file from disk (the default file or the
  `--config` path; never both).
- Reads process env vars matching `Buildout__*` and
  `OTEL_EXPORTER_OTLP_ENDPOINT`.
- Writes zero or more warning log lines through a fallback logger
  chain: tries to resolve `ILogger<BuildoutConfiguration>` via a
  pre-supplied factory if the caller arranged one (tests do), else
  falls back to `Console.Error.WriteLine`.

`Build` does NOT:

- Touch the file system other than reading the JSON file once.
- Cache results between invocations — every call rebuilds.
- Mutate any process-wide state.
- Throw `OptionsValidationException` (that comes from DI later).

## Thread safety

`Build` is reentrant. Callers SHOULD invoke it once per process at
startup. The returned `IConfiguration` is thread-safe for reads (the
standard contract from
`Microsoft.Extensions.Configuration`).

## Compatibility with `Host.CreateApplicationBuilder`

In `Buildout.Mcp/Program.cs`, the integration pattern is:

```csharp
var (config, residualArgs) = BuildoutConfiguration.Build(args);
var builder = Host.CreateApplicationBuilder(residualArgs);
builder.Configuration.Sources.Clear();
builder.Configuration.AddConfiguration(config);
```

This guarantees the host's default `appsettings.json` discovery is
suppressed (FR-002 chain only — no ad-hoc sources).

In `Buildout.Cli/Program.cs`, the integration pattern is:

```csharp
var (config, residualArgs) = BuildoutConfiguration.Build(args);
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddBuildinClient(config);
services.AddBuildoutCore();
// ... existing Spectre.Console.Cli wiring with residualArgs
await app.RunAsync(residualArgs);
```
