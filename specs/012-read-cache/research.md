# Research: In-Memory Read Cache

**Feature**: `012-read-cache` | **Date**: 2026-05-24

## R1: Cache Integration Architecture

**Decision**: Introduce an internal `IPageContentProvider` service that centralizes the "fetch page metadata + block tree" operation, with a caching decorator.

**Rationale**: The read path is currently duplicated between `PageMarkdownRenderer` and `PageEditor.FetchForEditAsync` — both call `GetPageAsync` then `BlockTreeFetcher.FetchAsync`. Rather than injecting a raw cache into each consumer, a `IPageContentProvider` abstraction:
1. Eliminates the duplication (DRY).
2. Gives the cache a single interception point for all reads.
3. Keeps `PageMarkdownRenderer` and `PageEditor` focused on rendering/editing, not caching.
4. Makes cache-on/cache-off a DI registration toggle (register the caching decorator or the passthrough).

The provider returns `(Page, List<BlockSubtree>)` — the fully-assembled data — which is exactly what the spec requires as the cache payload.

**Alternatives considered**:
- **Decorator on `IBuildinClient`**: Would cache at the individual API-call level (`GetPageAsync`, `GetBlockChildrenAsync`), but the spec explicitly says "cache stores the fully-assembled page + block tree, not individual API call results." Also doesn't help with the recursive `BlockTreeFetcher` pagination, which makes multiple calls.
- **Direct `IPageReadCache` injection into consumers**: Simpler but scatters cache-awareness across multiple classes. Each consumer must remember to check and invalidate.
- **Decorator on `IPageMarkdownRenderer` only**: Misses the `IPageEditor.FetchForEditAsync` read path, which also benefits from caching.

## R2: LRU Implementation Strategy

**Decision**: Custom LRU using `LinkedList<string>` (page IDs in access order) + `Dictionary<string, LinkedListNode<string>>` (for O(1) node lookup), wrapped in a `lock` for thread safety. Max entries configurable via `CacheOptions.MaxEntries`.

**Rationale**: .NET BCL has no built-in LRU collection. `ConcurrentDictionary` alone doesn't track access order. A `LinkedList` + `Dictionary` combo is the standard O(1) LRU pattern:
- **Get/Set/Invalidate**: All O(1) via dictionary lookup.
- **Eviction**: Remove `LinkedList.Last` (least recently used) when count exceeds `MaxEntries`.
- **Access update**: Move node to `LinkedList.First` on cache hit.
- **Thread safety**: A single `lock` object is simpler and more predictable than trying to compose lock-free structures. The critical section is tiny (dict + list mutation), so contention is negligible for Buildout's workload (single-process, sequential reads from one agent).

**Alternatives considered**:
- `ConcurrentDictionary` alone: No eviction ordering. Would need supplementary tracking.
- `MemoryCache` from `Microsoft.Extensions.Caching.Memory`: Supports eviction and expiration but is designed for TTL-based scenarios, adds a dependency, and doesn't natively support the "recency-only" semantics we need without workarounds.
- Third-party LRU library (e.g., `Caching.CLR`): Adds a dependency for something achievable in ~60 lines. Violates the "few lines of standard-library code" principle.

## R3: Concurrency — Duplicate In-Flight Fetch Prevention

**Decision**: Use a `SemaphoreSlim(1, 1)` per page ID, stored in a `ConcurrentDictionary<string, SemaphoreSlim>`. When a cache miss occurs, the caller acquires the semaphore before fetching, and releases it after caching. A second concurrent reader for the same page ID waits on the semaphore and then finds the cache populated.

**Rationale**: The spec requires "at most one in-flight fetch per page ID." A per-page semaphore ensures this without blocking reads for unrelated pages. The `ConcurrentDictionary` of semaphores is self-cleaning: semaphores for evicted pages are removed during eviction.

**Alternatives considered**:
- **Global lock**: Would serialize all reads, including unrelated pages. Unnecessarily slow.
- **No deduplication**: Two concurrent reads of the same uncached page would both fetch, wasting API calls. Violates FR-009.
- **`GetOrAddAsync` on `ConcurrentDictionary`**: The factory runs without a lock, so two callers can both run the factory concurrently. Not safe.

## R4: Cache Invalidation on Writes

**Decision**: Write services (`PageEditor`, `PageLifecycleService`, `PageCreator`) accept `IPageReadCache` via constructor injection and call `Invalidate(pageId)` after successful write operations. This is the most direct approach — no mediator, no events, no decorator chain.

**Rationale**: All three write services already know which page ID they're operating on. A single `Invalidate(pageId)` call is one line per write path. The cache is internal to `Buildout.Core`, so cross-project coupling is zero.

