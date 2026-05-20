# Contract — CLI: `buildout delete` and `buildout restore`

## Command surface

### `buildout delete <page_id> [--print summary|json]`

| Argument / flag | Required | Default | Notes |
|--|--|--|--|
| `<page_id>` (positional) | yes | n/a | UUID of the page to archive. |
| `--print` | no | `summary` | `summary` (one-line human-readable) or `json` (camelCase JSON object). |

### `buildout restore <page_id> [--print summary|json]`

Symmetric to `delete`. Identical argument shape.

## Settings classes (Spectre.Console.Cli)

```csharp
public sealed class DeleteSettings : Spectre.Console.Cli.CommandSettings
{
    [CommandArgument(0, "<page_id>")]
    [Description("Buildin page id to archive (soft-delete).")]
    public required string PageId { get; init; }

    [CommandOption("--print")]
    [Description("Output mode: summary (default) or json.")]
    public string PrintMode { get; init; } = "summary";
}
```

`RestoreSettings` is the same shape with description updated to "Buildin page id to
restore (un-archive)."

## Output — `--print summary` (default)

### Success (state change)

```
Deleted page <page_id>: archived=true (changed=true)
```

```
Restored page <page_id>: archived=false (changed=true)
```

### Success (no-op short-circuit)

```
Deleted page <page_id>: archived=true (changed=false, no-op)
```

```
Restored page <page_id>: archived=false (changed=false, no-op)
```

### Failure (stderr)

```
Delete failed [NotFound]: Page <page_id> not found.
```

```
Restore failed [Auth]: Authentication failure: <message>.
```

The `<failure_class>` token is the `PageLifecycleOutcome.FailureClass` enum value (PascalCase
matches the spec 006 `CreateCommand` convention).

## Output — `--print json`

### Success

```json
{"pageId":"<id>","archived":true,"changed":true}
```

```json
{"pageId":"<id>","archived":false,"changed":true}
```

### Success (no-op)

```json
{"pageId":"<id>","archived":true,"changed":false}
```

### Failure (stderr)

```json
{"pageId":"<id>","archived":null,"changed":false,"failureClass":"NotFound","errorMessage":"Page <id> not found."}
```

Serialised with `System.Text.Json` using `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`
(same instance shape as `UpdateCommand.OutputJsonOptions`).

## Exit codes

| Outcome | Exit code |
|--|--|
| Success (state change or no-op) | `0` |
| Validation (missing/empty `page_id`) | `2` |
| `FailureClass.NotFound` | `3` |
| `FailureClass.Auth` | `4` |
| `FailureClass.Transport` | `5` |
| `FailureClass.Unexpected` | `6` |

Matches the spec 006/008 CLI convention (`CreateCommand` and `UpdateCommand` use the
same exit-code table for the same failure classes).

## Stream discipline

- Success output (both `summary` and `json`) is written to **stdout**.
- Failure output is written to **stderr**.
- The summary mode always terminates its stdout output with a single newline.
- The JSON mode emits one JSON object on a single line followed by a newline (no
  trailing whitespace, no surrounding array).

## Registration

In `Buildout.Cli.Program.cs`:

```csharp
app.Configure(config =>
{
    config.AddCommand<CreateCommand>("create");
    config.AddCommand<GetCommand>("get");
    config.AddCommand<SearchCommand>("search");
    config.AddCommand<UpdateCommand>("update");
    config.AddCommand<DeleteCommand>("delete");      // NEW
    config.AddCommand<RestoreCommand>("restore");    // NEW
    config.AddBranch<DbSettings>("db", db => { db.AddCommand<DbViewCommand>("view"); });
});
```

## Verification

- `DeleteCommandTests` (CLI integration) covers: success (state change), success (no-op),
  every error class, `--print summary` and `--print json` output formats, exit codes.
- `RestoreCommandTests` is the symmetric counterpart.
- `DeleteRestoreSymmetryTests` (Cross) covers SC-004: `delete && restore` leaves
  archive state unchanged; JSON output of CLI matches the JSON in the MCP `TextContentBlock`
  for the same page state.
