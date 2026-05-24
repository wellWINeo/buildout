namespace Buildout.Core.Caching;

/// <summary>
/// No-op implementation of <see cref="IPageReadCache"/> used when caching is disabled.
/// </summary>
public sealed class NullPageReadCache : IPageReadCache
{
    private static readonly CacheStatistics EmptyStatistics = new();

    /// <inheritdoc />
    public bool TryGet(string pageId, out PageCacheEntry? entry)
    {
        entry = null;
        return false;
    }

    /// <inheritdoc />
    public void Store(string pageId, PageCacheEntry entry)
    {
    }

    /// <inheritdoc />
    public void Invalidate(string pageId)
    {
    }

    /// <inheritdoc />
    public CacheStatistics Statistics => EmptyStatistics;
}