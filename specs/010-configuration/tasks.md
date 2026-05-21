# Tasks: Unified Configuration

**Input**: Design documents from `/specs/010-configuration/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them (RED first, then implementation, then GREEN).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel with other [P] tasks in the same phase (different files, no incomplete dependencies)
- **[USn]**: Which user story this task belongs to (US1–US5); omitted for Setup, Foundational, and Polish phases

---

## Phase 1: Setup

**Purpose**: NuGet package updates required before any new source files compile.

- [ ] T001 Update `src/Buildout.Core/Buildout.Core.csproj` — add `Microsoft.Extensions.Configuration.Json` and `Microsoft.Extensions.Configuration.EnvironmentVariables` PackageReferences alongside the existing Abstractions and Binder references
- [ ] T002 Update `src/Buildout.Cli/Buildout.Cli.csproj` — remove the direct `Microsoft.Extensions.Configuration.EnvironmentVariables` PackageReference (now supplied transitively through Buildout.Core)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All `Buildout.Core.Configuration` types, new options classes, and their validators. Unit tests written RED before each matching implementation.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

> **NOTE: Write each test file first, confirm it FAILS, then implement to make it pass.**

- [ ] T003 Write `tests/Buildout.UnitTests/Configuration/ConfigFlagParserTests.cs` — RED tests: `--config <v>`, `--config=<v>`, `-c <v>`, `-c=<v>` all recognised; last occurrence wins on duplicates; residual-args order preserved; non-config flags untouched; empty-args input returns null path and reference-equal residual
- [ ] T004 Implement `src/Buildout.Core/Configuration/ConfigFlagParser.cs` — internal static class with `Extract(string[] args) → (string? ConfigPath, string[] Residual)`; single linear scan; removes both flag token and value token from residual
- [ ] T005 [P] Create `src/Buildout.Core/Configuration/BuildoutConfigurationException.cs` — public exception with `string Message`, `string? Path`, and `Exception? InnerException`; used for all hard-error cases (no unit test required; exercised by loader tests)
- [ ] T006 [P] Create `src/Buildout.Core/Configuration/BuildoutConfigurationOptions.cs` — internal record `{ string DefaultFilePath; string Prefix; string LegacyOtelEnvVar; }` used by `BuildoutConfiguration.Build` and test-injected overrides
- [ ] T007 [P] Write `tests/Buildout.UnitTests/Configuration/LegacyOtelEndpointSourceTests.cs` — RED tests: contributes `Telemetry:OtlpEndpoint` only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set and non-empty; no-op when env var absent; no empty-string value contributed; source added below `Buildout__`-prefixed layer (later wins in chain)
- [ ] T008 Implement `src/Buildout.Core/Configuration/LegacyOtelEndpointSource.cs` — `IConfigurationSource` + `IConfigurationProvider` pair; reads `OTEL_EXPORTER_OTLP_ENDPOINT` at `Load()` time; contributes nothing if unset or empty
- [ ] T009 [P] Write `tests/Buildout.UnitTests/Configuration/HttpSectionRemapSourceTests.cs` — RED tests: `Http:Timeout` projected to `HttpTimeout`; `Http:UnsafeAllowInsecure` projected to `UnsafeAllowInsecure`; projection is idempotent; non-Http keys untouched; setting only `Http:Timeout` does NOT blank `UnsafeAllowInsecure`
- [ ] T010 Implement `src/Buildout.Core/Configuration/HttpSectionRemapSource.cs` — `IConfigurationSource` + `IConfigurationProvider`; holds back-reference to parent `IConfigurationRoot`; projects both Http keys to flat property names at `Load()` time; registered LAST in the provider chain
- [ ] T011 [P] Write `tests/Buildout.UnitTests/Configuration/UnknownKeyAuditorTests.cs` — RED tests: FR-009 canonical keys produce no warning; unknown root keys each yield exactly one warning; legacy keys in `LegacyKeyHints` table produce a warning that names the new replacement key; `OTEL_EXPORTER_OTLP_ENDPOINT` produces NO warning (it is still honoured)
- [ ] T012 Implement `src/Buildout.Core/Configuration/UnknownKeyAuditor.cs` — internal static `Audit(IConfiguration, ILogger)`; diffs loaded flat key set against the FR-009 schema; emits one warning per unknown key; `LegacyKeyHints` table contains all six legacy-to-new mappings from `contracts/migration.md`
- [ ] T013 [P] Write `tests/Buildout.UnitTests/Configuration/TelemetryOptionsValidatorTests.cs` — RED tests: `OtlpEndpoint` must be absolute URI; scheme must be `http` or `https`; non-absolute URI fails; `ftp` scheme fails; valid `http` and `https` URIs pass; `Enabled` has no constraint
- [ ] T014 [P] Create `src/Buildout.Core/Diagnostics/TelemetryOptions.cs` — `{ bool Enabled = false; Uri OtlpEndpoint = new("http://localhost:4318"); }`
- [ ] T015 Implement `src/Buildout.Core/Diagnostics/TelemetryOptionsValidator.cs` — `IValidateOptions<TelemetryOptions>`; `OtlpEndpoint` must satisfy `IsAbsoluteUri` and scheme must be `http` or `https`
- [ ] T016 [P] Write `tests/Buildout.UnitTests/Configuration/LimitationsOptionsValidatorTests.cs` — RED tests: `LargeDeleteThreshold >= 0` passes; negative value fails with message naming the key
- [ ] T017 [P] Create `src/Buildout.Core/Markdown/Editing/LimitationsOptions.cs` — `{ int LargeDeleteThreshold = 10; }`
- [ ] T018 Implement `src/Buildout.Core/Markdown/Editing/LimitationsOptionsValidator.cs` — `IValidateOptions<LimitationsOptions>`; `LargeDeleteThreshold >= 0`
- [ ] T019 Write `tests/Buildout.UnitTests/Configuration/BuildoutConfigurationTests.cs` — RED tests covering: defaults < default-file < OTEL-fallback < `Buildout__` env precedence; `--config` path overrides default file; `--config` to missing file throws `BuildoutConfigurationException` with path in message; `Buildout__` prefix excludes unrelated env vars; unknown keys warned, not errored; `Buildout__Http__Timeout` maps via remap to `HttpTimeout`; `__` is section separator (`Buildout__Http__Timeout=00:01:00` → config key `Http:Timeout`); `BotToken` value never appears in logger output
- [ ] T020 Implement `src/Buildout.Core/Configuration/BuildoutConfiguration.cs` — public static `Build(string[] args) → (IConfiguration Configuration, string[] ResidualArgs)`; calls `ConfigFlagParser.Extract`; assembles 6-layer chain per `data-model.md`: default JSON file (`optional: true`) or `--config` JSON file (`optional: false`) → `LegacyOtelEndpointSource` → `AddEnvironmentVariables("Buildout__")` → `HttpSectionRemapSource`; calls `UnknownKeyAuditor.Audit`; wraps `FileNotFoundException`, `IOException`, and `JsonException` in `BuildoutConfigurationException` with human-readable message and file path

**Checkpoint**: All Phase 2 unit tests GREEN; `dotnet build` succeeds.

---

## Phase 3: User Story 1 — First-time User Configures Bot Token (Priority: P1) 🎯 MVP

**Goal**: Wire the loader into both presentation projects and register all options classes via DI; a user can run either presentation with only `Buildout__BotToken` or a default JSON file and it just works.

**Independent Test**: Run either presentation with only `Buildout__BotToken=test-token-123` exported; assert `BuildinClientOptions.BotToken == "test-token-123"`. Repeat with env var unset and `~/.config/buildout/config.json` containing `{ "BotToken": "test-token-123" }`; assert same result.

> **NOTE: Write T027–T028 RED before implementing T029–T030.**

- [ ] T021 [P] [US1] Modify `src/Buildout.Core/Buildin/BuildinClientOptions.cs` — add XML doc comments pointing readers to `docs/configuration.md`; no property shape changes
- [ ] T022 [P] [US1] Modify `src/Buildout.Core/Buildin/BuildinClientOptionsValidator.cs` — update failure message strings to reference new config key names: `BotToken`, `BaseUrl`, `Http:Timeout`, `Http:UnsafeAllowInsecure`
- [ ] T023 [US1] Modify `src/Buildout.Core/Markdown/Editing/PageEditorOptions.cs` — add `[Obsolete("Use LimitationsOptions. PageEditorOptions will be removed in a future release.")]`; retain class as a compile-time warning surface for any remaining reference sites
- [ ] T024 [US1] Modify `src/Buildout.Core/Markdown/Editing/PageEditor.cs` — change constructor parameter from `IOptions<PageEditorOptions>` to `IOptions<LimitationsOptions>`
- [ ] T025 [US1] Modify `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` — `AddBuildinClient`: replace `GetSection("Buildin").Bind(...)` with `services.AddOptions<BuildinClientOptions>().Bind(configuration).ValidateOnStart()`; `AddBuildoutCore`: replace `PageEditorOptions` binding with `services.AddOptions<LimitationsOptions>().Bind(configuration.GetSection("Limitations")).ValidateOnStart()` and add `services.AddOptions<TelemetryOptions>().Bind(configuration.GetSection("Telemetry")).ValidateOnStart()`; both methods accept `IConfiguration` obtained from `BuildoutConfiguration.Build`
- [ ] T026 [P] [US1] Extend `tests/Buildout.UnitTests/Buildin/ConfigurationBindingTests.cs` — add sibling tests that construct options by calling `BuildoutConfiguration.Build` with in-memory JSON input; existing assertions must remain GREEN
- [ ] T027 [P] [US1] Write `tests/Buildout.IntegrationTests/Configuration/CliConfigFlagTests.cs` — US1 RED scenarios: env-var-only (`Buildout__BotToken=test-token-123`) path resolves without config error; default-file path (`~/.config/buildout/config.json`) resolves correctly; missing `BotToken` exits non-zero with a message that names `BotToken`, shows `Buildout__BotToken` env var form, and shows the default JSON file path
- [ ] T028 [P] [US1] Write `tests/Buildout.IntegrationTests/Configuration/McpConfigFlagTests.cs` — US1 RED scenarios: env-var-only MCP startup binds `BuildinClientOptions.BotToken` correctly; basic MCP startup succeeds with minimal config
- [ ] T029 [P] [US1] Modify `src/Buildout.Cli/Program.cs` — replace `ConfigurationBuilder` block (lines 9–13) with `var (config, residualArgs) = BuildoutConfiguration.Build(args)`; add `services.AddSingleton<IConfiguration>(config)`; pass `residualArgs` to `app.RunAsync`; catch `BuildoutConfigurationException` at top level, write `e.Message` to `Console.Error`, return exit code 1
- [ ] T030 [P] [US1] Modify `src/Buildout.Mcp/Program.cs` — replace lines 12–21: call `BuildoutConfiguration.Build(args)` → `builder.Configuration.Sources.Clear()` → `builder.Configuration.AddConfiguration(config)`; gate telemetry block on `IOptions<TelemetryOptions>.Value`; delete the `BUILDOUT_TELEMETRY_ENABLED` and `OTEL_EXPORTER_OTLP_ENDPOINT` direct env-var reads; catch `BuildoutConfigurationException` at top level and exit 1

**Checkpoint**: Both presentations start with env var only or default JSON file; missing `BotToken` exits with actionable, non-leaking error message.

---

## Phase 4: User Story 2 — Operator Overrides Config File Per Invocation (Priority: P1)

**Goal**: `--config <path>` / `-c <path>` works on both CLI and MCP; every CLI subcommand's `--help` shows the flag; a missing override path is a hard error with no silent default fallback.

**Independent Test**: Invoke either presentation with `--config ./test-config.json` while `~/.config/buildout/config.json` also exists; assert bound `BotToken` equals the value from `./test-config.json`.

> **NOTE: Write T031–T032 RED before creating T033 and updating Settings classes.**

- [ ] T031 [P] [US2] Extend `tests/Buildout.IntegrationTests/Configuration/CliConfigFlagTests.cs` — US2 RED scenarios: `--config ./other.json` overrides default file; `-c ./other.json` short form behaves identically; missing `--config` path exits non-zero with `Configuration file not found:` message and does NOT silently consult default file; env var wins over `--config` file value when both present
- [ ] T032 [P] [US2] Extend `tests/Buildout.IntegrationTests/Configuration/McpConfigFlagTests.cs` — US2 RED scenarios: `-c ./mcp-prod.json` loads specified file and ignores default; missing `-c` path exits non-zero with `Configuration file not found:` message
- [ ] T033 [US2] Create `src/Buildout.Cli/Commands/BuildoutCommandSettings.cs` — abstract `CommandSettings` base with `[CommandOption("-c|--config")] [Description("Path to a JSON configuration file. Overrides the default ~/.config/buildout/config.json.")] string? ConfigPath { get; init; }`
- [ ] T034 [P] [US2] Modify `src/Buildout.Cli/Commands/CreateSettings.cs` — inherit `BuildoutCommandSettings`
- [ ] T035 [P] [US2] Modify `src/Buildout.Cli/Commands/DbSettings.cs` — inherit `BuildoutCommandSettings`
- [ ] T036 [P] [US2] Modify `src/Buildout.Cli/Commands/DbViewSettings.cs` — inherit `BuildoutCommandSettings`
- [ ] T037 [P] [US2] Modify `src/Buildout.Cli/Commands/DeleteSettings.cs` — inherit `BuildoutCommandSettings`
- [ ] T038 [P] [US2] Modify `src/Buildout.Cli/Commands/RestoreSettings.cs` — inherit `BuildoutCommandSettings`
- [ ] T039 [P] [US2] Modify `src/Buildout.Cli/Commands/UpdateSettings.cs` — inherit `BuildoutCommandSettings`
- [ ] T040 [US2] Modify inline settings classes declared inside GetCommand and SearchCommand source files in `src/Buildout.Cli/Commands/` — inherit `BuildoutCommandSettings`

**Checkpoint**: `buildout-cli search --help` (and all other subcommands) display `-c|--config` in their option list; `--config ./real.json` loads the file; `--config ./missing.json` fails with the documented error.

---

## Phase 5: User Story 3 — Layered Overrides for Existing Developer (Priority: P2)

**Goal**: Env vars override specific file keys without blanking adjacent keys; `Buildout__Http__Timeout` changes only timeout; `OTEL_EXPORTER_OTLP_ENDPOINT` is honoured below `Buildout__Telemetry__OtlpEndpoint`; partial-section env override is correct.

**Independent Test**: JSON file sets `BotToken`, `BaseUrl`, `Http:Timeout=00:00:30`; export `Buildout__Http__Timeout=00:01:00`; assert bound options have `BotToken` and `BaseUrl` from file and `HttpTimeout = 1 minute`.

> **NOTE: Write T041 RED before T042 verification.**

- [ ] T041 [US3] Write `tests/Buildout.IntegrationTests/Configuration/PrecedenceMatrixTests.cs` — `[Theory]` rows covering: default used when nothing set; file overrides default; `Buildout__` env overrides file for that key only (other keys from file unchanged); `OTEL_EXPORTER_OTLP_ENDPOINT` populates `Telemetry:OtlpEndpoint` when `Buildout__Telemetry__OtlpEndpoint` absent; `Buildout__Telemetry__OtlpEndpoint` wins over `OTEL_EXPORTER_OTLP_ENDPOINT`; `Buildout__Http__Timeout` alone does not blank `Http:UnsafeAllowInsecure` (RED)
- [ ] T042 [US3] Verify and update `src/Buildout.Core/Configuration/BuildoutConfiguration.cs` and `src/Buildout.Core/Configuration/HttpSectionRemapSource.cs` handle all partial-section override cases; run T041 to GREEN; fix any layer-ordering issues found

**Checkpoint**: Full FR-002 precedence matrix GREEN.

---

## Phase 6: User Story 4 — User-Facing Configuration Documentation (Priority: P2)

**Goal**: `docs/configuration.md` satisfies all FR-011 requirements; `docs/configuration.example.json` is valid per FR-012; the docs-lint test confirms key-set parity with the runtime schema.

**Independent Test**: Open `docs/configuration.md`; every FR-009 key has type, default, validation, env-var form, JSON form, and one worked example per channel; document is reachable via a relative link from at least one root-level markdown file.

> **NOTE: Write T043 RED before authoring docs; docs must turn it GREEN.**

- [ ] T043 [P] [US4] Write `tests/Buildout.UnitTests/Configuration/DocumentationKeysTests.cs` — walks up from `AppContext.BaseDirectory` to locate `docs/configuration.md`; parses the FR-009 key table (identified by header `| Key | Type | Default | Required | Validation |`); diffs extracted key set against keys reflected from `BuildinClientOptions` + `TelemetryOptions` + `LimitationsOptions` (with `Http:` and `Telemetry:` and `Limitations:` section prefixes); asserts exact equality with symmetric-difference failure message (RED — fails until docs are written)
- [ ] T044 [US4] Create `docs/README.md` — one-line introduction to the `docs/` directory with a link to `configuration.md`; provides a discoverable entry point
- [ ] T045 [US4] Create `docs/configuration.md` — per FR-011: (a) summary paragraph explaining loading and precedence order; (b) file location (`~/.config/buildout/config.json`) and `--config` override with default-file vs. override-file error semantics; (c) complete FR-009 key reference table with type / default / validation / env-var form / JSON form / one worked example per channel; (d) ASCII or text precedence diagram; (e) "Migration from earlier versions" section reproducing the rename table from `contracts/migration.md`; (f) "Common pitfalls" covering R10 (`appsettings.json` not discovered), missing `$HOME` fallback, `TimeSpan` format (`HH:MM:SS`), `__` section separator, env-var case sensitivity
- [ ] T046 [P] [US4] Create `docs/configuration.example.json` — every FR-009 key at its default value; `BotToken` set to `"<your-bot-token>"`; JSON structure matches `contracts/schema.md` JSON form; valid per JSON spec
- [ ] T047 [US4] Run `tests/Buildout.UnitTests/Configuration/DocumentationKeysTests.cs` to GREEN — key set in `docs/configuration.md` must exactly match the reflection-derived schema; fix any missing or extra keys in the document

**Checkpoint**: Documentation complete; docs-lint test GREEN; `docs/configuration.md` reachable from repo root.

---

## Phase 7: User Story 5 — Misconfiguration Surfaces a Clear Error (Priority: P3)

**Goal**: Invalid JSON, missing required key, unrecognised legacy key, and unparseable values each produce the exact error or warning text specified in the contracts; `BotToken` value never leaks to any output stream.

**Independent Test**: JSON file with `"Http": { "Timeout": "not-a-duration" }` → process exits non-zero with a message naming `Http:Timeout` and describing the expected format.

> **NOTE: Write T048–T049 RED before verification tasks T050–T053.**

- [ ] T048 [P] [US5] Write `tests/Buildout.IntegrationTests/Configuration/MigrationTests.cs` — RED tests: loading config with `Buildin:BotToken` key produces a warning message containing `BotToken`; `PageEditor:LargeDeleteThreshold` warning names `Limitations:LargeDeleteThreshold`; env var `BUILDOUT_TELEMETRY_ENABLED` warning names `Buildout__Telemetry__Enabled`; `OTEL_EXPORTER_OTLP_ENDPOINT` env var produces NO warning (it is still honoured as a fallback)
- [ ] T049 [P] [US5] Write `tests/Buildout.IntegrationTests/Configuration/SecretLeakTests.cs` — RED test: set `Buildout__BotToken=DO_NOT_LEAK_BUILDOUT_TOKEN_123` as a process env var; run CLI startup (`buildout-cli --help`) and MCP startup-shutdown cycle; capture all stdout / stderr / logger output; assert the literal string `DO_NOT_LEAK_BUILDOUT_TOKEN_123` does not appear anywhere in captured output
- [ ] T050 [US5] Verify `src/Buildout.Core/Configuration/BuildoutConfigurationException.cs` error message patterns match `contracts/loader.md` error taxonomy exactly — file-not-found message includes path; is-directory message includes path; permission-denied message includes path; invalid-JSON message includes path and (if available from the provider) line and column numbers
- [ ] T051 [US5] Verify `src/Buildout.Core/Buildin/BuildinClientOptionsValidator.cs` `BotToken` failure message contains no value text — message must be a static string like `BotToken is required.` with no interpolated value (FR-015 / SC-007)
- [ ] T052 [US5] Run `tests/Buildout.IntegrationTests/Configuration/MigrationTests.cs` to GREEN — fix `UnknownKeyAuditor.LegacyKeyHints` table entries until all six legacy-key warning messages match the `contracts/migration.md` rename table
- [ ] T053 [US5] Run `tests/Buildout.IntegrationTests/Configuration/SecretLeakTests.cs` to GREEN — confirm no `BotToken` leak path exists through startup, error messages, validation output, or help text

**Checkpoint**: All US5 tests GREEN; misconfigured installations surface actionable, non-leaking error messages.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Full build verification, complete test suite run, and post-implementation quality checks.

- [ ] T054 Run `dotnet build` across the full solution and fix all compilation errors introduced by this feature (including nullable warnings-as-errors and any `[Obsolete]` suppression needed in `PageEditorOptions.cs`)
- [ ] T055 Run `dotnet test` against both `tests/Buildout.UnitTests` and `tests/Buildout.IntegrationTests`; fix any remaining test failures; do NOT skip or disable any test
- [ ] T056 [P] Verify quickstart.md scenarios against the implementation: Scenario 1 (env var only), Scenario 2 (`--config` per-environment files), Scenario 3 (MCP launcher with `-c`) each produce the expected outcomes described in `specs/010-configuration/quickstart.md`
- [ ] T057 [P] Confirm `docs/configuration.md` is linked from the repository root `README.md` or `docs/README.md`, satisfying the US4 independent-test reachability criterion
- [ ] T058 [P] Verify no real token values or sentinel strings are present in any committed file; confirm `docs/configuration.example.json` contains only `"<your-bot-token>"` as the `BotToken` value

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Phase 1 (csproj must compile). **Blocks all user story phases.**
- **US1 (Phase 3)**: Depends on Phase 2 complete.
- **US2 (Phase 4)**: Depends on Phase 3 (presentations must be wired before Spectre integration tests can run).
- **US3 (Phase 5)**: Depends on Phases 3–4 (full layer chain in both presentations needed for `PrecedenceMatrixTests`).
- **US4 (Phase 6)**: Depends on Phase 2 (options classes needed for `DocumentationKeysTests` reflection); can begin T043–T046 in parallel with Phases 3–5.
- **US5 (Phase 7)**: Depends on Phases 3–4 (full presentation startup needed for `SecretLeakTests` and `MigrationTests`).
- **Polish (Phase 8)**: Depends on all prior phases.

### User Story Dependencies

- **US1 (P1)**: No story-level dependency — starts after Foundational.
- **US2 (P1)**: Depends on US1 (Phase 3) — Spectre settings update needs working `Program.cs`.
- **US3 (P2)**: Depends on US1 + US2 — precedence matrix tests exercise the complete stack.
- **US4 (P2)**: Depends on Foundational only (Phase 2, through T020) — documentation can be written independently after options classes exist.
- **US5 (P3)**: Depends on US1 + US2 — integration tests need real presentation startup.

### Within Each Phase: Sequential Rules

- Test tasks (numbered lower) MUST precede their matching implementation tasks.
- Options data classes (T014, T017) can be created at the same time as their test files.
- Options validators (T015, T018) must follow their data classes.
- `BuildoutConfiguration.cs` (T020) must follow all source sub-components (T004, T008, T010, T012) and its own test file (T019).

---

## Parallel Execution Examples

### Phase 2: Write all isolated source tests simultaneously

```
Start in parallel (different files, no inter-dependencies):
  T007: LegacyOtelEndpointSourceTests.cs
  T009: HttpSectionRemapSourceTests.cs
  T011: UnknownKeyAuditorTests.cs
  T013: TelemetryOptionsValidatorTests.cs
  T016: LimitationsOptionsValidatorTests.cs

