using System.Text;
using Buildout.Core.Markdown.Authoring;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class CreateCommand : AsyncCommand<CreateSettings>
{
    private readonly IPageCreator _creator;
    private readonly IAnsiConsole _console;

    public CreateCommand(IPageCreator creator, IAnsiConsole console)
    {
        _creator = creator;
        _console = console;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings, CancellationToken cancellationToken)
    {
        var markdown = await ResolveSource(settings.MarkdownSource, cancellationToken);

        var properties = ParseProperties(settings.Properties);

        var printMode = settings.PrintMode.ToLowerInvariant() switch
        {
            "json" => CreatePagePrintMode.Json,
            "none" => CreatePagePrintMode.None,
            _ => CreatePagePrintMode.Id
        };

        var input = new CreatePageInput
        {
            ParentId = settings.ParentId,
            Markdown = markdown,
            Title = settings.Title,
            Icon = settings.Icon,
            CoverUrl = settings.CoverUrl,
            Properties = properties.Count > 0 ? properties : null,
            Print = printMode
        };

        var outcome = await _creator.CreateAsync(input, cancellationToken);

        if (outcome.FailureClass is not null)
        {
            return HandleFailure(outcome);
        }

        PrintSuccess(outcome.NewPageId, printMode);
        return 0;
    }

    private static async Task<string> ResolveSource(string source, CancellationToken cancellationToken)
    {
        if (source == "-")
        {
            var sb = new StringBuilder();
            var buffer = new char[8192];
            var total = 0;
            var limit = 16 * 1024 * 1024;

            using var stream = Console.OpenStandardInput();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (total < limit)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0) break;
                total += read;
                sb.Append(buffer, 0, read);
            }

            return sb.ToString();
        }

        return await File.ReadAllTextAsync(source, cancellationToken);
    }

    private static Dictionary<string, string> ParseProperties(string[] raw)
    {
        var dict = new Dictionary<string, string>();
        foreach (var prop in raw)
        {
            var eq = prop.IndexOf('=');
            if (eq > 0)
            {
                dict[prop[..eq]] = prop[(eq + 1)..];
            }
        }
        return dict;
    }

    private static int HandleFailure(CreatePageOutcome outcome)
    {
        var message = outcome.UnderlyingException?.Message ?? "Unknown error.";

        switch (outcome.FailureClass)
        {
            case FailureClass.Validation:
                Console.Error.WriteLine($"Validation error: {message}");
                return 2;
            case FailureClass.NotFound:
                Console.Error.WriteLine($"Not found: {message}");
                return 3;
            case FailureClass.Auth:
                Console.Error.WriteLine($"Authentication failure: {message}");
                return 4;
            case FailureClass.Transport:
                Console.Error.WriteLine($"Transport failure: {message}");
                return 5;
            case FailureClass.Partial:
                Console.Error.WriteLine($"Partial failure: page {outcome.PartialPageId ?? outcome.NewPageId} created but not fully populated. {message}");
                return 6;
            default:
                Console.Error.WriteLine($"Unexpected error: {message}");
                return 6;
        }
    }

    private void PrintSuccess(string pageId, CreatePagePrintMode mode)
    {
        switch (mode)
        {
            case CreatePagePrintMode.Id:
                _console.WriteLine(pageId);
                break;
            case CreatePagePrintMode.Json:
                _console.WriteLine($"{{\"id\":\"{pageId}\",\"uri\":\"buildin://{pageId}\"}}");
                break;
        }
    }
}
