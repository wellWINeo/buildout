# Contract — `buildout search` CLI command

Located at `src/Buildout.Cli/Commands/SearchCommand.cs`. Registered
in `Program.cs` via
`config.AddCommand<SearchCommand>("search")` alongside the existing
`get` command from feature 002.

## Command signature

```text
buildout search <query> [--page <PAGE_ID>]
```

- `<query>` — required positional argument. Single string. Quote in
  the shell to prevent splitting on whitespace.
- `--page <PAGE_ID>` — optional flag carrying a buildin page id. When
  provided, restricts results to descendants of the named page (FR-004).

`Spectre.Console.Cli` settings class:

```text
public sealed class Settings : CommandSettings
{
    [CommandArgument(0, "<query>")]
    public string Query { get; init; } = string.Empty;

    [CommandOption("--page <PAGE_ID>")]
    public string? PageId { get; init; }
}
```

## Behavioural contract

### Inputs

- `Query` — passed straight to `ISearchService.SearchAsync` after a
  trim-and-non-empty validation in the command. The trim is for the
  validation only; the original (non-trimmed) string is what the
  service receives, matching the user's intent (a query with leading
  whitespace is a deliberate one).
- `PageId` — passed straight to `ISearchService.SearchAsync` as the
  `pageId` argument when non-null. Format validation is delegated to
  the buildin client (consistent with feature 002).

### Output streams

| Stream | When | What |
|---|---|---|
| stdout | Successful search, plain mode (non-TTY) | `ISearchResultFormatter.Format(...)` body verbatim. Bytes are exactly the formatter's output. Empty body when zero matches. |
| stdout | Successful search, styled mode (TTY) | `Spectre.Console.Table` rendering of the same body, plus ANSI styling. For zero matches, a single styled `No matches.` line. |
| stderr | Any failure (validation, buildin error) | Single styled error line via `IAnsiConsole.MarkupLine($"[red]<class>:[/] <message>")` |

### Exit codes

| Code | Meaning | Trigger |
|---|---|---|
| `0` | Success | Including zero-match results |
| `2` | Invalid arguments | `Query` is empty or whitespace |
| `3` | Page not found | `BuildinApiException` with `ApiError { StatusCode: 404 }` raised by ancestor walk for `--page`'s id |
| `4` | Authentication / authorisation failure | `BuildinApiException` with `StatusCode: 401` or `403` |
| `5` | Transport failure | `BuildinApiException` with `Error is TransportError` |
| `6` | Unexpected error | Any other `BuildinApiException` |

Codes 3 / 4 / 5 / 6 match `GetCommand` from feature 002 verbatim.
Code 2 is new for `SearchCommand` and is the documented "invalid
arguments" code; `GetCommand` does not currently use it.

### Behaviour pseudocode

```text
async Task<int> ExecuteAsync(ctx, settings, ct):
    if string.IsNullOrWhiteSpace(settings.Query):
        _console.MarkupLine("[red]Query must be non-empty.[/]")  // stderr-routed
        return 2
    try:
        var matches = await _service.SearchAsync(settings.Query, settings.PageId, ct)
        var body = _formatter.Format(matches)
        if _caps.IsStyledStdout:
            _styledRenderer.Render(body, _console)
        else:
            await Console.Out.WriteAsync(body)
        return 0
    catch BuildinApiException ex when ex.Error is ApiError { StatusCode: 404 }:
        _console.MarkupLine($"[red]Page not found:[/] {settings.PageId}")
        return 3
    catch BuildinApiException ex when ex.Error is ApiError { StatusCode: 401 or 403 }:
        _console.MarkupLine($"[red]Authentication failure:[/] {ex.Message}")
        return 4
    catch BuildinApiException ex when ex.Error is TransportError:
        _console.MarkupLine($"[red]Transport failure:[/] {ex.Message}")
        return 5
    catch BuildinApiException ex:
        _console.MarkupLine($"[red]Unexpected buildin error:[/] {ex.Message}")
        return 6
    catch ArgumentException:
        // Defensive — should be caught by the up-front validation.
        _console.MarkupLine("[red]Query must be non-empty.[/]")
        return 2
```