Then implement in parallel once each is RED:
  T008: LegacyOtelEndpointSource.cs     ← after T007
  T010: HttpSectionRemapSource.cs        ← after T009
  T012: UnknownKeyAuditor.cs             ← after T011
  T014: TelemetryOptions.cs              ← parallel with T013
  T015: TelemetryOptionsValidator.cs     ← after T013 + T014
  T017: LimitationsOptions.cs            ← parallel with T016
  T018: LimitationsOptionsValidator.cs   ← after T016 + T017
```

### Phase 3: Docs / XML / tests in parallel, then implementations

```
Start in parallel:
  T021: BuildinClientOptions.cs (XML doc)
  T022: BuildinClientOptionsValidator.cs (error messages)
  T026: ConfigurationBindingTests.cs (new loader tests)
  T027: CliConfigFlagTests.cs (US1 scenarios)
  T028: McpConfigFlagTests.cs (US1 scenarios)

After T025 (ServiceCollectionExtensions.cs) is done:
  T029: Buildout.Cli/Program.cs    — in parallel with →
  T030: Buildout.Mcp/Program.cs
```

### Phase 4: All Settings class inheritance in parallel

```
After T033 (BuildoutCommandSettings.cs):
  T034: CreateSettings.cs   ┐
  T035: DbSettings.cs       │
  T036: DbViewSettings.cs   │ all in parallel
  T037: DeleteSettings.cs   │
  T038: RestoreSettings.cs  │
  T039: UpdateSettings.cs   ┘
