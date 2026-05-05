using System.ComponentModel;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Markdown;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Resources;

[McpServerResourceType]
public sealed class PageResourceHandler
{
    private readonly IPageMarkdownRenderer _renderer;
    private readonly ILogger<PageResourceHandler> _logger;

    public PageResourceHandler(IPageMarkdownRenderer renderer, ILogger<PageResourceHandler> logger)
    {
        _renderer = renderer;
        _logger = logger;
    }

    [McpServerResource(UriTemplate = "buildin://{pageId}", Name = "buildin-page", MimeType = "text/markdown")]
    [Description("Returns the rendered Markdown content of a Buildin page")]
    public async Task<TextResourceContents> GetPageMarkdownAsync(
        string pageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var markdown = await _renderer.RenderAsync(pageId, cancellationToken).ConfigureAwait(false);
            return new TextResourceContents
            {
                Uri = $"buildin://{pageId}",
                MimeType = "text/markdown",
                Text = markdown
            };
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 404 })
        {
            throw new McpProtocolException($"Page not found: {pageId}", McpErrorCode.ResourceNotFound);
        }
        catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
        {
            throw new McpProtocolException($"Authentication error: {ex.Message}", McpErrorCode.InternalError);
        }
        catch (BuildinApiException ex) when (ex.Error is TransportError)
        {
            throw new McpProtocolException($"Transport error: {ex.Message}", McpErrorCode.InternalError);
        }
    }
}
