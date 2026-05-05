using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Markdown;

public sealed class PageMarkdownRenderer : IPageMarkdownRenderer
{
    private readonly IBuildinClient _client;
    private readonly BlockToMarkdownRegistry _registry;
    private readonly IInlineRenderer _inlineRenderer;

    public PageMarkdownRenderer(
        IBuildinClient client,
        BlockToMarkdownRegistry registry,
        IInlineRenderer inlineRenderer)
    {
        _client = client;
        _registry = registry;
        _inlineRenderer = inlineRenderer;
    }

    public async Task<string> RenderAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var page = await _client.GetPageAsync(pageId, cancellationToken).ConfigureAwait(false);
        var roots = await FetchChildrenAsync(pageId, cancellationToken).ConfigureAwait(false);

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

        return writer.ToString();
    }

    private async Task<List<BlockSubtree>> FetchChildrenAsync(string parentId, CancellationToken ct)
    {
        var result = new List<BlockSubtree>();
        string? cursor = null;

        do
        {
            var query = cursor is not null
                ? new BlockChildrenQuery { StartCursor = cursor }
                : null;

            var page = await _client
                .GetBlockChildrenAsync(parentId, query, ct)
                .ConfigureAwait(false);

            foreach (var block in page.Results)
            {
                List<BlockSubtree>? children = null;

                if (block.HasChildren)
                {
                    var converter = _registry.Resolve(block);
                    if (converter is { RecurseChildren: true })
                        children = await FetchChildrenAsync(block.Id, ct).ConfigureAwait(false);
                }

                result.Add(new BlockSubtree
                {
                    Block = block,
                    Children = children ?? []
                });
            }

            cursor = page.HasMore ? page.NextCursor : null;
        }
        while (cursor is not null);

        return result;
    }
}
