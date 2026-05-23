using Microsoft.Extensions.Logging;

namespace Buildout.Core.DatabaseViews;

internal static partial class DatabaseViewRendererLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "PaginateRows pagination_page={PageNumber} pagination_items={ItemCount}")]
    internal static partial void PaginateRowsPage(ILogger<DatabaseViewRenderer> logger, int pageNumber, int itemCount);
}
