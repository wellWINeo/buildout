using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Search;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class SearchCommand : AsyncCommand<SearchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<query>")]
        public string Query { get; init; } = string.Empty;

        [CommandOption("--page <PAGE_ID>")]
        public string? PageId { get; init; }
    }

    private readonly ISearchService _service;
    private readonly ISearchResultFormatter _formatter;
    private readonly IAnsiConsole _console;
    private readonly TerminalCapabilities _caps;
    private readonly SearchResultStyledRenderer _styledRenderer;

    public SearchCommand(
        ISearchService service,
        ISearchResultFormatter formatter,
        IAnsiConsole console,
        TerminalCapabilities caps,
        SearchResultStyledRenderer styledRenderer)
    {
        _service = service;
        _formatter = formatter;
        _console = console;
        _caps = caps;
        _styledRenderer = styledRenderer;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Query))
        {
            _console.MarkupLine("[red]Query must be non-empty.[/]");
            return 2;
        }

        try
        {
            var matches = await _service.SearchAsync(settings.Query, settings.PageId, cancellationToken);
            var body = _formatter.Format(matches);

            if (_caps.IsStyledStdout)
            {
                _styledRenderer.Render(body);
            }
            else
            {
                _console.Write(new Text(body));
            }

            return 0;
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            _console.MarkupLine($"[red]Page not found:[/] {settings.PageId}");
            return 3;
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            _console.MarkupLine($"[red]Authentication failure:[/] {ex.Message}");
            return 4;
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            _console.MarkupLine($"[red]Transport failure:[/] {ex.Message}");
            return 5;
        }
        catch (BuildinApiException ex)
        {
            _console.MarkupLine($"[red]Unexpected buildin error:[/] {ex.Message}");
            return 6;
        }
        catch (ArgumentException)
        {
            _console.MarkupLine("[red]Query must be non-empty.[/]");
            return 2;
        }
    }
}
