using System.Text.Json;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.PageLifecycle;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class RestoreCommand : AsyncCommand<RestoreSettings>
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IPageLifecycle _lifecycle;

    public RestoreCommand(IPageLifecycle lifecycle)
    {
        _lifecycle = lifecycle;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, RestoreSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.PageId))
        {
            await Console.Error.WriteLineAsync("Page ID is required.");
            return 2;
        }

        var outcome = await _lifecycle.RestoreAsync(settings.PageId, cancellationToken);

        if (outcome.FailureClass is not null)
        {
            switch (outcome.FailureClass)
            {
                case FailureClass.NotFound:
                    Console.Error.WriteLine($"Restore failed [NotFound]: Page {settings.PageId} not found.");
                    return 3;
                case FailureClass.Auth:
                    Console.Error.WriteLine($"Restore failed [Auth]: {outcome.UnderlyingException?.Message}");
                    return 4;
                case FailureClass.Transport:
                    Console.Error.WriteLine($"Restore failed [Transport]: {outcome.UnderlyingException?.Message}");
                    return 5;
                default:
                    Console.Error.WriteLine($"Restore failed [Unexpected]: {outcome.UnderlyingException?.Message}");
                    return 6;
            }
        }

        var printMode = settings.PrintMode.ToLowerInvariant();
        if (printMode == "json")
        {
            Console.WriteLine(JsonSerializer.Serialize(outcome, OutputJsonOptions));
        }
        else
        {
            var archivedStr = outcome.Archived?.ToString().ToLowerInvariant() ?? "null";
            var changedStr = outcome.Changed.ToString().ToLowerInvariant();
            if (outcome.Changed)
            {
                Console.WriteLine($"Restored page {settings.PageId}: archived={archivedStr} (changed={changedStr})");
            }
            else
            {
                Console.WriteLine($"Restored page {settings.PageId}: archived={archivedStr} (changed={changedStr}, no-op)");
            }
        }

        return 0;
    }
}
