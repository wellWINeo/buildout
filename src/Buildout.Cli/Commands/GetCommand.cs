using System.Text.Json;
using Buildout.Cli.Rendering;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Editing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class GetCommand : AsyncCommand<GetCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<page_id>")]
        public string PageId { get; init; } = string.Empty;

        [CommandOption("--editing")]
        public bool Editing { get; init; }

        [CommandOption("--print")]
        public string PrintMode { get; init; } = "markdown";
    }

    private readonly IPageMarkdownRenderer _renderer;
    private readonly IPageEditor _pageEditor;
    private readonly IAnsiConsole _console;
    private readonly TerminalCapabilities _caps;
    private readonly MarkdownTerminalRenderer _terminalRenderer;

    public GetCommand(
        IPageMarkdownRenderer renderer,
        IPageEditor pageEditor,
        IAnsiConsole console,
        TerminalCapabilities caps,
        MarkdownTerminalRenderer terminalRenderer)
    {
        _renderer = renderer;
        _pageEditor = pageEditor;
        _console = console;
        _caps = caps;
        _terminalRenderer = terminalRenderer;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            if (settings.PrintMode == "json" && !settings.Editing)
            {
                await Console.Error.WriteLineAsync("—print json requires --editing");
                return 2;
            }

            if (settings.Editing)
            {
                var snapshot = await _pageEditor.FetchForEditAsync(settings.PageId, cancellationToken);

                if (settings.PrintMode == "json")
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        markdown = snapshot.Markdown,
                        revision = snapshot.Revision,
                        unknown_block_ids = snapshot.UnknownBlockIds
                    }, JsonOptions);

                    Console.WriteLine(json);
                }
                else
                {
                    Console.WriteLine(snapshot.Markdown);
                    await Console.Error.WriteLineAsync($"revision: {snapshot.Revision}");
                    foreach (var id in snapshot.UnknownBlockIds)
                    {
                        await Console.Error.WriteLineAsync($"unknown_block_id: {id}");
                    }
                }

                return 0;
            }

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
