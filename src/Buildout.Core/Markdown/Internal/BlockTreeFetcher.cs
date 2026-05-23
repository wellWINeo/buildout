using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Markdown.Internal;

internal static class BlockTreeFetcher
{
    internal static async Task<List<BlockSubtree>> FetchAsync(
        IBuildinClient client,
        BlockToMarkdownRegistry registry,
        string parentId,
        ILogger logger,
        CancellationToken ct)
    {
        var result = new List<BlockSubtree>();
        string? cursor = null;
        var pageNumber = 1;

        do
        {
            var query = cursor is not null
                ? new BlockChildrenQuery { StartCursor = cursor }
                : null;

            var page = await client
                .GetBlockChildrenAsync(parentId, query, ct)
                .ConfigureAwait(false);

            BlockTreeFetcherLog.FetchChildrenPage(logger, pageNumber, page.Results.Count);

            foreach (var block in page.Results)
            {
                List<BlockSubtree>? children = null;

                if (block.HasChildren)
                {
                    var converter = registry.Resolve(block);
                    if (converter is { RecurseChildren: true })
                        children = await FetchAsync(client, registry, block.Id, logger, ct).ConfigureAwait(false);
                }

                result.Add(new BlockSubtree
                {
                    Block = block,
                    Children = children ?? []
                });
            }

            cursor = page.HasMore ? page.NextCursor : null;
            pageNumber++;
        }
        while (cursor is not null);

        return result;
    }

}
