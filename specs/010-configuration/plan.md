# Implementation Plan: Unified Configuration

**Branch**: `010-configuration` | **Date**: 2026-05-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/010-configuration/spec.md`

## Summary

Consolidate today's three independent configuration call-sites
(`Buildout.Cli/Program.cs:9-13` building its own `ConfigurationBuilder`,
`Buildout.Mcp/Program.cs:12-21` using `Host.CreateApplicationBuilder` plus two
ad-hoc string-keyed lookups, and `Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs:111-117`
binding `PageEditor` directly out of `IConfiguration`) into a single
`BuildoutConfiguration.Build(args)` helper that lives in
`Buildout.Core.Configuration`. The helper returns an `IConfiguration` plus the
caller's residual args (with `--config` / `-c` stripped). Both presentations
call the same helper at the top of `Program.cs`.

The provider chain is exactly the three layers FR-002 demands, in order:
defaults on the bound options classes → JSON file (default
`~/.config/buildout/config.json` or the `--config <path>` override; default file
is `optional: true`, override file is `optional: false` so a missing override
hard-errors) → `Buildout__`-prefixed env vars.

The flat root + nested-sections schema (FR-009) binds to three options
classes registered through `services.AddOptions<T>().Bind(IConfiguration).ValidateOnStart()`:
the existing `BuildinClientOptions` (whose `BuildinClientOptionsValidator` is
extended to read `BotToken` / `BaseUrl` from the root and `HttpTimeout` /
`UnsafeAllowInsecure` from a renamed `HttpTimeout` / `UnsafeAllowInsecure` —
implementation note: we rename the *config keys* but keep the existing C# property
names on `BuildinClientOptions` to avoid a wider refactor; the binder maps
`Http:Timeout` → `BuildinClientOptions.HttpTimeout` via a small
`BuildoutOptionsBindingSource` that flattens `Http:Timeout` → `HttpTimeout`
and `Http:UnsafeAllowInsecure` → `UnsafeAllowInsecure` before the standard
binder runs); the existing `PageEditorOptions` (renamed `LimitationsOptions`
in a sibling file under the same namespace, with the existing class kept as
a `[Obsolete]` type-forwarder to surface a compile-time warning anywhere it's
still referenced outside this PR); and a new `TelemetryOptions { Enabled,
OtlpEndpoint }` covering both `Buildout.Mcp` lookups deleted from
`Program.cs:17-20`.

User docs land at `docs/configuration.md` (per FR-011) with a complete schema
reference, every key in env-var + JSON form, and the loader precedence diagram;
an executable example file lands at
`docs/configuration.example.json` (per FR-012) carrying every key at its
default value with a placeholder for the required `BotToken`.

Test discipline (constitution Principle IV, applied by FR-013/014): every
loader behaviour ships with a failing unit test first, every wire change
ships with a CLI + MCP integration test, and one new secret-scrub test grep's
the captured output of the full suite for a sentinel `BotToken` value to
satisfy SC-007.

## Technical Context

**Language/Version**: C# / .NET 10. Inherited from features 001–009. No
target-framework change.

**Primary Dependencies**: All but one are already pulled in transitively. The
exception is `Microsoft.Extensions.Configuration.Json`, which `Buildout.Core`
must add to PackageReference because the loader lives there and binds the
file directly (the CLI csproj currently lists
`Microsoft.Extensions.Configuration.EnvironmentVariables` explicitly; the
JSON provider sits behind the same family of packages and is the obvious
sibling — constitution Principle VII's "use Microsoft.Extensions.Configuration"
explicitly allows this family).

- **`Microsoft.Extensions.Configuration.{Abstractions,Binder,Json,EnvironmentVariables}`**:
  the loader composes a `ConfigurationBuilder` from these four. `Abstractions`
  and `Binder` are already in `Buildout.Core.csproj:11-12`. `Json` and
  `EnvironmentVariables` are added to `Buildout.Core.csproj` so the loader
  works without depending on the presentation project's package list. The CLI
  loses its direct `EnvironmentVariables` reference because it now flows
  through `Buildout.Core`. The MCP project keeps `Microsoft.Extensions.Hosting`
  but its `Host.CreateApplicationBuilder(args)` configuration sources are
  cleared and replaced by the prebuilt loader chain (see Phase 0 R3).
- **`Microsoft.Extensions.Options`**: already in `Buildout.Core.csproj:15`. The
  loader uses `services.AddOptions<T>().Bind(config).ValidateOnStart()` for
  the three options classes; the existing
  `BuildinClientOptionsValidator` is retained and a new
  `TelemetryOptionsValidator` is added (per FR-007, Principle VII).
- **`Spectre.Console.Cli`**: already wired in `Buildout.Cli`. A new
  `BuildoutCommandSettings` base class with `[CommandOption("-c|--config")]`
  is added under `Buildout.Cli/Commands/` and every existing `*Settings`
  class is refactored to inherit from it. Spectre's parser surfaces the
  flag through `[CommandOption]` on every command; the actual loading happens
  in `Program.cs` *before* `app.Run(args)` via the residual-args helper, so
  the per-command `Config` property is unused at runtime — it exists only
  so Spectre's help output documents the flag on each subcommand (this is
  the only viable "global option" pattern Spectre.Console.Cli supports —
  see Phase 0 R1).
- **`ModelContextProtocol` SDK**: unchanged.
- **`OpenTelemetry.*`**: unchanged. The two ad-hoc env-var lookups at
  `Buildout.Mcp/Program.cs:17-20` are deleted; the gated block now reads
  `var telemetry = builder.Services.GetRequiredService<IOptions<TelemetryOptions>>().Value`
  (after `AddBuildoutCore`-equivalent registration).

**Storage**: N/A. The loader reads from a user-scoped file
(`~/.config/buildout/config.json`) and process env vars. Both are external
configuration surfaces; the feature persists nothing.

**Testing**: xUnit v3 + NSubstitute (unit), the same WireMock-based fixture
the rest of the suite uses (integration), all inherited from prior
features.

New test categories (Phase 2 will enumerate them as tasks):

- Unit tests in `Buildout.UnitTests/Configuration/` covering
  every FR-013 sub-clause: precedence layers, `--config` override,
  `--config` missing-file hard error, validator failures producing a
  single error string, env-var prefix isolation (an unrelated `Buildout`
  prefix without trailing `__` MUST NOT bleed in).
- Unit tests for `TelemetryOptionsValidator` (range, scheme).
- Integration tests in `Buildout.IntegrationTests/Configuration/` that
  invoke each presentation's entry point with `--config <temp.json>`
  and assert bound `BuildinClientOptions` / `TelemetryOptions` /
  `LimitationsOptions` instances (FR-014).
- One secret-scrub test (`SecretLeakTests.cs`) under
  `Buildout.IntegrationTests/Configuration/` that runs both
  presentations through their full startup with a sentinel
  `BotToken=DO_NOT_LEAK_BUILDOUT_TOKEN_123` and grep's captured
  stdout/stderr/log for the literal string (SC-007).
- One docs-lint test
  (`Buildout.UnitTests/Configuration/DocumentationKeysTests.cs`) that
  parses the FR-009 key table out of `docs/configuration.md` and diffs
  it against the runtime schema discovered via reflection on the bound
  options classes (SC-005).
- The existing `ConfigurationBindingTests.cs:1-154` is migrated in-place:
  every `BuildinClientOptions` assertion stays valid but the test that
  reads from `IConfiguration` (none today — they all construct
  `BuildinClientOptions` directly) gains sibling tests that build via
  the new loader.

**Target Platform**: Same as existing — .NET 10 console processes. Loader is
single-process, single-load (no `IOptionsMonitor` hot-reload — covered in
spec Assumptions).

**Project Type**: Internal infrastructure feature touching all three source
projects (`Buildout.Core`, `Buildout.Cli`, `Buildout.Mcp`) and both test
projects. One new top-level namespace (`Buildout.Core.Configuration`); no
new csproj's.

**Performance Goals**: Loader runs once per process at startup. Target:
under 20 ms wall-clock for the assembly + bind + validate phase on a cold
cache, including reading a typical config file (<1 KB). Not a hot path, no
specific p99 target — purely "fast enough that nobody notices".

**Constraints**:

- No new NuGet dependencies outside the `Microsoft.Extensions.Configuration.*`
  family (FR-016).
- No secrets in logs (FR-015; constitution T&I Standards: Secrets).
- Loader code lives in `Buildout.Core` and is the only place that
  composes a provider chain (Principle VII).
- One process = one configuration = one workspace (spec Assumptions).
- No `IOptionsMonitor<T>` change tokens — bind once at startup (spec Assumptions).

**Scale/Scope**:

- 1 new namespace `Buildout.Core.Configuration` (≈ 5 new types).
- 1 new options class (`TelemetryOptions`) + 1 new validator
  (`TelemetryOptionsValidator`).
- 1 rename (`PageEditorOptions` → `LimitationsOptions`) with the old
  type kept as `[Obsolete]` forwarder for one release window.
- 1 new CLI base class (`BuildoutCommandSettings`) + 8 existing settings
  classes refactored to inherit from it (every `*Settings` in
  `Buildout.Cli/Commands/`).
- Modifications to both `Program.cs` files (≈ 10 lines each).
- 2 new user-facing documents (`docs/configuration.md`,
  `docs/configuration.example.json`) — net new directory if `docs/`
  doesn't exist yet (it doesn't; confirmed via `ls docs/` returning
  ENOENT during research).
- ≈ 8 new unit test files; ≈ 4 new integration test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|-----------|------------|-------|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | The loader and all options classes live in `Buildout.Core.Configuration` and `Buildout.Core.{Buildin,Markdown.Editing}`. `Buildout.Cli/Program.cs` and `Buildout.Mcp/Program.cs` shrink to: parse `--config` via the shared helper → register options via `AddBuildoutCore` / `AddBuildinClient` → run. No presentation project composes its own provider chain, parses its own env vars, or reads `IConfiguration` directly. |
| II | LLM-Friendly Output Fidelity | ✅ PASS (N/A) | Feature does not render Markdown or convert blocks. |
| III | Bidirectional Round-Trip Testing | ✅ PASS (N/A) | No block↔Markdown conversion. |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | Phase 2 tasks order red-first: loader precedence test → loader implementation; validator test → validator; docs-lint test → docs; secret-scrub test → final wiring. No tests skipped, disabled, or deleted to make the build pass. |
| V | Buildin API Abstraction | ✅ PASS | The buildin client interface and the bot/user-API split are not touched. `BuildinClientOptions` continues to be the only place authentication options live, regardless of which `IBuildinClient` implementation is in use. |
| VI | Non-Destructive Editing | ✅ PASS (N/A) | Feature does not implement block edits. The renamed `Limitations:LargeDeleteThreshold` continues to gate spec 008's destructive-edit guard exactly as before. |
| VII | Dual-Channel Configuration (NON-NEGOTIABLE) | ✅ PASS | This feature *is* the dual-channel infrastructure: every key in FR-009 ships in both `docs/configuration.md` env-var and JSON forms (verified by the SC-005 docs-lint test). The loader is `Buildout.Core.Configuration.BuildoutConfiguration.Build(args)`. Validation lives on `IValidateOptions<T>` per options class and runs at startup (FR-007). No ad-hoc sources, no custom file formats, no custom env prefixes, no legacy compatibility shims. |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | New code respects `Directory.Build.props`. |
| `ModelContextProtocol` SDK | ✅ | Unchanged. |
| `Spectre.Console.Cli` | ✅ | New `BuildoutCommandSettings` base. Existing command registration loop unchanged. |
| Solution layout (5 projects) | ✅ | No new projects. |
| Secrets from env/config | ✅ | Reinforced — `BotToken` flows only through the loader, never through a CLI arg or a tool parameter. SC-007 enforces it by test. |

**Gate result (pre-Phase 0)**: PASS — no violations. `Complexity Tracking`
table is empty.

**Re-check after Phase 1 design**: PASS — Phase 1 design preserves all
gates. `Complexity Tracking` remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/010-configuration/
├── plan.md                  # This file (/speckit-plan output)
├── spec.md                  # /speckit-specify output
├── research.md              # Phase 0 output (this command)
├── data-model.md            # Phase 1 output (this command)
├── quickstart.md            # Phase 1 output (this command)
├── contracts/               # Phase 1 output
│   ├── loader.md                # BuildoutConfiguration.Build(args) surface, residual-args contract
│   ├── schema.md                # JSON schema for config.json + env-var name table (FR-009 source of truth)
│   └── cli-config-flag.md       # --config / -c surface across CLI and MCP entry points
├── checklists/
│   └── requirements.md      # Spec quality checklist (already created)
└── tasks.md                 # Phase 2 output (/speckit-tasks — NOT this command)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    Configuration/                                            # NEW namespace
      BuildoutConfiguration.cs                                # NEW: static Build(string[] args) → (IConfiguration, string[] residualArgs)
      BuildoutConfigurationException.cs                       # NEW: thrown for hard errors (missing --config file, JSON parse error)
      BuildoutConfigurationOptions.cs                         # NEW: internal options object — default-file path, prefix, fallback envs (used by tests to inject fakes)
      ConfigFlagParser.cs                                     # NEW: extract --config <path> / -c <path> from args, return path + residual args
      HttpSectionRemapSource.cs                               # NEW: maps Http:Timeout → HttpTimeout and Http:UnsafeAllowInsecure → UnsafeAllowInsecure so the existing BuildinClientOptions property names stay stable
    Buildin/
      BuildinClientOptions.cs                                 # MODIFIED: no shape change; XML doc updated to point at docs/configuration.md
      BuildinClientOptionsValidator.cs                        # MODIFIED: error messages reference the new key names (`BaseUrl`, `Http:Timeout`, `Http:UnsafeAllowInsecure`, `BotToken`); validation rules unchanged
    Diagnostics/
      TelemetryOptions.cs                                     # NEW: { Enabled: bool, OtlpEndpoint: Uri }
      TelemetryOptionsValidator.cs                            # NEW: absolute-URI rule
    Markdown/Editing/
      PageEditorOptions.cs                                    # MODIFIED: kept as [Obsolete] forwarder pointing at LimitationsOptions; same property, same default
      LimitationsOptions.cs                                   # NEW (sibling file): { LargeDeleteThreshold: int = 10 }, [Obsolete] no
      LimitationsOptionsValidator.cs                          # NEW: ≥ 0 rule
      PageEditor.cs                                           # MODIFIED: constructor takes IOptions<LimitationsOptions> instead of IOptions<PageEditorOptions>
    DependencyInjection/
      ServiceCollectionExtensions.cs                          # MODIFIED: AddBuildinClient drops its inline GetSection("Buildin").Bind() in favour of services.AddOptions<BuildinClientOptions>().Bind(configuration).ValidateOnStart(); AddBuildoutCore replaces PageEditorOptions binding with LimitationsOptions; both methods now expect the IConfiguration from BuildoutConfiguration.Build

  Buildout.Cli/
    Commands/
      BuildoutCommandSettings.cs                              # NEW: CommandSettings base with [CommandOption("-c|--config")] string? ConfigPath
      CreateSettings.cs                                       # MODIFIED: inherits BuildoutCommandSettings
      DbSettings.cs                                           # MODIFIED: inherits BuildoutCommandSettings
      DbViewSettings.cs                                       # MODIFIED: inherits BuildoutCommandSettings
      DeleteSettings.cs                                       # MODIFIED: inherits BuildoutCommandSettings
      RestoreSettings.cs                                      # MODIFIED: inherits BuildoutCommandSettings
      UpdateSettings.cs                                       # MODIFIED: inherits BuildoutCommandSettings
      (GetCommand/SearchCommand inline settings)              # MODIFIED similarly where they declare settings types
    Program.cs                                                # MODIFIED: replace ConfigurationBuilder() block with var (config, residualArgs) = BuildoutConfiguration.Build(args); services.AddSingleton<IConfiguration>(config); … app.RunAsync(residualArgs)
    Buildout.Cli.csproj                                       # MODIFIED: remove Microsoft.Extensions.Configuration.EnvironmentVariables PackageReference (now transitive via Buildout.Core)

  Buildout.Core/Buildout.Core.csproj                          # MODIFIED: add Microsoft.Extensions.Configuration.Json + Microsoft.Extensions.Configuration.EnvironmentVariables PackageReferences (already has Abstractions, Binder, Options)

  Buildout.Mcp/
    Program.cs                                                # MODIFIED: replace lines 12-21. Now: var (config, residualArgs) = BuildoutConfiguration.Build(args); var builder = Host.CreateApplicationBuilder(residualArgs); builder.Configuration.Sources.Clear(); builder.Configuration.AddConfiguration(config); … telemetry gated by builder.Services.GetRequiredService<IOptions<TelemetryOptions>>().Value
    Buildout.Mcp.csproj                                       # UNCHANGED (Host package already pulls Json + EnvironmentVariables transitively; Buildout.Core's explicit references are belt-and-braces)

docs/                                                          # NEW directory
  configuration.md                                            # NEW (FR-011): authoritative schema reference, env+JSON examples, precedence diagram
  configuration.example.json                                  # NEW (FR-012): every key at its default value, placeholder BotToken
  README.md                                                   # NEW: one-line "see configuration.md for configuration" + table of contents stub (so docs/ has a discoverable entry point; future spec docs land alongside)

tests/
  Buildout.UnitTests/
    Configuration/                                            # NEW directory
      ConfigFlagParserTests.cs                                # NEW: --config <path>, -c <path>, repeated flag, missing-value, dashed-but-not-config-flag, residual-args invariant
      BuildoutConfigurationTests.cs                           # NEW: precedence (defaults < default-file < env), --config overrides default file, --config missing file throws BuildoutConfigurationException, validator failures surfaced as single error string, Http: prefix flattening behaves correctly
      HttpSectionRemapSourceTests.cs                          # NEW: Http:Timeout → HttpTimeout flattening, idempotency, untouched non-Http keys
      TelemetryOptionsValidatorTests.cs                       # NEW
      LimitationsOptionsValidatorTests.cs                     # NEW
      DocumentationKeysTests.cs                               # NEW: parses docs/configuration.md key table, diffs against reflection-derived schema (SC-005)
    Buildin/
      ConfigurationBindingTests.cs                            # MODIFIED: existing tests stay; new sibling tests build options via BuildoutConfiguration.Build to exercise the binding path end-to-end

  Buildout.IntegrationTests/
    Configuration/                                            # NEW directory
      CliConfigFlagTests.cs                                   # NEW: buildout-cli --config <tmp.json> resolves to expected BuildinClientOptions; missing-file path errors
      McpConfigFlagTests.cs                                   # NEW: buildout-mcp -c <tmp.json> startup loads file; OTEL fallback honoured; Buildout__-prefixed override wins
      PrecedenceMatrixTests.cs                                # NEW: [Theory] table covering every cell of (default | file | env | OTEL fallback) precedence to assert FR-002 layering
      SecretLeakTests.cs                                      # NEW (SC-007): runs both presentations through full startup with sentinel BotToken, fails if sentinel appears anywhere in captured output
```

