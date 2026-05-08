using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Markdown;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class GetCommand : AsyncCommand<GetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<page_id>")]
        public string PageId { get; init; } = string.Empty;
    }

    private readonly IPageMarkdownRenderer _renderer;
    private readonly IAnsiConsole _console;
    private readonly TerminalCapabilities _caps;
    private readonly MarkdownTerminalRenderer _terminalRenderer;

    public GetCommand(
        IPageMarkdownRenderer renderer,
        IAnsiConsole console,
        TerminalCapabilities caps,
        MarkdownTerminalRenderer terminalRenderer)
    {
        _renderer = renderer;
        _console = console;
        _caps = caps;
        _terminalRenderer = terminalRenderer;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var markdown = await _renderer.RenderAsync(settings.PageId, cancellationToken);

            if (_caps.IsStyledStdout)
            {
                _terminalRenderer.Render(markdown);
            }
            else
            {
                _console.Write(new Text(markdown));
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
    }
}
