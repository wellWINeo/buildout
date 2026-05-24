using System.Diagnostics;
using System.Diagnostics.Metrics;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Markdown;

public sealed class PageMarkdownRenderer : IPageMarkdownRenderer
{
    private readonly IPageContentProvider _contentProvider;
    private readonly BlockToMarkdownRegistry _registry;
    private readonly IInlineRenderer _inlineRenderer;
    private readonly ILogger<PageMarkdownRenderer> _logger;

    public PageMarkdownRenderer(
        IPageContentProvider contentProvider,
        BlockToMarkdownRegistry registry,
        IInlineRenderer inlineRenderer,
        ILogger<PageMarkdownRenderer> logger)
    {
        _contentProvider = contentProvider;
        _registry = registry;
        _inlineRenderer = inlineRenderer;
        _logger = logger;
    }

    public async Task<string> RenderAsync(string pageId, CancellationToken cancellationToken = default)
    {
        using var recorder = OperationRecorder.Start(_logger, "page_read");
        try
        {
            var content = await _contentProvider.FetchAsync(pageId, cancellationToken).ConfigureAwait(false);
            var page = content.Page;
            var roots = content.Blocks;

            var totalBlockCount = CountBlocks(roots);
            recorder.SetTag("page_id", pageId);
            recorder.SetTag("block_count", totalBlockCount);

            var writer = new MarkdownWriter();

            if (page.Title is { Count: > 0 })
            {
                var titleText = _inlineRenderer.Render(page.Title, 0);
                writer.WriteLine("# " + titleText);
                writer.WriteBlankLine();
            }

            var ctx = new MarkdownRenderContext(writer, _inlineRenderer, _registry, 0);
            foreach (var subtree in roots)
                ctx.WriteBlockSubtree(subtree);

            recorder.Succeed();
            BuildoutMeter.BlocksProcessedTotal.Add(totalBlockCount, new TagList { { "operation", "page_read" } });

            return writer.ToString();
        }
        catch
        {
            recorder.Fail("unknown");
            throw;
        }
    }

    private static int CountBlocks(IReadOnlyList<BlockSubtree> subtrees)
    {
        var count = 0;
        foreach (var subtree in subtrees)
        {
            count++;
            count += CountBlocks(subtree.Children);
        }
        return count;
    }
}
