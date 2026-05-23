# Contract: `--config` / `-c` Flag on CLI and MCP

## Surface

Both `buildout-cli` and `buildout-mcp` accept the same flag:

```text
--config <path>
--config=<path>
-c <path>
-c=<path>
```

Either form is acceptable. The flag MAY appear anywhere in the
argument list (before, between, or after subcommand args). The
`Buildout.Core.Configuration.ConfigFlagParser` strips both the flag
and its value from the argument list before the per-presentation
argument parser (Spectre.Console.Cli or
`Host.CreateApplicationBuilder`) sees the remaining tokens.

### Spectre.Console.Cli surfacing (CLI)

Every command's `Settings` class inherits from
`Buildout.Cli.Commands.BuildoutCommandSettings`, which declares:

```csharp
[CommandOption("-c|--config")]
[Description("Path to a JSON configuration file. Overrides the default ~/.config/buildout/config.json.")]
public string? ConfigPath { get; init; }
```

The property is documented in `--help` output for every command but
NEVER read at runtime — the loading happens in `Program.cs` before
Spectre runs. Spectre's re-parse of the flag is a no-op for
configuration purposes.

## Precedence

The `--config` path REPLACES the default file lookup; it does not
supplement it. Specifically:

- `--config <path>` supplied → the loader loads `<path>` and ignores
  `~/.config/buildout/config.json` (even if it exists).
- `--config` NOT supplied → the loader loads
  `~/.config/buildout/config.json` if it exists, or skips silently if
  it does not.

Environment variables (`Buildout__*`) override file values regardless
of which file was loaded.

## Error behaviour

| Scenario | Behaviour |
|----------|-----------|
| `--config <path>` and `<path>` does not exist | Hard error: process exits non-zero with `Configuration file not found: <path>`. Default file is NOT silently consulted. |
| `--config <path>` and `<path>` is a directory | Hard error: `Configuration path is not a file: <path>`. |
| `--config <path>` and `<path>` is unreadable (perm denied) | Hard error: `Configuration file is not readable: <path>`. |
| `--config <path>` and `<path>` contains invalid JSON | Hard error: `Configuration file is not valid JSON: <path> (line N, column M)`. |
| `--config` supplied twice | Last occurrence wins (Microsoft.Extensions.Configuration semantics). |
| `--config` supplied with no value (e.g., trailing `-c` at end of args) | Argument parser surfaces its standard missing-value error; the loader does not catch it specifically. |
| Default file present but unreadable (perm denied) | Hard error: `Configuration file is not readable: <path>`. The silent-skip path applies ONLY to file-not-found. |
| Default file present but invalid JSON | Hard error: `Configuration file is not valid JSON: ~/.config/buildout/config.json (line N, column M)`. |

All errors are surfaced as `BuildoutConfigurationException` from
`BuildoutConfiguration.Build`; the presentation catches it at
`Program.cs` top level, prints `Message` to stderr, and exits
non-zero.

## Default file path resolution

The default path is resolved as follows:

1. `Environment.GetFolderPath(SpecialFolder.UserProfile)` →
   `<home>`.
2. `Path.Combine(<home>, ".config", "buildout", "config.json")`.

If `<home>` resolves to empty/null (rare; service accounts without a
user profile), default-file discovery is silently skipped — the
process is expected to be configured via env vars or `--config`. This
is documented in `docs/configuration.md` under "Common pitfalls".

No XDG_CONFIG_HOME respect, no AppData rewriting on Windows, no
Library/Application Support rewriting on macOS. Operators wanting
those paths can supply `--config`.

## MCP launcher considerations

MCP servers are typically launched by clients (e.g. Claude Code,
Claude Desktop) that pass `args` but may or may not propagate the
parent process environment. The `--config` flag is the
launcher-friendly channel because it does not depend on env-var
inheritance.

Example client manifest fragment:

```json
{
  "command": "buildout-mcp",
  "args": ["-c", "/etc/buildout/prod.json"]
}
```

This works identically whether the launcher inherits `$HOME` or not.
