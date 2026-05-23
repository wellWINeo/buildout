# Phase 0 Research: Unified Configuration

This document consolidates the answers to the R1–R10 research items
enumerated in [plan.md](./plan.md) under "Phase 0: Research".

All ten items have been resolved; no `NEEDS CLARIFICATION` markers
remain.

## R1 — Spectre.Console.Cli "global option" pattern

**Decision**: Introduce `Buildout.Cli.Commands.BuildoutCommandSettings :
CommandSettings` with `[CommandOption("-c|--config")] string? ConfigPath`.
Every existing `*Settings` class (`CreateSettings`, `DbSettings`,
`DbViewSettings`, `DeleteSettings`, `RestoreSettings`,
`UpdateSettings`, and the inline settings declared by `GetCommand` /
`SearchCommand`) is refactored to inherit from `BuildoutCommandSettings`.

**Rationale**: Spectre.Console.Cli's parser does not support a true
global option. The framework only ever binds options against a
command-scoped `Settings` class, so a flag that is "available
everywhere" must be declared on a base class that every command's
settings extends. The actual configuration-loading work happens in
`Program.cs` before `app.RunAsync(residualArgs)` — the per-command
`ConfigPath` property exists solely so Spectre's help output documents
the flag on every command and so `--help` shows it as part of each
command's surface.

**Alternatives considered**:

- Use `IRemainingArguments` to scoop up the flag at the command-handler
  level — rejected because the flag would no longer show up in
  per-command help, violating the discoverability intent expressed in
  FR-005 / SC-001.
- Fork Spectre or write a custom parser layer — rejected as
  disproportionate to a four-character flag.

## R2 — Pre-parse of `--config` for the loader

**Decision**: `Buildout.Core.Configuration.ConfigFlagParser.Extract(string[] args)`
performs a single linear scan over `args`, recognises the four supported
forms (`--config <value>`, `--config=<value>`, `-c <value>`,
`-c=<value>`), removes both tokens from the array, and returns
`(string? configPath, string[] residual)`. The order of non-flag args is
preserved; ties (`--config` supplied twice) take the last occurrence,
matching the Microsoft.Extensions.Configuration "last wins" semantics
already in play for the rest of the chain.

**Rationale**: Both presentations need the same parse, and the parse
must run before either argument parser (Spectre, Host) sees the
residual args. Implementing it once in `Buildout.Core` (constitution
Principle I — shared configuration bootstrap lives in core) avoids
divergence.

**Alternatives considered**:

- Read `Environment.GetCommandLineArgs()` from inside the loader and
  reach over the caller's `args` array — rejected because it hides the
  data flow and breaks test isolation.
- Reuse Spectre's tokenizer via reflection — rejected because Spectre's
  internal parser is not a stable public surface.

## R3 — Composing the loader with `Host.CreateApplicationBuilder` in MCP

**Decision**: After constructing `var builder =
Host.CreateApplicationBuilder(residualArgs)`, immediately call
`builder.Configuration.Sources.Clear()` and then
`builder.Configuration.AddConfiguration(prebuilt)` to splice the loader's
`IConfiguration` in as the host's sole configuration source. DI
consumers of `IConfiguration` continue to work unchanged; the host's
default discovery (e.g., a stray `appsettings.json` in `pwd`) is
suppressed.

**Rationale**: The host builder's `IConfigurationManager` is explicitly
mutable for exactly this case. The two-line `Clear` + `AddConfiguration`
pattern is symmetric to the CLI's `services.AddSingleton<IConfiguration>(config)`
and keeps both presentations' `Program.cs` files visually parallel.

**Alternatives considered**:

- Wrap `builder.Configuration` via `ConfigureAppConfiguration` — same
  outcome with more ceremony; rejected for readability.
- Write a custom `IConfigurationSource` that defers to the prebuilt
  `IConfiguration` — rejected as net-equivalent and more code.

## R4 — Mapping config keys (`Http:Timeout`) to C# properties (`HttpTimeout`)