**Structure Decision**: A new top-level `Buildout.Core.Configuration`
namespace is added alongside the existing top-level non-Markdown namespaces
(`Search/`, `DatabaseViews/`, `Diagnostics/`, `Properties/`, `PageLifecycle/`,
`Buildin/`). Configuration is shared bootstrap, not Markdown conversion,
buildin transport, or diagnostics; it warrants its own namespace and
directory by analogy with those. The CLI changes follow the existing
settings/command pattern (introducing one base class is the minimal Spectre
pattern to surface a flag everywhere; see Phase 0 R1). No new projects. No
new NuGet dependencies outside the explicitly-permitted
`Microsoft.Extensions.Configuration.{Json,EnvironmentVariables}` (FR-016).

## Phase 0: Research (output: research.md)

Items unknown at the start of `/speckit-plan` and resolved in `research.md`:

- **R1 — Spectre.Console.Cli "global option" pattern for `--config`**:
  Spectre.Console.Cli does not support a true global option that is parsed
  before commands run. The supported pattern is to declare the option on a
  base `CommandSettings` class that every command's settings inherits from;
  Spectre then renders the flag in every command's help output and binds
  it onto each command's `Settings` instance. Decision: introduce
  `BuildoutCommandSettings : CommandSettings` with
  `[CommandOption("-c|--config")] string? ConfigPath` and have every
  existing `*Settings` class inherit from it. The runtime cost is
  zero because the *parsing* of `--config` for loader purposes happens in
  `Program.cs` BEFORE `app.Run(args)` — the per-command property is purely
  for help-text documentation. This avoids forking Spectre's parser or
  hand-rolling a separate pre-pass that drifts from the documented surface.
  Alternative considered: split-binding via Spectre's
  `IRemainingArguments` — rejected because the flag would not show up in
  per-command help, violating the spec's discoverability intent.

