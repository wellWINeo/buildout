using System.Diagnostics.CodeAnalysis;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Conversion;
using Microsoft.Extensions.Logging;

namespace Buildout.Core.Markdown.Internal;

[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Dynamic operation names prevent compile-time LoggerMessage definitions")]
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

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("FetchChildren pagination_page={PageNumber} pagination_items={ItemsCount}", pageNumber, page.Results.Count);

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
