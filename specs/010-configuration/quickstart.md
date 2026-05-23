# Phase 1 Quickstart: Unified Configuration

Three scenarios that exercise the feature end-to-end. The
acceptance scenarios under spec User Stories 1, 2, and 4 are
verified by integration tests that mirror these steps; the steps
here are the runnable user-facing version.

## Scenario 1 — First-run with a single env var (User Story 1)

```bash
# Prerequisites: buildout-cli is installed and on $PATH.
# No configuration files exist yet.

export Buildout__BotToken="ntn_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx"

buildout-cli search "release notes"
# -> succeeds, queries buildin with the exported token,
#    prints search results
```

Expected outcomes:

- The buildin client is constructed with the exported `BotToken` and
  defaults for all other settings.
- No file is read (no `~/.config/buildout/config.json` exists).
- No warning lines on stderr.
- Exit code 0.

## Scenario 2 — Per-environment file via `--config` (User Story 2)

```bash
# Set up two environment-specific files.
cp docs/configuration.example.json ~/dev.json
cp docs/configuration.example.json ~/prod.json

# Edit each to point at the right workspace.
$EDITOR ~/dev.json    # set BotToken + (optionally) BaseUrl
$EDITOR ~/prod.json

# Run against the dev workspace.
buildout-cli --config ~/dev.json search "release notes"

# Run against prod with the short form.
buildout-cli -c ~/prod.json search "release notes"
```

Expected outcomes:

- Each invocation reads ONLY the file specified by `--config`/`-c`;
  `~/.config/buildout/config.json` is not consulted.
- A typo in the path (e.g.,
  `buildout-cli --config ~/typo.json search`) exits non-zero with
  `Configuration file not found: ~/typo.json` on stderr.
- Mixing the file with env vars works: if
  `Buildout__Http__Timeout=00:01:00` is exported, the timeout
  overrides what's in the file but the rest of the file's values
  remain.

## Scenario 3 — MCP launcher integration (User Story 4)

For a Claude Code (or similar) MCP server configuration, the launcher
manifest does not depend on inherited env vars:

```json
{
  "mcpServers": {
    "buildout-prod": {
      "command": "buildout-mcp",
      "args": ["-c", "/etc/buildout/prod.json"]
    },
    "buildout-staging": {
      "command": "buildout-mcp",
      "args": ["--config", "/etc/buildout/staging.json"]
    }
  }
}
```

Expected outcomes:

- Each server loads its named file at startup; the two servers do
  not interfere with each other.
- Removing `prod.json` between launcher restarts surfaces
  `Configuration file not found: /etc/buildout/prod.json` to the
  launcher's log; the server fails fast rather than silently
  falling back to env vars or the default file.
- Telemetry can be enabled per environment by either setting
  `Telemetry.Enabled = true` in the file or exporting
  `Buildout__Telemetry__Enabled=true` (the latter via the
  launcher's own env management).
- A pre-existing
  `OTEL_EXPORTER_OTLP_ENDPOINT=https://otel.example/v1/otlp` in the
  launcher's environment is HONOURED as the OTLP endpoint unless
  the file or a `Buildout__Telemetry__OtlpEndpoint` env var
  overrides it.

## Verification scripts

Each scenario maps to one or more integration tests:

| Scenario step | Integration test |
|---------------|------------------|
| 1 — env-var-only happy path | `Buildout.IntegrationTests/Configuration/CliConfigFlagTests.HappyPath_EnvVarOnly` |
| 1 — exit code on success | shared with the test above |
| 2 — `--config <path>` overrides default file | `Buildout.IntegrationTests/Configuration/CliConfigFlagTests.ConfigFlag_OverridesDefaultFile` |
| 2 — `-c <path>` short form | `Buildout.IntegrationTests/Configuration/CliConfigFlagTests.ShortFlag_LoadsFile` |
| 2 — missing-file hard error | `Buildout.IntegrationTests/Configuration/CliConfigFlagTests.MissingFile_HardError` |
| 2 — env var overrides file value | `Buildout.IntegrationTests/Configuration/PrecedenceMatrixTests.EnvVar_OverridesFile` |
| 3 — MCP loads `-c` file | `Buildout.IntegrationTests/Configuration/McpConfigFlagTests.ShortFlag_LoadsFile` |
| 3 — OTel fallback honoured | `Buildout.IntegrationTests/Configuration/PrecedenceMatrixTests.OtelEnvVar_HonouredAsFallback` |
| 3 — Buildout-prefixed env wins over OTel fallback | `Buildout.IntegrationTests/Configuration/PrecedenceMatrixTests.BuildoutEnv_WinsOverOtelFallback` |
