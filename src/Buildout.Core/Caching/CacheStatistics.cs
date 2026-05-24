using System.Threading;

namespace Buildout.Core.Caching;

/// <summary>
/// Thread-safe statistics tracking cache behavior.
/// </summary>
public sealed class CacheStatistics
{
    private long _hits;
    private long _misses;
    private long _evictions;

    /// <summary>
    /// Gets the number of cache hits.
    /// </summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>
    /// Gets the number of entries evicted due to capacity.
    /// </summary>
    public long Evictions => Interlocked.Read(ref _evictions);

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    public void RecordHit()
    {
        Interlocked.Increment(ref _hits);
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    public void RecordMiss()
    {
        Interlocked.Increment(ref _misses);
    }

    /// <summary>
    /// Records a cache eviction.
    /// </summary>
    public void RecordEviction()
    {
        Interlocked.Increment(ref _evictions);
    }
}