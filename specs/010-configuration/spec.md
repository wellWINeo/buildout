# Feature Specification: Unified Configuration

**Feature Branch**: `010-configuration`
**Created**: 2026-05-21
**Status**: Draft
**Input**: User description: "Configuration feature. Options could be provided in 2
ways: JSON-file or environment variables. E.g. env var `Buildout__BotToken`, or
JSON file `{ "BotToken": "<token>" }`. By default try to find config file in
`~/.config/buildout/config.json`. Config file can also be provided via CLI
argument: `buildout-{cli|mcp} --config my-config.json` / `-c my-config.json`.
Discover other options which should be incorporated into configuration. Feature
must include user's documentation on configuration (both env vars & json) under
`docs/`. For configuration use standard Microsoft.Extensions.Configuration."

## Context

Today, configuration is fragmented across the two presentation projects:

- `Buildout.Cli` builds a `ConfigurationBuilder()` with only
  `.AddEnvironmentVariables()` (no prefix) and binds the `Buildin` section into
  `BuildinClientOptions`.
- `Buildout.Mcp` uses `Host.CreateApplicationBuilder(args)` (which loads
  `appsettings.json`, env vars, and CLI args), binds the same `Buildin` section,
  and additionally reads two unprefixed environment keys
  (`BUILDOUT_TELEMETRY_ENABLED`, `OTEL_EXPORTER_OTLP_ENDPOINT`) for
  observability.
- `Buildout.Core` separately binds `PageEditor:LargeDeleteThreshold`.

There is no shared configuration entry point, no documented configuration
schema, no documented file location, no documented env var prefix, no
`--config` CLI flag, and no user-facing configuration documentation.

This feature unifies configuration loading across both presentation surfaces
behind a single, documented schema, file location, env var prefix, and CLI
override mechanism, all built on
`Microsoft.Extensions.Configuration`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — First-time user configures the bot token (Priority: P1)

As a developer who has just installed `buildout-cli` (or `buildout-mcp`), I
need to point the tool at my buildin workspace by providing exactly one
secret (the bot token) without writing any code or learning a configuration
section layout. I either `export Buildout__BotToken=<token>` and rerun any
command, or I create `~/.config/buildout/config.json` containing
`{ "BotToken": "<token>" }`, and the tool just works.

**Why this priority**: This is the only mandatory setting in the entire
configuration surface — without it nothing else works. Both env var and JSON
file paths are equally first-class; a user picking one MUST NOT need to know
the other exists. This is also the on-ramp for every other user story.

**Independent Test**: Start either presentation project with no JSON file,
no CLI args, and only `Buildout__BotToken=test-token-123` exported. Assert
that the bound `BuildinClientOptions.BotToken` equals `test-token-123` and
that validation passes. Repeat with the env var unset and a JSON file at
`~/.config/buildout/config.json` containing `{ "BotToken": "test-token-123" }`
— assert the same bound value.

**Acceptance Scenarios**:

1. **Given** no JSON config file exists and `Buildout__BotToken=abc` is
   exported, **When** the user runs `buildout-cli search foo`, **Then** the
   CLI proceeds (no configuration error) and the buildin client is
   constructed with `BotToken=abc`.
2. **Given** no env vars set and a file at `~/.config/buildout/config.json`
   containing `{ "BotToken": "abc" }`, **When** the user runs `buildout-cli
   search foo`, **Then** the CLI proceeds and the buildin client is
   constructed with `BotToken=abc`.
3. **Given** neither env var nor file is present, **When** the user runs any
   command on either presentation, **Then** the process exits with a
   non-zero status and a single human-readable error message that names the
   missing key (`BotToken`), the two ways to supply it (env var
   `Buildout__BotToken`, JSON file with key `BotToken`), and the default
   file location (`~/.config/buildout/config.json`).

---

### User Story 2 — Operator overrides config file per invocation (Priority: P1)

