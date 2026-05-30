using Microsoft.Extensions.Logging;

namespace Buildout.Core.PageTree;

internal static partial class PageTreeServiceLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get block children for page {PageId}")]
    internal static partial void FailedBlockChildren(ILogger<PageTreeService> logger, string pageId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to query database {DatabaseId}")]
    internal static partial void FailedDatabaseQuery(ILogger<PageTreeService> logger, string databaseId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch child page {PageId}")]
    internal static partial void FailedChildPage(ILogger<PageTreeService> logger, string pageId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch child database {DatabaseId}")]
    internal static partial void FailedChildDatabase(ILogger<PageTreeService> logger, string databaseId, Exception ex);
}