**Decision**: Keep `BuildinClientOptions`'s existing flat property
layout (`HttpTimeout`, `UnsafeAllowInsecure`). Introduce a tiny
`HttpSectionRemapSource : IConfigurationSource` (≈ 30 lines plus its
`HttpSectionRemapProvider`) that, *after* the rest of the chain is
assembled, materialises two derived keys: `HttpTimeout` from
`Http:Timeout`, and `UnsafeAllowInsecure` from
`Http:UnsafeAllowInsecure`. The standard binder then populates the
existing C# properties without changes. The remap source is the only
place that knows about the rename.

**Rationale**: The configurable surface (env vars + JSON keys) is what
users see — and `Http:Timeout` is what the spec promises (FR-009). The
internal C# property name is an implementation detail. Renaming the
properties on `BuildinClientOptions` would churn `BotBuildinClient`,
`ServiceCollectionExtensions`, and ten test files in
`Buildout.UnitTests/Buildin/`, none of which is part of this feature's
scope (constitution Scope Discipline). The remap source contains the
churn to one new file plus its own unit tests.

**Alternatives considered**:

- Add `BuildinHttpOptions { Timeout, UnsafeAllowInsecure }` as a nested
  property — rejected for blast radius; if a future feature genuinely
  models HTTP options as a sub-struct, this becomes a localised
  refactor at that point.
- Use `IPostConfigureOptions<BuildinClientOptions>` to copy values
  after binding — rejected because it runs after `ValidateOnStart`,
  defeating the fail-fast goal.

## R5 — `OTEL_EXPORTER_OTLP_ENDPOINT` low-precedence fallback

**Decision**: Implement `LegacyOtelEndpointSource : IConfigurationSource`
in `Buildout.Core.Configuration`. The source contributes exactly one
key — `Telemetry:OtlpEndpoint` — sourced at provider-load time from
`Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")`,
contributing **nothing** if the env var is unset or empty (no
empty-string poison). The source is registered into the chain ONCE,
between the default-file layer and the `Buildout__`-prefixed env-var
layer. Microsoft.Extensions.Configuration's later-wins precedence then
gives exactly the order FR-009 (row 7) requires:
defaults < default-file < OTEL legacy < `Buildout__` env.

**Rationale**: The OTel env var is an industry-standard
vendor-neutral key that operators routinely set in container
environments; removing it would surprise users with OpenTelemetry
collector / sidecar deployments. Wiring it through the loader (rather
than a side-channel imperative read) keeps Principle VII intact: every
config flows through the loader's chain.

**Alternatives considered**:

- Read the legacy env var imperatively inside `TelemetryOptionsValidator`
  — rejected because validators see only the already-bound options
  instance and cannot influence precedence.
- Hardcode the fallback at the call site in `Buildout.Mcp/Program.cs`
  — rejected as a Principle VII violation (ad-hoc config source).

## R6 — `Microsoft.Extensions.Configuration.EnvironmentVariables` prefix semantics

**Decision**: Use the stock
`Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationProvider`
with `prefix: "Buildout__"`. The provider strips the prefix AND treats
`__` as the section separator on what remains, so
`Buildout__Http__Timeout=00:01:00` materialises as config key
`Http:Timeout` — exactly what `HttpSectionRemapSource` then projects
to the flat property name `HttpTimeout`.

**Rationale**: The stock provider's behaviour is documented and
stable. Re-implementing prefix stripping by hand violates Principle
VII ("use Microsoft.Extensions.Configuration") and introduces a
parallel parser that would inevitably drift.

**Verification**: covered by
`Buildout.UnitTests/Configuration/BuildoutConfigurationTests.EnvVar_DoubleUnderscoreIsSectionSeparator`.

**Alternatives considered**: writing a custom env provider — rejected
as duplication.

## R7 — Migration error semantics

**Decision**: `UnknownKeyAuditor` runs after binding and walks the
loader's flattened key set. Any key not in the FR-009 schema yields a
single warning, logged via `ILogger<BuildoutConfiguration>`. The
auditor carries a hardcoded `LegacyKeyHints` table mapping known
legacy keys to their new replacements:

| Legacy key | Replacement |
|------------|-------------|
| `Buildin:BotToken` | `BotToken` |
| `Buildin:BaseUrl` | `BaseUrl` |
| `Buildin:HttpTimeout` | `Http:Timeout` |
| `Buildin:UnsafeAllowInsecure` | `Http:UnsafeAllowInsecure` |
| `PageEditor:LargeDeleteThreshold` | `Limitations:LargeDeleteThreshold` |
| `BUILDOUT_TELEMETRY_ENABLED` (env var) | `Buildout__Telemetry__Enabled` |