As an operator running `buildout-cli` or `buildout-mcp` in multiple
environments (local, staging, production), I keep separate JSON files per
environment and select which one to load on the command line: `buildout-cli
--config /etc/buildout/prod.json search foo` or `buildout-mcp -c
/etc/buildout/prod.json`. The CLI-supplied path completely replaces the
default `~/.config/buildout/config.json` lookup; env vars supplied at the
same time still override the file's values.

**Why this priority**: Without per-invocation file selection, multi-env
operation requires symlink hacks, `cp` before each run, or wrapper scripts.
Both CLI and MCP must support this identically — an MCP server launched by
a Claude-Code-like client needs to be told which workspace's config to use
without depending on the launcher's process environment.

**Independent Test**: Invoke either presentation with `--config
./test-config.json` pointing at a JSON file containing
`{ "BotToken": "from-cli-arg" }` while a different
`~/.config/buildout/config.json` also exists. Assert the bound `BotToken`
equals `from-cli-arg`, not the default file's value.

**Acceptance Scenarios**:

1. **Given** `~/.config/buildout/config.json` contains
   `{ "BotToken": "default" }` and `./other.json` contains
   `{ "BotToken": "override" }`, **When** the user runs
   `buildout-cli --config ./other.json search foo`, **Then** the buildin
   client is constructed with `BotToken=override` and the default file is
   not read.
2. **Given** the same setup as scenario 1 with `-c ./other.json` (short
   form) instead of `--config`, **When** the user runs the equivalent
   command, **Then** the behaviour is identical to scenario 1.
3. **Given** `./other.json` does not exist, **When** the user runs
   `buildout-cli --config ./other.json search foo`, **Then** the process
   exits with a non-zero status and a human-readable error message naming
   the missing file path. The default file is **not** silently consulted
   as a fallback.
4. **Given** `--config ./other.json` is supplied AND
   `Buildout__BotToken=env-wins` is exported, **When** the user runs
   `buildout-cli --config ./other.json search foo`, **Then** the env var
   value is applied on top of the file's value and `BotToken=env-wins`.
5. **Given** `buildout-mcp -c ./mcp-prod.json` is invoked by an MCP client
   (no terminal, stdio transport), **When** the server starts, **Then** it
   loads `./mcp-prod.json` and ignores `~/.config/buildout/config.json`.

---

### User Story 3 — Layered overrides for an existing developer (Priority: P2)

As a developer who already has a working `~/.config/buildout/config.json`
with my normal bot token and tuned settings, I want to temporarily override
one or two values for a single command (e.g., point at a staging buildin
endpoint, raise the HTTP timeout, enable telemetry) without editing the
file. I do this by exporting a single env var (`Buildout__BaseUrl=…`,
`Buildout__Http__Timeout=00:01:00`, `Buildout__Telemetry__Enabled=true`)
and rerunning the command. Unset env vars do not blank out file values.

**Why this priority**: This is the standard
`Microsoft.Extensions.Configuration` precedence contract (file < env < CLI
args). Without it, the configuration surface is not composable and every
deviation requires a full second config file.

**Independent Test**: With `~/.config/buildout/config.json` containing
`{ "BotToken": "tok", "BaseUrl": "https://api.buildin.ai/", "Http": {
"Timeout": "00:00:30" } }` and `Buildout__Http__Timeout=00:01:00` exported,
assert that bound options have `BotToken=tok`,
`BaseUrl=https://api.buildin.ai/`, and `Http.Timeout=00:01:00`.

**Acceptance Scenarios**:

1. **Given** the file sets `BotToken`, `BaseUrl`, `Http.Timeout`, and
   `Http.UnsafeAllowInsecure` and `Buildout__Http__Timeout=00:01:00` is
   exported, **When** any command runs, **Then** the timeout is one minute
   (env wins for that nested key) and the other three keys come from the
   file (env does not blank them, and the rest of the `Http` section is
   not erased by setting one of its members).