### TTY detection

Reuses `TerminalCapabilities` from feature 002 unchanged. `IsStyledStdout`
is `true` iff the underlying terminal advertises ANSI capability AND
stdout is not redirected AND `NO_COLOR` is unset. Tests inject a
`TerminalCapabilities` instance with explicit booleans (matching the
pattern in `GetCommandTests`).

### Error routing

The CLI routes error markup through `IAnsiConsole`. In tests, a
`TestConsole` captures the markup; in production, Spectre's
`AnsiConsole.Console` writes to stderr by default for `MarkupLine` only
when explicitly told via `Console.Error`. The command MUST write
errors to stderr in production. This is achieved by using
`AnsiConsole.Console.Profile.Out = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) })`
at registration time? — **simpler**: register a *separate*
`IAnsiConsole` instance for stderr (`AnsiConsole.Create(new
AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) })`)
and inject that as the error console; rename the field to `_errConsole`.

The error-routing mechanism details are an implementation choice; the
contract is "errors go to stderr", verified by
`SearchCommandTests.Errors_AreRoutedToStderr`. (Feature 002's
`GetCommand` writes errors to a `MarkupLine` against the console; this
feature can match its routing exactly. If feature 002's choice is
later corrected to route to stderr explicitly, this command picks up
the same fix.)

## Test obligations

Integration tests live at
`tests/Buildout.IntegrationTests/Cli/SearchCommandTests.cs`:

| Test | Asserts |
|---|---|
| `HappyPath_NonTty_StdoutIsFormatterBody` | Plain mode: stdout equals `ISearchResultFormatter.Format(matches)` byte-for-byte |
| `HappyPath_Tty_StdoutContainsAnsiAndTitles` | Styled mode: stdout contains an ANSI escape AND each title is present |
| `NoMatches_NonTty_StdoutIsEmpty` | Plain mode + empty result list → stdout is `""` |
| `NoMatches_Tty_StdoutShowsNoMatchesLine` | Styled mode + empty result list → stdout contains `No matches.` |
| `EmptyQuery_ReturnsExit2` | Empty positional argument exits 2 with stderr message |
| `WhitespaceQuery_ReturnsExit2` | `"   "` exits 2 |
| `Scope_NotFound_ReturnsExit3` | `--page <missing>` where ancestor walk's `GetPageAsync` raises 404 → exit 3 |
| `AuthFailure_ReturnsExit4` | Substituted `SearchPagesAsync` throws 401 → exit 4 |
| `Forbidden_ReturnsExit4` | 403 → exit 4 |
| `TransportFailure_ReturnsExit5` | `TransportError` → exit 5 |
| `UnexpectedError_ReturnsExit6` | `UnknownError` → exit 6 |
| `PageOption_PassedToService` | When `--page` is set, service receives non-null `pageId` arg |
| `Plain_OutputBytes_EqualMcpToolResultBytes` | Same fixture in CLI plain mode and MCP `search` tool produces identical bytes |

The byte-equality test (last row) is the SC-003 assertion in CLI form;
its mirror in MCP form lives in `SearchToolTests`.

## DI registration

`Buildout.Cli/Program.cs` is extended:

```text
config.AddCommand<SearchCommand>("search");
```

inside the existing `app.Configure(...)` block. The DI services for
`ISearchService` and `ISearchResultFormatter` are picked up
automatically via the existing `AddBuildoutCore()` call (which is
extended in this feature to register the search seams — see
`contracts/search-service.md` § DI).

A second `IAnsiConsole` for stderr (if introduced — see Error routing
above) is registered as a keyed service, with `SearchCommand` resolving
the keyed instance.