For these specifically, the warning text names the new key explicitly.
For other unknown keys, the warning text reports the key and a link to
`docs/configuration.md`. The auditor never errors — a user mid-migration
may legitimately have both forms set; hard-erroring would punish them.

**Rationale**: The right behaviour for a typo on a misnamed key and a
stale env var from a prior version is the same: surface it, don't act
on it. Errors are reserved for unrecoverable problems (missing
required key, parse failure, missing `--config` file).

**Alternatives considered**:

- Hard-error on any legacy key — rejected, punishes migration users.
- Silently ignore — rejected, the spec explicitly requires the
  unknown-key warning (FR-009).

## R8 — Docs-lint test design (SC-005)

**Decision**: Place `DocumentationKeysTests.cs` in
`Buildout.UnitTests/Configuration/`. The test:

1. Locates `docs/configuration.md` by walking up from
   `AppContext.BaseDirectory` until a directory containing
   `Buildout.sln` is found, then resolves
   `<repo>/docs/configuration.md`.
2. Parses the markdown for a code-fenced table whose header row exactly
   matches the canonical FR-009 column set
   (`| Key | Type | Default | Required | Validation |`).
3. Extracts the first column of each data row to build the
   "documented keys" set.
4. Computes the "schema keys" set by reflecting on
   `BuildinClientOptions`, `TelemetryOptions`, and
   `LimitationsOptions`, including nested properties at one level
   (`Http:Timeout`, `Http:UnsafeAllowInsecure`,
   `Telemetry:Enabled`, …).
5. Asserts the two sets are equal; on failure, shows the symmetric
   difference (`extra in docs`, `missing from docs`).

**Rationale**: A test enforces FR-011 (schema reference completeness)
without humans having to remember. The repo-root walk-up is the same
mechanism the existing WireMock fixture uses to find its fixture
files, so this is already a vetted pattern.

**Alternatives considered**: a Roslyn analyser — rejected for vastly
disproportionate complexity.

## R9 — Secret-scrub test design (SC-007)

**Decision**: Place `SecretLeakTests.cs` in
`Buildout.IntegrationTests/Configuration/`. The test sets
`Buildout__BotToken=DO_NOT_LEAK_BUILDOUT_TOKEN_123` as a process env
var, runs both presentations through their real `Program.cs` entry
point (the CLI runs `buildout-cli --help`; the MCP runs a single
startup-shutdown cycle via the existing MCP test fixture), captures
stdout/stderr/logger output, and asserts the sentinel literal does
NOT appear anywhere in captured output.

**Rationale**: This exercises the real loader, the real DI graph, and
the real `BuildinClientOptionsValidator` paths. The validator's
existing failure message
(`BuildinClientOptions.BotToken is required.`) carries no value text;
this test enforces that no future change introduces a leak. The
sentinel form is deliberately distinctive so an accidental partial
match (e.g., the literal `BotToken` showing up in help text) is not a
false positive.

**Alternatives considered**:

- Static-analysis pass over all log strings — rejected, gives false
  negatives for runtime-formatted messages.
- Unit-test each logger call site — rejected, doesn't catch new call
  sites added in future features.

## R10 — `appsettings.json` discovery in MCP

**Decision**: After `builder.Configuration.Sources.Clear()`, the host
no longer probes `pwd` for `appsettings.json`. This is intentional:
the buildout repo has never carried such a file (confirmed by
`find . -name appsettings.json -not -path '*/bin/*' -not -path '*/obj/*'`
returning nothing), and an operator dropping an `appsettings.json`
into their working directory would otherwise inject untracked
configuration that bypasses the loader's audit and validation. This
behaviour is documented in `docs/configuration.md` under "Common
pitfalls" so the absence of `appsettings.json` discovery is explicit.

**Rationale**: Hidden discovery is exactly the ad-hoc-source failure
mode constitution Principle VII forbids.

**Alternatives considered**:

- Keep `appsettings.json` discovery — rejected, would have created
  exactly the bypass Principle VII is designed to prevent.
