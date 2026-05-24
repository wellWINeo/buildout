using System.Collections.Concurrent;

namespace Buildout.Core.Caching;

/// <summary>
/// LRU cache implementation for page read operations.
/// </summary>
public sealed class PageReadCache : IPageReadCache
{
    private readonly CacheOptions _options;
    private readonly CacheStatistics _statistics;
    private readonly LinkedList<string> _accessOrder;
    private readonly Dictionary<string, LinkedListNode<string>> _cacheMap;
    private readonly Dictionary<string, PageCacheEntry> _entries;
    private readonly object _lock;

    public PageReadCache(CacheOptions options)
    {
        _options = options;
        _statistics = new CacheStatistics();
        _accessOrder = new LinkedList<string>();
        _cacheMap = new Dictionary<string, LinkedListNode<string>>();
        _entries = new Dictionary<string, PageCacheEntry>();
        _lock = new object();
    }

    /// <inheritdoc />
    public bool TryGet(string pageId, out PageCacheEntry? entry)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(pageId, out var node))
            {
                _accessOrder.Remove(node);
                _accessOrder.AddFirst(node);
                _statistics.RecordHit();
                entry = _entries[pageId];
                return true;
            }

            _statistics.RecordMiss();
            entry = null;
            return false;
        }
    }

    /// <inheritdoc />
    public void Store(string pageId, PageCacheEntry entry)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(pageId, out var existingNode))
            {
                _accessOrder.Remove(existingNode);
                _cacheMap.Remove(pageId);
                _entries.Remove(pageId);
            }

            if (_cacheMap.Count >= _options.MaxEntries)
            {
                EvictLeastRecentlyUsed();
            }

            var newNode = _accessOrder.AddFirst(pageId);
            _cacheMap[pageId] = newNode;
            _entries[pageId] = entry;
        }
    }

    /// <inheritdoc />
    public void Invalidate(string pageId)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(pageId, out var node))
            {
                _accessOrder.Remove(node);
                _cacheMap.Remove(pageId);
                _entries.Remove(pageId);
            }
        }
    }

    /// <inheritdoc />
    public CacheStatistics Statistics => _statistics;

    private void EvictLeastRecentlyUsed()
    {
        var lruNode = _accessOrder.Last;
        if (lruNode != null)
        {
            _accessOrder.RemoveLast();
            _cacheMap.Remove(lruNode.Value);
            _entries.Remove(lruNode.Value);
            _statistics.RecordEviction();
        }
    }
}