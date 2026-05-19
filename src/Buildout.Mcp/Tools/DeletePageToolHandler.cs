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
public sealed class DeletePageToolHandler
{
    private readonly IPageLifecycle _lifecycle;

    public DeletePageToolHandler(IPageLifecycle lifecycle)
    {
        _lifecycle = lifecycle;
    }

    [McpServerTool(Name = "delete_page")]
    [Description("Archive (soft-delete) a buildin page. The page becomes hidden from normal browse and search; its blocks, comments, and backlinks are preserved. Reversible via the `restore_page` tool.")]
#pragma warning disable CA1707
    public async Task<CallToolResult> DeletePageAsync(
        [Description("Buildin page id to archive.")] string page_id,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var outcome = await _lifecycle.DeleteAsync(page_id, cancellationToken);

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

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "delete_page" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "delete_page" }, { "outcome", "success" } });
            return result;
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "delete_page" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "delete_page" }, { "outcome", "failure" } });
            throw;
        }
    }
}
