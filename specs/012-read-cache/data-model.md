# Data Model: In-Memory Read Cache

**Feature**: `012-read-cache` | **Date**: 2026-05-24

## Entities

### PageCacheEntry

The unit of cached data. Stored keyed by page ID in the LRU cache.

| Field | Type | Description |
|-------|------|-------------|
| `Page` | `Page` | Page metadata (title, ID, archived status, etc.) from `GetPageAsync` |
| `Blocks` | `List<BlockSubtree>` | Fully-resolved block tree from `BlockTreeFetcher.FetchAsync` |
| `CachedAt` | `DateTimeOffset` | Timestamp when the entry was cached (for diagnostics) |

**Key**: `string pageId` — the buildin.ai page GUID used as the argument to `GetPageAsync`.

**Invariants**:
- `Page` is never null (constructor enforced).
- `Blocks` is never null but may be empty (valid: a page with no blocks).
- Entry is only created after successful completion of both `GetPageAsync` and `BlockTreeFetcher.FetchAsync`.

### CacheOptions

Configuration bound to the `"Cache"` section via `Microsoft.Extensions.Options`.

| Field | Type | Default | Validation |
|-------|------|---------|------------|
| `Enabled` | `bool` | `true` | — |
| `MaxEntries` | `int` | `50` | Must be > 0 when `Enabled` is `true` |

**Environment variable mapping**:
- `Buildout__Cache__Enabled` → `CacheOptions.Enabled`
- `Buildout__Cache__MaxEntries` → `CacheOptions.MaxEntries`

**State transitions**:
- Read at startup, validated by `CacheOptionsValidator`, fails fast on invalid config.
- Not re-read at runtime — options are set for the process lifetime.

### CacheStatistics

Mutable counters tracking cache behavior. Exposed through `BuildoutMeter` instruments.

| Field | Type | Description |
|-------|------|-------------|
| `Hits` | `long` | Number of cache lookups that found a valid entry |
| `Misses` | `long` | Number of cache lookups that did not find an entry |
| `Evictions` | `long` | Number of entries removed due to LRU capacity eviction |

**Invariants**:
- Counters are monotonically increasing.
- `Hits + Misses` = total read attempts through the cache.
- `Evictions <= Misses` (every eviction is triggered by a miss that adds a new entry).

## Relationships

```text
CacheOptions ──────configures──────► PageReadCache
                                          │
                                          │ contains 0..MaxEntries of
                                          ▼
                                    PageCacheEntry
                                     ├── Page (from buildin.ai)
                                     └── List<BlockSubtree> (assembled tree)
                                          │
CacheStatistics ◄──tracks── PageReadCache
       │
       └──emits via──► BuildoutMeter (CacheHits, CacheMisses, CacheEvictions)
```

## Internal Interfaces

### IPageContentProvider

Centralizes the "fetch page + block tree" operation. Both `PageMarkdownRenderer` and `PageEditor` use this instead of calling `IBuildinClient` + `BlockTreeFetcher` directly.

```csharp
internal interface IPageContentProvider
{
    Task<PageContent> FetchAsync(string pageId, CancellationToken ct);
}
```

Returns `PageContent` — a record containing the `Page` and `List<BlockSubtree>`.

Implementations:
- `CachingPageContentProvider` — checks `IPageReadCache`, on miss fetches via the base provider, caches result, returns.
- `PassthroughPageContentProvider` — always fetches (cache disabled path).

### IPageReadCache

The raw cache store with LRU eviction.

```csharp
internal interface IPageReadCache
{
    bool TryGet(string pageId, [NotNullWhen(true)] out PageCacheEntry? entry);
    void Set(string pageId, PageCacheEntry entry);
    void Invalidate(string pageId);
    CacheStatistics Statistics { get; }
}
```

Implementations:
- `PageReadCache` — real LRU cache with `LinkedList` + `Dictionary`, thread-safe via `lock`.
- `NullPageReadCache` — all operations are no-ops, `TryGet` always returns false.
