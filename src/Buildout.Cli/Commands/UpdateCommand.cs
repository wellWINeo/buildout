using System.Text;
using System.Text.Json;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Editing.PatchOperations;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class UpdateCommand : AsyncCommand<UpdateSettings>
{
    private static readonly JsonSerializerOptions PatchJsonOptions = CreatePatchJsonOptions();
    private static readonly JsonSerializerOptions OutputJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static JsonSerializerOptions CreatePatchJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new PatchOperationJsonConverter());
        return options;
    }

    private readonly IPageEditor _pageEditor;

    public UpdateCommand(IPageEditor pageEditor)
    {
        _pageEditor = pageEditor;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.PageId))
        {
            await Console.Error.WriteLineAsync("--page is required.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.Revision))
        {
            await Console.Error.WriteLineAsync("--revision is required.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.OpsSource))
        {
            await Console.Error.WriteLineAsync("--ops is required.");
            return 2;
        }

        string opsJson;
        try
        {
            opsJson = await ReadOpsSourceAsync(settings.OpsSource, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            await Console.Error.WriteLineAsync($"Ops file not found: {settings.OpsSource}");
            return 2;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to read ops source: {ex.Message}");
            return 2;
        }

        PatchOperation[]? operations;
        try
        {
            operations = JsonSerializer.Deserialize<PatchOperation[]>(opsJson, PatchJsonOptions);
        }
        catch (JsonException ex)
        {
            await Console.Error.WriteLineAsync($"Invalid ops JSON: {ex.Message}");
            return 2;
        }

        if (operations is null || operations.Length == 0)
        {
            await Console.Error.WriteLineAsync("Ops must be a non-empty JSON array of patch operations.");
            return 2;
        }

        var input = new UpdatePageInput
        {
            PageId = settings.PageId,
            Revision = settings.Revision,
            Operations = operations,
            DryRun = settings.DryRun,
            AllowLargeDelete = settings.AllowLargeDelete,
        };

        try
        {
            var summary = await _pageEditor.UpdateAsync(input, cancellationToken);

            var printMode = settings.PrintMode.ToLowerInvariant();
            if (printMode == "json")
            {
                Console.WriteLine(JsonSerializer.Serialize(summary, OutputJsonOptions));
            }
            else
            {
                var prefix = settings.DryRun ? "[dry-run] " : string.Empty;
                Console.WriteLine(
                    $"{prefix}Reconciled page {settings.PageId}: " +
                    $"{summary.PreservedBlocks} preserved, {summary.UpdatedBlocks} updated, " +
                    $"{summary.NewBlocks} new, {summary.DeletedBlocks} deleted");
                Console.WriteLine($"Revision: {summary.NewRevision}");
            }

            return 0;
        }
        catch (PartialPatchException ex)
        {
            await Console.Error.WriteLineAsync($"Patch rejected [{ex.PatchErrorClass}]: {ex.Message}");
            return 6;
        }
        catch (PatchRejectedException ex)
        {
            await Console.Error.WriteLineAsync($"Patch rejected [{ex.PatchErrorClass}]: {ex.Message}");
            if (ex is StaleRevisionException && ex.Details is not null &&
                ex.Details.TryGetValue("current_revision", out var rev))
            {
                await Console.Error.WriteLineAsync($"Current revision: {rev}");
            }
            return 7;
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            await Console.Error.WriteLineAsync($"Page not found: {settings.PageId}");
            return 3;
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            await Console.Error.WriteLineAsync($"Authentication failure: {ex.Message}");
            return 4;
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            await Console.Error.WriteLineAsync($"Transport failure: {ex.Message}");
            return 5;
        }
        catch (BuildinApiException ex)
        {
            await Console.Error.WriteLineAsync($"Unexpected buildin error: {ex.Message}");
            return 6;
        }
    }

    private static async Task<string> ReadOpsSourceAsync(string source, CancellationToken cancellationToken)
    {
        if (source == "-")
        {
            var sb = new StringBuilder();
            var buffer = new char[8192];
            var total = 0;
            const int limit = 16 * 1024 * 1024;

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
}