2. **Given** the file sets `BotToken` only and env sets
   `Buildout__BaseUrl=https://staging.example.com/`, **When** any command
   runs, **Then** the bound options carry both values without error.
3. **Given** env sets `Buildout__Telemetry__Enabled=true` and
   `Buildout__Telemetry__OtlpEndpoint=https://otel:4318` and no file is
   present, **When** `buildout-mcp` starts, **Then** OpenTelemetry export
   is enabled with that endpoint, replacing today's
   `BUILDOUT_TELEMETRY_ENABLED` / `OTEL_EXPORTER_OTLP_ENDPOINT`
   environment keys.

---

### User Story 4 — User-facing configuration documentation (Priority: P2)

As any user (developer, operator, LLM agent reading the repo), I open
`docs/configuration.md` and find a single page that lists every supported
configuration key with its type, default value, validation rules, and the
two ways to set it (env var name and JSON path). Each key has at least one
worked example for each delivery channel.

**Why this priority**: Without this document, every user has to read the
spec or the source to learn what `Buildout__Http__Timeout` accepts. The user
explicitly asked for this document in the feature description, and the
feature is not done until it exists.

**Independent Test**: Open `docs/configuration.md`. Confirm it has a
section for each configuration key listed in the FR table below, and that
each section shows both the env var form and the JSON form, plus the
default value (if any) and the validation rule (if any). Confirm the
document is reachable from the repo root via a relative link from at least
one of `README.md`, `docs/index.md`, or `docs/README.md` (whichever
already exists).

**Acceptance Scenarios**:

1. **Given** the document exists, **When** a reader searches for
   `BotToken`, **Then** they find a section that says it is required,
   shows `export Buildout__BotToken=<value>` and the equivalent JSON
   snippet, and explains that the JSON file path defaults to
   `~/.config/buildout/config.json` and can be overridden with `--config`
   / `-c`.
2. **Given** the document exists, **When** a reader needs to enable
   OpenTelemetry export against a non-default OTLP endpoint, **Then** they
   find a section documenting `Telemetry:Enabled` and
   `Telemetry:OtlpEndpoint` with both env var and JSON examples and the
   default endpoint value (`http://localhost:4318`).

---

### User Story 5 — Misconfiguration surfaces a clear error (Priority: P3)

As a user who has just made a typo in their JSON file (`{ "BotTokn":
"abc" }`) or mis-set an env var (`Buildout__Http__Timeout=abc`), I expect
the process to exit fast with a single error message that points at the
offending key and the expected shape — not a stack trace, not a binder
exception, not a silent fallback.

**Why this priority**: Configuration errors are the first thing every new
user hits. They must be diagnosable from the error text alone. This is a
quality-of-life story rather than a capability story, hence P3.

**Independent Test**: Construct a JSON file with one valid key (`BotToken`)
and one obviously invalid value (`Http.Timeout: "not-a-duration"`), run the
process, and assert the exit code is non-zero AND the stderr/console output
contains both the offending key name and a one-line description of the
expected format.

**Acceptance Scenarios**:

1. **Given** `~/.config/buildout/config.json` contains valid JSON but a
   value the binder cannot parse (e.g., `Http: { "Timeout": "abc" }`),
   **When** any command runs, **Then** the process exits non-zero with an
   error message that names the offending key (`Http:Timeout`) and the
   expected format (e.g., `HH:MM:SS` or .NET `TimeSpan`).
2. **Given** `~/.config/buildout/config.json` is not valid JSON, **When**
   any command runs, **Then** the process exits non-zero with an error
   message that names the file path and reports a parse error (with line
   and column if available); the env var fallback is not silently used.
3. **Given** the `BotToken` is missing entirely (no env, no file, or both
   present but neither sets it), **When** any command runs, **Then** the
   existing `BuildinClientOptionsValidator` failure message is surfaced
   verbatim with no stack trace.

---

### Edge Cases

