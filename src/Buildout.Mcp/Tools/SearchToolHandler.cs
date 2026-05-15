using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Diagnostics;
using Buildout.Core.Search;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Tools;

[McpServerToolType]
public sealed class SearchToolHandler
{
    private readonly ISearchService _service;
    private readonly ISearchResultFormatter _formatter;
    private readonly ILogger<SearchToolHandler> _logger;

    public SearchToolHandler(ISearchService service, ISearchResultFormatter formatter, ILogger<SearchToolHandler> logger)
    {
        _service = service;
        _formatter = formatter;
        _logger = logger;
    }

    [McpServerTool(Name = "search")]
    [Description("Search buildin pages by query. Returns one match per line, tab-separated: <page_id>\\t<object_type>\\t<title>. Use buildin://<page_id> to read a match.")]
#pragma warning disable CA1707
    public async Task<string> SearchAsync(
        [Description("Non-empty search query.")] string query,
        [Description("Optional buildin page UUID. When set, restricts results to descendants of this page.")] string? page_id = null,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new McpProtocolException("Query must be non-empty.", McpErrorCode.InvalidParams);

            string result;
            try
            {
                var matches = await _service.SearchAsync(query, page_id, cancellationToken);
                result = _formatter.Format(matches);
            }
            catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
            {
                throw new McpProtocolException($"Page not found: {page_id}", McpErrorCode.ResourceNotFound);
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

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "search" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "search" }, { "outcome", "success" } });
            return result;
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "search" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "search" }, { "outcome", "failure" } });
            throw;
        }
    }
}
