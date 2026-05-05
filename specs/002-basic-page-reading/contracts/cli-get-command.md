# Contract — CLI command `buildout get <page_id>`

The first user-facing CLI command in buildout. Defined as a
`Spectre.Console.Cli` command type and registered in
`Buildout.Cli/Program.cs`.

## Command shape

```text
buildout get <page_id>
```

| Element | Form | Notes |
|---|---|---|
| Command name | `get` | Single positional verb. |
| Argument | `<page_id>` | Required positional. Any form `IBuildinClient` accepts. |
| Options | (none in v1) | `--rich` / `--no-rich` overrides explicitly out of scope per spec. |
| Stdout | rendered Markdown | Plain CommonMark/GFM (non-TTY) or styled via Markdig + Spectre (TTY). |
| Stderr | error messages on failure | Single human-readable line; failure class identifiable. |
| Exit codes | per R9 | 0 success, 3 not-found, 4 auth, 5 transport, 6 unexpected (1/2 reserved for parser/argument errors). |

## Implementation shape

Located at `src/Buildout.Cli/Commands/GetCommand.cs`. Implements
`AsyncCommand<GetCommand.Settings>` from `Spectre.Console.Cli`.

```text
public sealed class GetCommand : AsyncCommand<GetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<page_id>")]
        [Description("Buildin page id (UUID, with or without dashes).")]
        public string PageId { get; init; } = string.Empty;
    }

    private readonly IPageMarkdownRenderer _renderer;
    private readonly IAnsiConsole _console;
    private readonly TerminalCapabilities _caps;

    public GetCommand(IPageMarkdownRenderer renderer,
                      IAnsiConsole console,
                      TerminalCapabilities caps) { … }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings) { … }
}
```

`Program.cs` is updated to register a Spectre `CommandApp` with the
constitution-compatible `ITypeRegistrar` shim so the command resolves through
`Microsoft.Extensions.DependencyInjection`. `AddBuildoutCore(...)` provides
`IPageMarkdownRenderer`; `AnsiConsole.Console` and a `TerminalCapabilities`
factory are registered locally.

## Behavioural contract

### TTY detection (FR-007 / R4)

On entry, the command reads `_caps.IsStyledStdout`:

- **Styled** (`AnsiConsole.Profile.Capabilities.Ansi == true` AND stdout is
  not redirected AND `NO_COLOR` is unset): pipe the Markdown string through
  `MarkdownTerminalRenderer`, which writes via `IAnsiConsole`.
- **Plain** (any other case): write the Markdown string verbatim to
  `Console.Out`. No Spectre involvement; zero terminal escape codes by
  construction.

The plain path ensures FR-008 byte-equivalence with the MCP resource body.

### Rich-mode rendering (R1)

`MarkdownTerminalRenderer`:

1. Parses the Markdown via Markdig.
2. Walks the AST and emits to `IAnsiConsole`:
   - H1 → bold + dim rule below.
   - H2/H3/H4 → bold, decreasing emphasis.
   - Paragraph → `Markup` with annotation tags (`[bold]…[/]` etc.) escaped
     correctly so Markdig literal text never gets re-interpreted as Spectre
     markup.
   - Bulleted/numbered/task lists → `Tree` or simple indent + bullet glyph;
     v1 uses indent + bullet glyph for simplicity and good output even on
     dumb terminals.
   - Code block → `Panel` with `BoxBorder.Rounded`, language tag in panel
     header when present.
   - Block quote → indented italic, with leading `│` glyph.
   - Thematic break → `AnsiConsole.Write(new Rule())`.
   - HTML block (the unsupported placeholder) → dim grey text, verbatim.
3. Handles inline formatting per CommonMark: `**bold**`, `*italic*`,
   `~~strike~~`, `` `code` `` (rendered as Spectre `[underline]` or color),
   and link text (rendered with the link target shown after if it's a
   `buildin://` URI, omitted otherwise to avoid noise).

The walker is scoped to the supported subset; anything else falls through to
plain text — which is sufficient because Core never produces unsupported
forms.

### Error handling (FR-009 / R9)

`ExecuteAsync` wraps the renderer call in a try/catch:

```text
try
{
    var markdown = await _renderer.RenderAsync(settings.PageId, context.CancellationToken);
    if (_caps.IsStyledStdout)
        _terminalRenderer.Render(markdown);
    else
        Console.Out.Write(markdown);
    return 0;
}
catch (BuildinApiException ex) when (ex.Error is NotFoundError)         { _console.MarkupLineInterpolated($"[red]Page not found:[/] {settings.PageId}"); return 3; }
catch (BuildinApiException ex) when (ex.Error is UnauthorizedError or ForbiddenError) { _console.MarkupLineInterpolated($"[red]Authentication failure:[/] {ex.Error.Message}"); return 4; }
catch (BuildinApiException ex) when (ex.Error is TransportError)        { _console.MarkupLineInterpolated($"[red]Transport failure:[/] {ex.Error.Message}"); return 5; }
catch (BuildinApiException ex)                                          { _console.MarkupLineInterpolated($"[red]Unexpected buildin error:[/] {ex.Error.Message}"); return 6; }
```

(Exact `BuildinError` discriminator names follow feature 001's existing
shape; the renderer never wraps or rethrows.)

Error messages always go to **stderr** via `_console` configured for stderr;
stdout is reserved for rendered Markdown.

`OperationCanceledException` (Ctrl-C) is allowed to propagate; Spectre's
default handling exits non-zero. We do not customise this for v1.

## Test obligations

| Test class | Path | Purpose |
|---|---|---|
| `GetCommandTests.ExitCodeOnSuccess` | `tests/Buildout.IntegrationTests/Cli/GetCommandTests.cs` | Happy path: returns 0; stdout contains the renderer output; stderr is empty. Test injects a fake `IBuildinClient`. |
| `GetCommandTests.PlainOutputMatchesMcp` | (same file) | Non-TTY mode produces stdout byte-identical to what the MCP resource returns for the same fixture (FR-008). |
| `GetCommandTests.RichOutputContainsAnsi` | (same file) | Styled mode (forced via injected `TerminalCapabilities` fake) writes ANSI escape codes. |
| `GetCommandTests.PlainOutputContainsNoAnsi` | (same file) | Non-styled mode (forced via injected fake) writes zero escape codes. |
| `GetCommandTests.NotFoundExitCode` | (same file) | `BuildinApiException(NotFoundError)` → exit 3 + stderr message identifying the page id. |
| `GetCommandTests.AuthFailureExitCode` | (same file) | Auth error → exit 4 + stderr distinguishes auth from transport. |
| `GetCommandTests.TransportFailureExitCode` | (same file) | Transport error → exit 5. |
| `GetCommandTests.UnexpectedErrorExitCode` | (same file) | Unmapped error → exit 6. |
| `GetCommandTests.MissingPageIdShowsParserError` | (same file) | Spectre's parser exits 1 / 2 with usage; not our concern beyond a smoke check. |

Tests run the command in-process via Spectre's `CommandApp` test harness and
capture stdout/stderr + the returned exit code.

## Out-of-scope (deferred)

- `--rich` / `--no-rich` override flags.
- `--output <file>` — already achievable via shell redirection.
- Pretty-printing front-matter / page metadata (timestamps, parent chain).
- Sub-commands (`buildout get block`, `buildout list`, etc.) — separate
  features.
