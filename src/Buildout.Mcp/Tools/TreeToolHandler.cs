using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Diagnostics;
using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Errors;
using Buildout.Core.PageTree.Rendering;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Tools;

[McpServerToolType]
public sealed class TreeToolHandler
{
    private readonly IPageTreeService _service;
    private readonly IReadOnlyDictionary<TreeFormat, ITreeRenderer> _renderers;

    public TreeToolHandler(
        IPageTreeService service,
        IReadOnlyDictionary<TreeFormat, ITreeRenderer> renderers)
    {
        _service = service;
        _renderers = renderers;
    }

    [McpServerTool(Name = "tree")]
    [Description("Returns a hierarchical map of a buildin page or database and its descendant pages/databases. " +
                 "Supports two format values: 'ascii' (default) for a Unix tree-style rendering with markdown links, " +
                 "and 'json' for a recursive {name, uri, children} object. " +
                 "Accepts a depth of 1–7 (default 3) to control how many levels of descendants to traverse. " +
                 "Skips content blocks (paragraphs, headings, etc.); includes only sub-pages and embedded databases. " +
                 "Uses placeholder names '(untitled)' for empty titles and '(unavailable)' for inaccessible descendants.")]
#pragma warning disable CA1707
    public async Task<string> TreeAsync(
        [Description("UUID of the root page or database.")] string page_id,
        [Description("Output format: 'ascii' or 'json'.")] string format = "ascii",
        [Description("Number of descendant levels to traverse (1–7).")] int depth = 3,
        CancellationToken cancellationToken = default)
#pragma warning restore CA1707
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!Enum.TryParse<TreeFormat>(format, ignoreCase: true, out var treeFormat))
                throw new McpProtocolException($"format must be 'ascii' or 'json'; got '{format}'", McpErrorCode.InvalidParams);

            try
            {
                TreeDepth.Validate(depth);
            }
            catch (TreeDepthOutOfRangeException ex)
            {
                throw new McpProtocolException(ex.Message, McpErrorCode.InvalidParams);
            }

            TreeNode root;
            try
            {
                root = await _service.BuildAsync(page_id, depth, cancellationToken);
            }
            catch (TreeRootNotFoundException ex)
            {
                throw new McpProtocolException(ex.Message, McpErrorCode.ResourceNotFound);
            }
            catch (BuildinApiException ex) when (ex.Error is ApiError { StatusCode: 401 or 403 })
            {
                throw new McpProtocolException($"Authentication error: {ex.Message}", McpErrorCode.InternalError);
            }
            catch (BuildinApiException ex) when (ex.Error is TransportError)
            {
                throw new McpProtocolException($"Transport error: {ex.Message}", McpErrorCode.InternalError);
            }
            catch (TreeCycleDetectedException ex)
            {
                throw new McpProtocolException(ex.Message, McpErrorCode.InternalError);
            }
            catch (BuildinApiException ex)
            {
                throw new McpProtocolException($"Unexpected buildin error: {ex.Message}", McpErrorCode.InternalError);
            }

            var renderer = _renderers[treeFormat];
            var result = renderer.Render(root);

            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "tree" }, { "outcome", "success" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "tree" }, { "outcome", "success" } });

            return result;
        }
        catch (Exception)
        {
            BuildoutMeter.McpToolInvocationsTotal.Add(1, new TagList { { "tool", "tree" }, { "outcome", "failure" } });
            BuildoutMeter.McpToolDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "tool", "tree" }, { "outcome", "failure" } });
            throw;
        }
    }
}
