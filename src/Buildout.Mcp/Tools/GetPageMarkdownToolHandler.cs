using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Editing;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Tools;

[McpServerToolType]
public sealed class GetPageMarkdownToolHandler
{
    private readonly IPageEditor _editor;

    public GetPageMarkdownToolHandler(IPageEditor editor)
    {
        _editor = editor;
    }

    [McpServerTool(Name = "get_page_markdown")]
    [Description("Fetch a buildin page as anchored Markdown with a revision token. " +
                 "Use this before update_page to obtain the current snapshot and revision. " +
                 "The returned markdown contains <!-- buildin:block:<id> --> comments that " +
                 "anchor each block — include these anchors in patch operations to target " +
                 "specific blocks precisely.")]
#pragma warning disable CA1707
    public async Task<string> GetPageMarkdownAsync(
        [Description("The buildin page ID")] string page_id,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var sw = Stopwatch.StartNew();
        try
        {
            AnchoredPageSnapshot snapshot;
            try
            {
                snapshot = await _editor.FetchForEditAsync(page_id, cancellationToken);
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

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "get_page_markdown" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "get_page_markdown" }, { "outcome", "success" } });

            return JsonSerializer.Serialize(snapshot);
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "get_page_markdown" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "get_page_markdown" }, { "outcome", "failure" } });
            throw;
        }
    }
}