- **File exists but is empty (`{}`)**: treated as if no config file were
  present. Loading proceeds; validation runs against defaults + env vars
  alone. Not an error unless required keys remain unset.
- **File exists but is not readable (permissions)**: error with the file
  path. Do not silently fall back to env-only.
- **CLI flag `--config` repeated**: behaviour follows whatever
  `Microsoft.Extensions.Configuration` + the argument parser already
  produce for repeated flags — feature does not introduce custom merge
  semantics for multiple `--config` flags. Documented as "supply one".
- **CLI flag `-c` with no value**: argument parser surfaces its standard
  missing-value error; this feature does not catch it specially.
- **Default file path expansion (`~/`)**: the path is resolved to the
  current OS user's home directory via the standard .NET mechanism (i.e.
  `Environment.GetFolderPath(SpecialFolder.UserProfile)` or equivalent),
  joined with `.config/buildout/config.json`. No shell expansion is
  required.
- **XDG_CONFIG_HOME**: out of scope for this feature. The default path is
  fixed at `~/.config/buildout/config.json` regardless of XDG. Users who
  need an XDG-style override use `--config` or the env var prefix.
- **Windows / macOS path differences**: the path becomes
  `%USERPROFILE%\.config\buildout\config.json` on Windows and the same
  `~/.config/buildout/config.json` on macOS; no platform-specific roaming
  or AppData rewriting. This feature does not branch on OS.
- **Process running as a system service (no `$HOME`)**: if the user
  profile directory cannot be resolved, default-file discovery is silently
  skipped (no error); the user is expected to supply `--config` or env
  vars. Surfaced to users in the documentation.
- **`OTEL_EXPORTER_OTLP_ENDPOINT` (existing, vendor-neutral standard env
  var)**: continues to be honoured by `Buildout.Mcp` for backwards
  compatibility with industry tooling, but is no longer the documented
  configuration channel. The documented channel is
  `Buildout__Telemetry__OtlpEndpoint` / JSON `Telemetry.OtlpEndpoint`.
  The two are read in that order (Buildout-prefixed wins).
- **`BUILDOUT_TELEMETRY_ENABLED` (existing)**: removed and replaced by
  `Buildout__Telemetry__Enabled`. This is the only intentional breaking
  change introduced by the feature; documented under "Migration" in
  `docs/configuration.md`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Both `Buildout.Cli` and `Buildout.Mcp` MUST load
  configuration through the same code path. That code path MUST be
  implemented in `Buildout.Core` (constitution Principle I —
  presentation projects translate transport concerns and nothing
  else; configuration is not a transport concern but it is shared
  domain bootstrap and belongs in `Buildout.Core`).
- **FR-002**: The configuration provider chain MUST be built on
  `Microsoft.Extensions.Configuration` and MUST be assembled in this
  order (lower-precedence first, higher-precedence last):
  1. In-code defaults (the property defaults already on
     `BuildinClientOptions`, `PageEditorOptions`, and the new
     `TelemetryOptions`).
  2. JSON file at `~/.config/buildout/config.json` IF it exists AND no
     `--config` flag was supplied.
  3. JSON file at the path supplied by `--config` / `-c` IF the flag was
     supplied (this REPLACES, not supplements, the default file).
  4. Environment variables with prefix `Buildout__`.
- **FR-003**: The environment variable prefix MUST be `Buildout__` (with
  a trailing double underscore as a section separator). Nested settings
  use a second `__` between segments (e.g.,
  `Buildout__Telemetry__OtlpEndpoint`). The prefix MUST NOT collide with
  unrelated `Buildout`-prefixed variables (none currently exist; this is
  enforced going forward by documentation, not by code).
- **FR-004**: The JSON configuration root MUST NOT include a top-level
  `Buildin` section. Buildin-client identity (`BotToken`, `BaseUrl`)
  lives flat at the root; HTTP transport tuning lives under an
  `Http` sub-object; observability lives under `Telemetry`; page-edit
  safety thresholds live under `Limitations`. Env vars mirror this nesting via
  the `__` section separator (e.g., `Buildout__Http__Timeout`). The
  supported keys are exactly the ones enumerated in FR-009 below.
