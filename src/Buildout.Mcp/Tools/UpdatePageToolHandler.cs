using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Editing;
using Buildout.Core.Markdown.Editing.PatchOperations;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Tools;

[McpServerToolType]
public sealed class UpdatePageToolHandler
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new PatchOperationJsonConverter());
        return options;
    }

    private readonly IPageEditor _editor;

    public UpdatePageToolHandler(IPageEditor editor)
    {
        _editor = editor;
    }

    [McpServerTool(Name = "update_page")]
    [Description("DESTRUCTIVE. Apply patch operations to an existing buildin page. " +
                 "Always call get_page_markdown first to obtain the revision token. " +
                 "Supply the revision token from that call to prevent overwriting concurrent edits. " +
                 "Use dry_run=true to preview the reconciliation before committing. " +
                 "Failure modes: patch.stale_revision (re-fetch and retry), " +
                 "patch.ambiguous_match (make old_str unique), " +
                 "patch.no_match (check old_str), " +
                 "patch.unknown_anchor (anchor not in snapshot), " +
                 "patch.section_anchor_not_heading (use replace_block instead), " +
                 "patch.anchor_not_container (use insert_after_block instead), " +
                 "patch.reorder_not_supported (delete + re-insert at new position), " +
                 "patch.unsupported_block_touched (avoid altering opaque placeholders), " +
                 "patch.large_delete (set allow_large_delete=true to acknowledge).")]
#pragma warning disable CA1707
    public async Task<string> UpdatePageAsync(
        [Description("The buildin page ID")] string page_id,
        [Description("Revision token from get_page_markdown")] string revision,
        [Description("JSON array of patch operations")] string operations,
        [Description("Preview without committing")] bool dry_run = false,
        [Description("Bypass large-delete guard")] bool allow_large_delete = false,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var sw = Stopwatch.StartNew();
        try
        {
            PatchOperation[] parsedOperations;
            try
            {
                parsedOperations = JsonSerializer.Deserialize<PatchOperation[]>(operations, JsonOptions)
                    ?? throw new McpProtocolException("operations must be a non-empty JSON array", McpErrorCode.InvalidParams);
            }
            catch (JsonException ex)
            {
                throw new McpProtocolException($"Invalid operations JSON: {ex.Message}", McpErrorCode.InvalidParams);
            }

            if (parsedOperations.Length == 0)
                throw new McpProtocolException("operations must be a non-empty JSON array", McpErrorCode.InvalidParams);

            ReconciliationSummary summary;
            try
            {
                summary = await _editor.UpdateAsync(new UpdatePageInput
                {
                    PageId = page_id,
                    Revision = revision,
                    Operations = parsedOperations,
                    DryRun = dry_run,
                    AllowLargeDelete = allow_large_delete,
                }, cancellationToken);
            }
            catch (PartialPatchException ex)
            {
                throw new McpProtocolException($"Patch partially applied: {ex.Message}", McpErrorCode.InternalError);
            }
            catch (PatchRejectedException ex)
            {
                throw new McpProtocolException($"Patch rejected ({ex.PatchErrorClass}): {ex.Message}", McpErrorCode.InvalidParams);
            }
            catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
            {
                throw new McpProtocolException($"Page not found: {page_id}", McpErrorCode.InvalidParams);
            }
            catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
            {
                throw new McpProtocolException($"Authentication error: {ex.Message}", McpErrorCode.InternalError);
            }
            catch (BuildinApiException ex) when (ex.Error is TransportError)
            {
                throw new McpProtocolException($"Transport error: {ex.Message}", McpErrorCode.InternalError);
            }
            catch (BuildinApiException ex)
            {
                throw new McpProtocolException($"Unexpected buildin error: {ex.Message}", McpErrorCode.InternalError);
            }

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "update_page" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "update_page" }, { "outcome", "success" } });

            return JsonSerializer.Serialize(summary);
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "update_page" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "update_page" }, { "outcome", "failure" } });
            throw;
        }
    }
}
