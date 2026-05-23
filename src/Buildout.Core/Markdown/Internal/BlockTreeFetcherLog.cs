using Microsoft.Extensions.Logging;

namespace Buildout.Core.Markdown.Internal;

internal static partial class BlockTreeFetcherLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "FetchChildren pagination_page={PageNumber} pagination_items={ItemsCount}")]
    internal static partial void FetchChildrenPage(ILogger logger, int pageNumber, int itemsCount);
}