- **FR-005**: Both `buildout-cli` and `buildout-mcp` MUST accept
  `--config <path>` and `-c <path>` as a global option. The CLI uses
  `Spectre.Console.Cli`'s global option mechanism (constitution
  Technology & Implementation Standards mandates this framework). The
  MCP project uses `Microsoft.Extensions.Hosting`'s `args`
  pre-processing.
- **FR-006**: The default file path MUST be
  `<UserHome>/.config/buildout/config.json`, where `<UserHome>` is the
  current user's home directory as reported by the OS. The path is
  computed once at startup. Missing default file: silently skipped.
  Missing `--config` file: hard error with the file path in the
  message.
- **FR-007**: Configuration binding MUST happen exactly once per process,
  during DI registration. The bound `BuildinClientOptions` instance is
  validated by the existing `BuildinClientOptionsValidator` (no new
  validator is added for it). New options classes
  (`TelemetryOptions`, others added in FR-009) MUST each have an
  `IValidateOptions<T>` implementation that fails fast on startup for
  unparseable / out-of-range values.
- **FR-008**: Configuration loading MUST NOT silently swallow file I/O
  errors. File-not-found at the default path is the only silent skip;
  all other I/O errors (permission denied, invalid JSON, unparseable
  values) surface as a single human-readable error message and a
  non-zero exit code.
- **FR-009**: The configuration schema MUST consist of exactly the
  following keys, all flat at the root of the JSON object and
  corresponding env vars prefixed with `Buildout__`:

  | Key | Type | Default | Required | Validation | Replaces |
  |-----|------|---------|----------|------------|----------|
  | `BotToken` | string | — | **yes** | non-empty | `Buildin:BotToken` |
  | `BaseUrl` | URI string | `https://api.buildin.ai/` | no | absolute URI; must be `https` unless `Http:UnsafeAllowInsecure=true` | `Buildin:BaseUrl` |
  | `Http:Timeout` | `TimeSpan` | `00:00:30` | no | > 0 | `Buildin:HttpTimeout` |
  | `Http:UnsafeAllowInsecure` | bool | `false` | no | — | `Buildin:UnsafeAllowInsecure` |
  | `Limitations:LargeDeleteThreshold` | int | `10` | no | ≥ 0 | `PageEditor:LargeDeleteThreshold` |
  | `Telemetry:Enabled` | bool | `false` | no | — | `BUILDOUT_TELEMETRY_ENABLED` |
  | `Telemetry:OtlpEndpoint` | URI string | `http://localhost:4318` | no | absolute URI | `OTEL_EXPORTER_OTLP_ENDPOINT` (Buildout-prefixed key wins; legacy env var honoured as fallback only) |

  Per-section grouping: `Http:*` covers HTTP transport tuning (timeout,
  insecure-scheme opt-out) for the buildin client. `Telemetry:*` covers
  observability export. `Limitations:*` covers user-configurable safety
  thresholds (the kinds of guard rails the constitution's
  non-destructive-editing principle motivates — currently the
  large-delete threshold, with room for future additions without a
  section rename). `BotToken` and `BaseUrl` remain flat at the root
  because they identify the workspace and endpoint, not a
  transport-tuning or feature-area concern.

  No other keys are accepted. Unknown keys are logged at startup as a
  single warning (not an error) and otherwise ignored. The exact warning
  format is a `/speckit-plan` decision.

