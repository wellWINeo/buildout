using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.DatabaseViews;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class DbViewCommand : AsyncCommand<DbViewSettings>
{
    private readonly IDatabaseViewRenderer _renderer;
    private readonly IAnsiConsole _console;
    private readonly TerminalCapabilities _caps;
    private readonly MarkdownTerminalRenderer _terminalRenderer;

    public DbViewCommand(
        IDatabaseViewRenderer renderer,
        IAnsiConsole console,
        TerminalCapabilities caps,
        MarkdownTerminalRenderer terminalRenderer)
    {
        _renderer = renderer;
        _console = console;
        _caps = caps;
        _terminalRenderer = terminalRenderer;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, DbViewSettings settings, CancellationToken cancellationToken)
    {
        var request = new DatabaseViewRequest(
            settings.DatabaseId,
            settings.Style,
            settings.GroupByProperty,
            settings.DateProperty);

        try
        {
            var rendered = await _renderer.RenderAsync(request, cancellationToken);

            if (_caps.IsStyledStdout && settings.Style == DatabaseViewStyle.Table)
            {
                _terminalRenderer.Render(rendered);
            }
            else
            {
                _console.Write(new Text(rendered));
            }

            return 0;
        }
        catch (DatabaseViewValidationException ex)
        {
            _console.MarkupLine($"[red]Validation error:[/] {ex.Message}");
            return 2;
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            _console.MarkupLine($"[red]Database not found:[/] {settings.DatabaseId}");
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