Specific invalidation points:
- `PageEditor.UpdateAsync`: Invalidate the page being updated after successful reconciliation.
- `PageLifecycleService.DeleteAsync`: Invalidate after successful archive.
- `PageLifecycleService.RestoreAsync`: Invalidate after successful unarchive.
- `PageCreator.CreateAsync`: Invalidate the **parent** page (its block children list changed).

**Alternatives considered**:
- **Decorator on write interfaces (`IPageEditor`, `IPageLifecycle`, `IPageCreator`)**: Cleaner in theory but requires three decorator classes, each wrapping one method. More ceremony than value.
- **Event-based invalidation**: Write services publish an event, cache subscribes. Over-engineered for a synchronous in-process cache with exactly one invalidation trigger per write.
- **Decorator on `IBuildinClient` write methods**: Would intercept at the lowest level, but `CreatePageAsync` invalidates the *parent*, not the created page — the cache key logic lives at a higher level of abstraction.

## R5: Error Filtering — No Caching of Failed Results

**Decision**: `CachingPageContentProvider` only caches the result when the fetch delegate completes successfully (no exception). If `GetPageAsync` or `GetBlockChildrenAsync` throws, the exception propagates and nothing is stored.

**Rationale**: FR-005 requires "only successful, complete responses are eligible for caching." The simplest implementation is: `try { var result = await fetch(); cache.Set(id, result); return result; } catch { /* don't cache */ throw; }`. The provider is the single gatekeeper, so no other code needs to worry about this.

## R6: Configuration Binding

**Decision**: New `CacheOptions` class bound to the `"Cache"` configuration section, following the established pattern used by `BuildinClientOptions`, `LimitationsOptions`, and `TelemetryOptions`.

```csharp
public sealed class CacheOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxEntries { get; set; } = 50;
}
```

Environment variable form: `Buildout__Cache__Enabled`, `Buildout__Cache__MaxEntries`.

Validation rules (via `CacheOptionsValidator : IValidateOptions<CacheOptions>`):
- `MaxEntries` must be > 0 when `Enabled` is true.
- `Enabled` = false with any `MaxEntries` is valid (cache is disabled).

**Rationale**: Follows Principle VII (Dual-Channel Configuration) and the existing options pattern. `MaxEntries = 50` default is conservative — a typical Buildout session touches 10–30 pages, so 50 gives headroom without wasting memory.

**Alternatives considered**:
- **`MaxEntries = 0` to disable**: The spec explicitly requires a separate `Enabled` boolean toggle. Overloading `MaxEntries` violates FR-006.
- **Byte-size capacity**: More precise but much more complex. Page sizes vary unpredictably. Entry counting is sufficient for v1 (spec assumption agrees).

## R7: Metrics / Observability

**Decision**: Add three new instruments to `BuildoutMeter`:

| Instrument | Type | Name | Tags |
|---|---|---|---|
| `CacheHits` | Counter\<long\> | `buildout.cache.hits.total` | — |
| `CacheMisses` | Counter\<long\> | `buildout.cache.misses.total` | — |
| `CacheEvictions` | Counter\<long\> | `buildout.cache.evictions.total` | — |

A `CacheStatistics` class exposes long counters that the cache increments internally. The counters are also emitted through `BuildoutMeter` for OTel collection when telemetry is enabled.

**Rationale**: FR-010 requires observability. Following the existing pattern (`OperationsTotal`, `ApiCallsTotal`, etc.) of counter instruments on the static `BuildoutMeter`.

## R8: DI Registration — Cache-Enabled vs. Passthrough

**Decision**: `ServiceCollectionExtensions.AddBuildoutCore` registers `IPageReadCache` based on `CacheOptions.Enabled`:
- `Enabled = true`: Registers `PageReadCache` (real LRU cache) and `CachingPageContentProvider` (fetch-through decorator).
- `Enabled = false`: Registers `NullPageReadCache` (no-op) and `PassthroughPageContentProvider` (always fetches).

Both `PageMarkdownRenderer` and `PageEditor` are updated to depend on `IPageContentProvider` instead of calling `IBuildinClient` + `BlockTreeFetcher` directly.

**Rationale**: The `Enabled` toggle is architectural — it determines which implementation is registered. This avoids runtime if-checks on every read. `NullPageReadCache` provides the same interface for write invalidation (no-op) so write services don't need `Enabled` checks either.

## R9: Default MaxEntries Value

**Decision**: `MaxEntries = 50`.

**Rationale**: A Buildout session typically works with a working set of 10–30 pages (the agent reads pages, edits some, references others). 50 entries provides comfortable headroom. Each entry is a `Page` record + a `List<BlockSubtree>` — typically 1–10 KB per page — so 50 entries consume roughly 50–500 KB, negligible for a server process. If operators need more, they can tune via config.
