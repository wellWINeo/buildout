# Contracts: In-Memory Read Cache

**Feature**: `012-read-cache` | **Date**: 2026-05-24

This feature introduces **no new public interfaces or external contracts**. The cache is an internal implementation detail of `Buildout.Core`. Existing public interfaces (`IPageMarkdownRenderer`, `IPageEditor`, `IPageLifecycle`, `IPageCreator`) are unchanged in signature and behavior — caching is transparent.

## Internal Contracts

### IPageContentProvider (internal to Buildout.Core)

**Purpose**: Centralized page content fetching with optional caching.

```csharp
namespace Buildout.Core.Caching;

internal record PageContent(Page Page, List<BlockSubtree> Blocks);

internal interface IPageContentProvider
{
    Task<PageContent> FetchAsync(string pageId, CancellationToken ct);
}
```

**Contract**:
- On success: returns `PageContent` with non-null `Page` and non-null `Blocks`.
- On API failure: propagates the original exception. No partial or error result is cached.
- On concurrent calls for the same `pageId`: at most one in-flight fetch; others await the result.

### IPageReadCache (internal to Buildout.Core)

**Purpose**: LRU cache store for page content entries.

```csharp
namespace Buildout.Core.Caching;

internal interface IPageReadCache
{
    bool TryGet(string pageId, [NotNullWhen(true)] out PageCacheEntry? entry);
    void Set(string pageId, PageCacheEntry entry);
    void Invalidate(string pageId);
    CacheStatistics Statistics { get; }
}
```

**Contract**:
- `TryGet`: Returns `true` and sets `entry` if pageId is cached; `false` otherwise. Thread-safe.
- `Set`: Stores or replaces the entry for `pageId`. Evicts LRU entry if at capacity. Thread-safe.
- `Invalidate`: Removes the entry for `pageId` if present. No-op if not present. Thread-safe.
- `Statistics`: Live counters for hits, misses, evictions.

### CacheOptions (configuration contract)

```csharp
namespace Buildout.Core.Caching;

public sealed class CacheOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxEntries { get; set; } = 50;
}
```

**Configuration keys**:

| Config File Key | Env Var | Type | Default |
|---|---|---|---|
| `Cache:Enabled` | `Buildout__Cache__Enabled` | `bool` | `true` |
| `Cache:MaxEntries` | `Buildout__Cache__MaxEntries` | `int` | `50` |

**Validation** (startup, fails fast):
- `MaxEntries` must be > 0 when `Enabled` is `true`.
- Error message: `"Cache:MaxEntries must be greater than 0 (current value: {value}). Either set Cache:Enabled to false or specify a positive MaxEntries."`

### Metrics Contract (BuildoutMeter additions)

| Instrument | Type | Name | Unit | Tags |
|---|---|---|---|---|
| `CacheHits` | `Counter<long>` | `buildout.cache.hits.total` | `{hit}` | — |
| `CacheMisses` | `Counter<long>` | `buildout.cache.misses.total` | `{miss}` | — |
| `CacheEvictions` | `Counter<long>` | `buildout.cache.evictions.total` | `{eviction}` | — |

## Unchanged Public Interfaces

The following interfaces have **no signature changes**. Their implementations are modified to use `IPageContentProvider` internally, but callers see no difference:

- `IPageMarkdownRenderer.RenderAsync(pageId, ct)` — still returns `string` markdown.
- `IPageEditor.FetchForEditAsync(pageId, ct)` — still returns `AnchoredPageSnapshot`.
- `IPageEditor.UpdateAsync(input, ct)` — still returns `ReconciliationSummary`.
- `IPageLifecycle.DeleteAsync(pageId, ct)` — still returns `PageLifecycleOutcome`.
- `IPageLifecycle.RestoreAsync(pageId, ct)` — still returns `PageLifecycleOutcome`.
- `IBuildinClient.*` — all methods unchanged.