- **FR-010**: Migration handling — the existing `BUILDOUT_TELEMETRY_ENABLED`
  env var is **removed** from the recognised set. The existing
  `OTEL_EXPORTER_OTLP_ENDPOINT` env var continues to be read as a
  lower-precedence fallback below `Buildout__Telemetry__OtlpEndpoint` and
  the JSON value, for compatibility with vendor-neutral OpenTelemetry
  tooling. The existing `Buildin:*` keys are **removed**; users must
  rename them to the flat / `Http:*` keys above. The existing
  `PageEditor:LargeDeleteThreshold` key is **renamed** to
  `Limitations:LargeDeleteThreshold` (same type, default, validation,
  and bound value — only the section name changes). The removal /
  rename is documented in `docs/configuration.md` under "Migration from
  earlier versions" with a one-to-one rename table.
- **FR-011**: The feature MUST ship `docs/configuration.md` containing,
  at minimum: (a) a one-paragraph summary of how configuration is
  loaded and the precedence order; (b) the supported file location and
  the CLI override flag; (c) a complete reference of every key in
  FR-009 with type, default, validation, env var form, JSON form, and
  one worked example per channel; (d) a "Migration from earlier
  versions" section with the rename table from FR-010; (e) a "Common
  pitfalls" section covering at least the edge cases enumerated in this
  spec (missing file vs. explicit `--config`, env var case sensitivity,
  double-underscore separator, `TimeSpan` format).
- **FR-012**: A reference example file MUST also ship at
  `docs/configuration.example.json` containing every key from FR-009
  set to its default value (with a placeholder for `BotToken`), valid
  as JSON, and committed to the repo. Users can copy it to
  `~/.config/buildout/config.json` and edit.
- **FR-013**: The configuration loader MUST be exercised by unit tests
  in `Buildout.UnitTests` covering at least: (a) precedence
  (defaults < file < env), (b) `--config` overriding the default file,
  (c) `--config` to a missing file failing hard, (d) validator failures
  surfacing as a single error string, (e) the `Buildout__` env prefix
  being applied and not bleeding from unrelated env vars, (f) unknown
  keys logged but ignored. The constitution's Test-First Discipline
  (Principle IV) applies — these tests precede the implementation.
- **FR-014**: An integration test in `Buildout.IntegrationTests` MUST
  exercise the CLI's `--config` flag end-to-end with a temp-file JSON
  config and assert the resulting `BuildinClientOptions` instance.
  An equivalent test MUST exercise `buildout-mcp -c <path>` through
  its process entry point.
- **FR-015**: No secrets MAY be logged or echoed by the configuration
  loader. Specifically, `BotToken` MUST NOT appear in any startup log
  line, error message, validation failure message, or warning about
  unknown keys. The constitution's Technology & Implementation
  Standards "Secrets" rule and the global AGENTS.md secrets directive
  both apply.
- **FR-016**: The feature MUST NOT introduce a new dependency. All
  required behaviour is achievable with the existing
  `Microsoft.Extensions.Configuration` family of packages already
  pulled in transitively by `Microsoft.Extensions.Hosting` (used by
  `Buildout.Mcp`) and `Microsoft.Extensions.DependencyInjection` (used
  by `Buildout.Cli`). If the CLI project needs to add the JSON config
  provider package explicitly (`Microsoft.Extensions.Configuration.Json`)
  or the env-var provider (`Microsoft.Extensions.Configuration.EnvironmentVariables`),
  that is not considered a "new" dependency — it is part of the
  mandated configuration framework.

### Key Entities

- **Configuration File**: a JSON document at a discoverable path
  (default `~/.config/buildout/config.json` or the path supplied via
  `--config`/`-c`) whose root is a flat object of FR-009 keys. The
  file's only purpose is to carry user-scoped persistent settings; it
  has no schema versioning field in v1.
- **Environment Variable Set**: any process-environment entries with
  prefix `Buildout__`; the suffix after the prefix is treated as a
  configuration key with `__` as the section separator.
- **CLI Override Flag**: a global option `--config <path>` (alias `-c`)
  on both `buildout-cli` and `buildout-mcp` that replaces the default
  file path during loader assembly.
