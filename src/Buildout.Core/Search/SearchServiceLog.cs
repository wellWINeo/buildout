using Microsoft.Extensions.Logging;

namespace Buildout.Core.Search;

internal static partial class SearchServiceLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "SearchAsync pagination_page={PageNumber} pagination_items={ItemCount}")]
    internal static partial void SearchPage(ILogger<SearchService> logger, int pageNumber, int itemCount);
}
