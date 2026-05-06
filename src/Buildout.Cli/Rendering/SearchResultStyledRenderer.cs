using Spectre.Console;

namespace Buildout.Cli.Rendering;

public sealed class SearchResultStyledRenderer
{
    private readonly IAnsiConsole _console;

    public SearchResultStyledRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    public void Render(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            _console.MarkupLine("[dim]No matches.[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Type");
        table.AddColumn("Title");

        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length != 3)
                throw new InvalidOperationException($"Expected 3 tab-separated columns but got {parts.Length}.");
            table.AddRow(parts[0], parts[1], parts[2]);
        }

        _console.Write(table);
    }
}
