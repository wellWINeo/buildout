namespace Buildout.Core.Caching;

/// <summary>
/// Provider for fetching page content.
/// </summary>
public interface IPageContentProvider
{
    /// <summary>
    /// Fetches the page content (metadata and block tree).
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page content.</returns>
    Task<PageContent> FetchAsync(string pageId, CancellationToken ct);
}