- **Bound Options Objects**: the existing `BuildinClientOptions`, the
  existing `PageEditorOptions`, and a new `TelemetryOptions` (with
  `Enabled: bool`, `OtlpEndpoint: Uri`). All three are registered as
  validated singletons during DI bootstrap.
- **Loader Result**: a single `IConfiguration` instance shared between
  presentation and core, plus the three bound option objects. There is
  no other public API of this feature — call sites that need a value
  request the bound options class via DI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new developer can run `buildout-cli search foo`
  successfully after performing exactly one of the following: (a)
  exporting a single environment variable, (b) creating a single
  one-line JSON file. No other configuration step is required, and the
  end-to-end time from clone to first successful command is under
  three minutes for someone who already has a buildin bot token —
  verifiable by following `docs/configuration.md` step by step.
- **SC-002**: The same JSON config file works unchanged for both
  `buildout-cli` and `buildout-mcp` — verifiable by an integration
  test that points both presentations at the same temp file and
  asserts identical bound options.
- **SC-003**: Switching to a different config file requires changing
  exactly one CLI argument and zero environment variables — verifiable
  by the User Story 2 integration test.
- **SC-004**: A misconfigured installation surfaces the cause in the
  first error message of the failing process; the user does not need
  to read a stack trace or enable debug logging — verifiable by tests
  asserting that the error text contains the offending key name and
  the expected format.
- **SC-005**: `docs/configuration.md` lists every configuration key
  recognised by the loader, with no extras and no missing keys —
  verifiable by a documentation lint test that diffs the key set in
  the doc against the schema defined in FR-009.
- **SC-006**: Removing the `BUILDOUT_TELEMETRY_ENABLED` env var and
  the `Buildin:*` keys breaks no tests outside this feature's own
  migration tests — verifiable by the existing `Buildout.UnitTests`
  and `Buildout.IntegrationTests` suites passing after the migration.
- **SC-007**: No log line, error, or warning emitted by any process
  in any of the unit/integration test suites contains the literal
  string value of a configured `BotToken` — verifiable by tests that
  set `BotToken` to a known sentinel and grep all captured output for
  it.

## Assumptions

- The `Buildin:*` configuration section name and the
  `BUILDOUT_TELEMETRY_ENABLED` env var are not yet relied on by external
  users; they are internal-only configuration of pre-1.0 specs (001 -
  009). This feature replaces them with the unified schema in a single
  step (no overlap window) — only documented as a rename table.
- `OTEL_EXPORTER_OTLP_ENDPOINT` is intentionally kept as a low-precedence
  fallback because it is an OpenTelemetry-standard env var that
  downstream tooling (collectors, sidecars) may already set; removing it
  would surprise operators using vendor-neutral OTel deployment
  patterns.
- The home directory is resolvable in all targeted execution
  environments (developer workstations, CI runners, MCP launchers
  inheriting the user's environment). The "no home directory"
  fallback is a documented edge case, not a primary scenario.
- Users supplying configuration are humans editing files and exporting
  env vars; no programmatic / hot-reload reconfiguration is in scope.
  `IOptionsMonitor<T>` change tokens are out of scope — configuration
  is bound once at startup.
- The `--config` flag value is a regular filesystem path; URL-loaded
  configuration (HTTP, S3, vault) is explicitly out of scope. A future
  feature can layer a remote provider on top of the unified loader if
  needed.
- Multi-tenant or per-workspace configuration (one `buildout-cli`
  process targeting two buildin workspaces in one invocation) is out
  of scope. One process = one configuration = one workspace.
- The `Spectre.Console.Cli` framework supports global options that
  are parsed before per-command binding (this is the standard pattern
  via `IConfigurator.SetApplicationName` + a typed `CommandSettings`
  base with a `[CommandOption]` for `--config`). If this assumption
  proves false during planning, the `/speckit-plan` phase will pick
  an alternative wiring inside the same constitutional constraint
  (Spectre.Console.Cli is mandatory; the wiring details are not).
- YAML and TOML formats are intentionally out of scope. JSON only.
