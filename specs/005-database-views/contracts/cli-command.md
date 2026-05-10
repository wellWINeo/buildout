# Contract: `db view` CLI Command (Buildout.Cli)

## Invocation

```
buildout db view <database_id>
                 [--style <table|board|gallery|list|calendar|timeline>]
                 [--group-by <property name>]
                 [--date-property <property name>]
```

`--style` defaults to `table`. `--group-by` is required when
`--style board`; `--date-property` is required when
`--style calendar` or `--style timeline`.

## Registration

In `src/Buildout.Cli/Program.cs`:

```csharp
config.AddBranch<DbSettings>("db", db =>
{
    db.AddCommand<DbViewCommand>("view");
});
```

The `DbSettings` branch carries no options; it exists only to satisfy
`AddBranch<T>`. Following the established pattern (`GetCommand`,
`SearchCommand`), the new command class derives from
`AsyncCommand<DbViewSettings>` and is constructor-injected with:

- `IDatabaseViewRenderer`
- `IAnsiConsole`
- `TerminalCapabilities`
- `MarkdownTerminalRenderer` (used only for `Table` style on TTY,
  see Plain vs Styled below)

## Settings shape

```csharp
public sealed class DbViewSettings : CommandSettings
{
    [CommandArgument(0, "<DATABASE_ID>")]
    public required string DatabaseId { get; init; }

    [CommandOption("-s|--style")]
    [DefaultValue(DatabaseViewStyle.Table)]
    public DatabaseViewStyle Style { get; init; }

    [CommandOption("-g|--group-by")]
    public string? GroupByProperty { get; init; }

    [CommandOption("-d|--date-property")]
    public string? DateProperty { get; init; }
}
```

## Plain vs styled

Mirrors `GetCommand`:

```csharp
var rendered = await _renderer.RenderAsync(request, ct);
if (_caps.IsStyledStdout && settings.Style == DatabaseViewStyle.Table)
{
    _terminalRenderer.Render(rendered);
}
else
{
    _console.Write(new Text(rendered));
}
```

Only the `Table` style uses `MarkdownTerminalRenderer` for TTY —
its output is a GFM pipe-table that the existing renderer already
knows how to style. The other five styles emit ASCII exclusively
in both TTY and non-TTY modes; layering Spectre on them would risk
drifting from the byte-identical body returned by MCP.

## Exit codes (reused from features 002/003)

| Outcome                                      | Exit code |
|----------------------------------------------|-----------|
| Success                                      | 0         |
| Validation error (unknown style, etc.)       | 2         |
| Buildin 404 (database not found)             | 3         |
| Buildin 401 / 403 (auth failure)             | 4         |
| Transport / timeout error                    | 5         |
| Generic buildin error                        | 6         |

Validation errors are emitted before any network call. Their messages
follow the existing precedent ("Unknown style 'xyz'. Valid styles:
table, board, gallery, list, calendar, timeline.") and name the
offending input plus the valid alternatives.

## Help text

`buildout db view --help` MUST list the supported styles, the
required-vs-optional matrix for `--group-by` / `--date-property`,
and one example per style.
