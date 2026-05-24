using System.Diagnostics;
using Buildout.Core.Diagnostics;

namespace Buildout.Core.Caching;

/// <summary>
/// Fetch-through provider that caches results.
/// </summary>
public sealed class CachingPageContentProvider : IPageContentProvider
{
    private readonly IPageReadCache _cache;
    private readonly Func<string, CancellationToken, Task<PageContent>> _fetchDelegate;

    public CachingPageContentProvider(
        IPageReadCache cache,
        Func<string, CancellationToken, Task<PageContent>> fetchDelegate)
    {
        _cache = cache;
        _fetchDelegate = fetchDelegate;
    }

    /// <inheritdoc />
    public async Task<PageContent> FetchAsync(string pageId, CancellationToken ct)
    {
        if (_cache.TryGet(pageId, out var entry) && entry != null)
        {
            BuildoutMeter.CacheHitsTotal.Add(1);
            return new PageContent
            {
                Page = entry.Page,
                Blocks = entry.Blocks
            };
        }

        BuildoutMeter.CacheMissesTotal.Add(1);

        try
        {
            var content = await _fetchDelegate(pageId, ct);

            var cacheEntry = new PageCacheEntry
            {
                Page = content.Page,
                Blocks = content.Blocks,
                CachedAt = DateTimeOffset.UtcNow
            };

            _cache.Store(pageId, cacheEntry);

            return content;
        }
        catch
        {
            throw;
        }
    }
}