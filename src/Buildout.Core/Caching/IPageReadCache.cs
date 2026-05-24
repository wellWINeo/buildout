namespace Buildout.Core.Caching;

/// <summary>
/// Cache store for page read operations.
/// </summary>
public interface IPageReadCache
{
    /// <summary>
    /// Attempts to get a cached page entry.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="entry">The cached entry, if found.</param>
    /// <returns><c>true</c> if the entry was found; otherwise, <c>false</c>.</returns>
    bool TryGet(string pageId, out PageCacheEntry? entry);

    /// <summary>
    /// Stores a cached page entry.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="entry">The entry to cache.</param>
    void Store(string pageId, PageCacheEntry entry);

    /// <summary>
    /// Invalidates a cached page entry.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    void Invalidate(string pageId);

    /// <summary>
    /// Gets the cache statistics.
    /// </summary>
    CacheStatistics Statistics { get; }
}