```

---

## Implementation Strategy

### MVP First (US1 + US2 only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks everything)
3. Complete Phase 3: US1 — bot token via env var or default file
4. **STOP and VALIDATE**: env-var-only and default-file paths work end-to-end
5. Complete Phase 4: US2 — `--config` override flag
6. **STOP and VALIDATE**: `--config` flag works and shows in every subcommand `--help`
7. Ship P1 scope — both user stories complete

### Incremental Delivery

1. Phases 1–2 → Core loader ready, all unit tests GREEN
2. Phase 3 (US1) → Bot token works → Validate → Demo
3. Phase 4 (US2) → `--config` override works → Validate → Demo
4. Phase 5 (US3) → Full precedence matrix verified
5. Phase 6 (US4) → Documentation shipped
6. Phase 7 (US5) → Error quality hardened
7. Phase 8 → Polish, full suite GREEN

---

## Notes

- `[P]` = different files, no shared-state blocking; safe to run simultaneously with other `[P]` tasks in the same phase
- `[USn]` maps each task to a user story for traceability; Setup, Foundational, and Polish tasks carry no story label
- Test-first is NON-NEGOTIABLE (Principle IV): RED first, implement, confirm GREEN — never skip, disable, or delete a test to clear a build
- Stage files explicitly by path when committing (`git add src/... tests/...`); never use `git add -A`
- Commit after each logical group rather than after each individual task
- Phase 8 quickstart verification (T056) is the final human-readable gate before calling the feature done