- **R2 — Where to parse `--config` so the loader can act on it before any
  argument parser sees the residual args**: the helper
  `Buildout.Core.Configuration.ConfigFlagParser.Extract(string[] args)`
  performs a single linear scan, recognises `--config <value>`,
  `--config=<value>`, `-c <value>`, and `-c=<value>`, removes both tokens
  from the array, and returns `(string? configPath, string[] residual)`.
  Decision: implement this helper in `Buildout.Core` so both presentations
  use the same parser. The CLI then passes `residual` to `app.RunAsync`;
  the MCP project passes `residual` to `Host.CreateApplicationBuilder`.
  Spectre's per-command settings still re-parse the flag, but that
  re-parse is a no-op for runtime behaviour and serves only to keep
  Spectre's help text honest.
  Alternative considered: `Environment.GetCommandLineArgs` + custom
  state machine — rejected as overkill; the flag's grammar is tiny.

- **R3 — Composing `BuildoutConfiguration.Build(args)` with
  `Host.CreateApplicationBuilder(args)` in `Buildout.Mcp`**: the host
  builder's `IConfigurationManager` is mutable. The plan is to call
  `builder.Configuration.Sources.Clear()` immediately after construction,
  then `builder.Configuration.AddConfiguration(prebuilt)` to splice the
  loader's `IConfiguration` in as the sole configuration source for the
  host. This preserves DI behaviour (services depending on
  `IConfiguration` see the unified chain), and it discards the host's
  default `appsettings.json` lookup which the buildout project does not
  use (no `appsettings.json` file exists; verified by repo search).
  Decision: this approach over the alternative of feeding the host builder
  through `ConfigureAppConfiguration(b => b.Sources.Clear()...)` because
  the explicit two-line clear+add is easier to read in `Program.cs` and
  symmetric to what the CLI does.
  Alternative considered: implement a custom `IConfigurationSource` that
  delegates to the prebuilt `IConfiguration` — rejected: same outcome,
  more code.

