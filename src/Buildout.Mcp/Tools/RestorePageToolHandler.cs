using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.PageLifecycle;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Tools;

[McpServerToolType]
public sealed class RestorePageToolHandler
{
    private readonly IPageLifecycle _lifecycle;

    public RestorePageToolHandler(IPageLifecycle lifecycle)
    {
        _lifecycle = lifecycle;
    }

    [McpServerTool(Name = "restore_page")]
    [Description("Restore (un-archive) a previously archived buildin page. The page becomes visible to normal browse and search again. Use this tool to undo a previous `delete_page` call.")]
#pragma warning disable CA1707
    public async Task<CallToolResult> RestorePageAsync(
        [Description("Buildin page id to restore.")] string page_id,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var outcome = await _lifecycle.RestoreAsync(page_id, cancellationToken);

            _ = outcome.FailureClass switch
            {
                FailureClass.NotFound => throw new McpProtocolException(
                    $"Page '{page_id}' was not found.", McpErrorCode.ResourceNotFound),
                FailureClass.Auth => throw new McpProtocolException(
                    $"Authentication error: {outcome.UnderlyingException?.Message}", McpErrorCode.InternalError),
                FailureClass.Transport => throw new McpProtocolException(
                    $"Transport error: {outcome.UnderlyingException?.Message}", McpErrorCode.InternalError),
                FailureClass.Unexpected => throw new McpProtocolException(
                    $"Unexpected error: {outcome.UnderlyingException?.Message}", McpErrorCode.InternalError),
                _ => (object?)null,
            };

            var result = new CallToolResult
            {
                IsError = false,
                Content =
                [
                    new ResourceLinkBlock
                    {
                        Uri = $"buildin://{outcome.PageId}",
                        Name = outcome.PageId,
                    },
                    new TextContentBlock
                    {
                        Text = JsonSerializer.Serialize(new
                        {
                            page_id = outcome.PageId,
                            archived = outcome.Archived,
                            changed = outcome.Changed,
                        }),
                    },
                ],
            };

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "restore_page" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "restore_page" }, { "outcome", "success" } });
            return result;
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "restore_page" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "restore_page" }, { "outcome", "failure" } });
            throw;
        }
    }
}