- **R4 — `BuildinClientOptions` property names vs. new config keys**: the
  spec keys `Http:Timeout` and `Http:UnsafeAllowInsecure` (FR-009) do not
  directly bind to the existing `BuildinClientOptions.HttpTimeout` and
  `UnsafeAllowInsecure` properties (no `Http` sub-object on the C# type).
  Two implementation options:
  (a) introduce a nested `BuildinHttpOptions { Timeout, UnsafeAllowInsecure }`
      property on `BuildinClientOptions` and migrate all callers
      (`BotBuildinClient`, `ServiceCollectionExtensions`, every existing
      `ConfigurationBindingTests` assertion) to read from
      `.Http.Timeout` / `.Http.UnsafeAllowInsecure`. Wider blast radius.
  (b) keep the existing flat property layout on `BuildinClientOptions` and
      add a small `HttpSectionRemapSource : IConfigurationSource` to
      `Buildout.Core.Configuration` that, after the rest of the chain is
      assembled, projects `Http:Timeout` → `HttpTimeout` and
      `Http:UnsafeAllowInsecure` → `UnsafeAllowInsecure` so the standard
      binder works unchanged. Smaller blast radius; the C# property names
      diverge from the config keys but the public surface (env vars + JSON)
      matches the spec exactly.

  Decision: **(b)**. The configurable surface is what users see; internal
  property names are an implementation detail. (a) would churn five files
  outside this feature's scope (constitution Scope Discipline). The
  remap source is ~30 lines, has its own unit tests, and is the only
  place that knows about the rename. If a future feature genuinely needs
  to model HTTP options as a nested struct, (a) becomes a localised
  refactor at that point.

- **R6 — `Microsoft.Extensions.Configuration.EnvironmentVariables` prefix
  semantics**: passing `prefix: "Buildout__"` to `.AddEnvironmentVariables(prefix)`
  strips the prefix AND treats `__` as the section separator on what
  remains (this is the
  `Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationProvider`
  default behaviour). So `Buildout__Http__Timeout=00:01:00` becomes config
  key `Http:Timeout` — exactly what `HttpSectionRemapSource` then projects
  to `HttpTimeout`. Decision: rely on the stock provider; no custom env
  parsing needed. Verified by reading the `EnvironmentVariablesConfigurationProvider`
  source comments and by an explicit unit test
  (`BuildoutConfigurationTests.EnvVar_DoubleUnderscoreIsSectionSeparator`).
  Alternative considered: parsing env vars by hand — rejected as
  needless duplication and a constitution Principle VII violation
  ("use Microsoft.Extensions.Configuration").

- **R8 — docs-lint test design (SC-005)**: the test parses
  `docs/configuration.md` looking for the FR-009 markdown table (matched
  by the literal column header `| Key | Type | Default | Required |
  Validation |`), extracts the first column of each subsequent row, and
  diffs the resulting set against a canonical set computed at runtime
  by reflecting on the three options classes and their nested members.
  Decision: keep this test in `Buildout.UnitTests` (no DI, no IO beyond
  the doc file) and have it locate `docs/configuration.md` via
  `AppContext.BaseDirectory` + walk-up-to-repo-root (same trick the
  WireMock fixture uses to find its fixture files). Failure mode: a
  single string-diff failure showing "extra in docs" / "missing in docs"
  sets. Alternative considered: a Roslyn analyser — rejected, vastly
  more complex for the same outcome.

- **R9 — Secret-scrub test design (SC-007)**: the test sets
  `Buildout__BotToken=DO_NOT_LEAK_BUILDOUT_TOKEN_123` as a process env
  variable, captures stdout/stderr/logger output through standard
  redirection during a full presentation startup (the existing
  `BuildinWireMockFixture` already redirects), and asserts that the
  sentinel literal does not appear in any captured stream. Decision:
  place this in `Buildout.IntegrationTests/Configuration/` so it
  exercises the real loader, real DI, and the real
  `BuildinClientOptionsValidator` path (the validator's failure
  message currently says `BuildinClientOptions.BotToken is required.`
  which contains no value — must remain so).

- **R10 — `appsettings.json` discovery in MCP**: the existing
  `Host.CreateApplicationBuilder(args)` call discovers
  `appsettings.json` from the current working directory by default.
  No such file exists in this repo today (`find . -name appsettings.json`
  returns nothing). After `Sources.Clear()` the host no longer looks
  for one, removing a confusing future-failure mode where a stray
  `appsettings.json` in `pwd` would inject untracked configuration.
  Decision: document this in `docs/configuration.md` "Common pitfalls"
  so operators understand the loader is intentionally narrow.

## Phase 1: Design & Contracts

### data-model.md

Captures the bound-options shapes and the loader's residual-args contract.
See `data-model.md`. Key shapes:

- `BuildinClientOptions` (modified — see R4; XML doc adds links to
  `docs/configuration.md`; properties unchanged).
- `TelemetryOptions { Enabled: bool = false; OtlpEndpoint: Uri =
  new("http://localhost:4318") }` (new).
- `LimitationsOptions { LargeDeleteThreshold: int = 10 }` (renamed from
  `PageEditorOptions`; `[Obsolete]` forwarder retained for one release
  window).
- `BuildoutConfigurationException : Exception` (new) — thrown for
  `--config` missing file, JSON parse error, validator failures
  bubbling out of `ValidateOnStart`.
- Loader return type:
  `(IConfiguration Configuration, string[] ResidualArgs)` — a tuple,
  not a wrapper struct (idiomatic for the single call site).

### contracts/

Four contract documents:

- `loader.md` — `BuildoutConfiguration.Build(args)` signature, side
  effects (logger acquisition, file IO), error taxonomy
  (`BuildoutConfigurationException` cases), the residual-args contract
  (every element of input `args` not part of a `--config` / `-c` flag
  preserves both value and position).
- `schema.md` — authoritative key table reproduced verbatim from
  FR-009 with types, defaults, validation rules, env-var form, JSON
  form. This file is what the docs-lint test diffs against. Any change
  to the schema requires updating both this file AND
  `docs/configuration.md` AND the options-class defaults in source.
- `cli-config-flag.md` — surface contract for the `--config` / `-c`
  flag on `buildout-cli` and `buildout-mcp`: accepted forms,
  precedence vs. the default file, behaviour when the file does not
  exist (hard error), behaviour when supplied multiple times
  (last wins — Microsoft.Extensions.Configuration semantics).

### quickstart.md

Three scenarios mirroring User Stories 1, 2, 4:

1. **First-run with env var**: install `buildout-cli`, export
   `Buildout__BotToken=<token>`, run `buildout-cli search foo`,
   confirm success.
2. **Per-environment file**: copy `docs/configuration.example.json`
   to two paths (`dev.json`, `prod.json`), edit `BotToken` in each,
   show `buildout-cli --config dev.json search …` vs.
   `buildout-cli --config prod.json search …` against different
   workspaces.
3. **MCP launcher integration**: show a Claude-Code-style MCP server
   command-line entry: `buildout-mcp -c /etc/buildout/prod.json`,
   confirm the launcher does not need to set env vars on the spawned
   process.

### Agent context update

`CLAUDE.md` (project root) currently references
`specs/009-delete-restore-page/plan.md` between the
`<!-- SPECKIT START -->` and `<!-- SPECKIT END -->` markers. Phase 1
updates that link to `specs/010-configuration/plan.md`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
