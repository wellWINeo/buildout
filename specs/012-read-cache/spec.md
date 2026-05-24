# Feature Specification: In-Memory Read Cache

**Feature Branch**: `012-read-cache`
**Created**: 2026-05-24
**Status**: Draft
**Input**: User description: "Cache for reading to avoid multiple API requests on each read/re-read. In-memory only, no persistence. Eviction algorithms and invalidation need investigation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Repeated Page Reads Skip the API (Priority: P1)

An LLM agent or CLI user reads the same page multiple times during a session (e.g., reviewing a page, then referencing it again). The second and subsequent reads return instantly because the result is served from memory rather than making another round of HTTP calls to buildin.ai.

**Why this priority**: This is the core value proposition. Every read path (CLI `get`, MCP tool, MCP resource) currently makes 1+N API calls per invocation. Eliminating redundant calls is the entire point of the feature.

**Independent Test**: Read a page twice in the same process. Observe that the first read triggers API calls and the second returns without any network activity. Delivers measurable latency reduction.

**Acceptance Scenarios**:

1. **Given** a page has been read once, **When** the same page is read again in the same process, **Then** the second read returns the same content without making any HTTP requests to buildin.ai.
2. **Given** a page has never been read, **When** it is read for the first time, **Then** the system fetches it from buildin.ai normally and stores the result in memory.
3. **Given** the process restarts, **When** a page that was read in the previous process lifetime is requested, **Then** it is fetched fresh from buildin.ai (no persistence).

---

### User Story 2 - Write Operations Invalidate Stale Data (Priority: P2)

A user reads a page, then updates it (via the edit-in-place workflow or a page update command). Subsequent reads of that page must reflect the updated content, not the stale cached version.

**Why this priority**: Serving stale data after a write would be a correctness bug. This must work correctly from day one, but it depends on having a working cache first (P1).

**Independent Test**: Read a page, update it, read it again. The final read must return the updated content, not the pre-edit cached version.

**Acceptance Scenarios**:

1. **Given** a page is cached, **When** the page is updated through any write operation, **Then** the cache entry for that page is removed.
2. **Given** a page was updated and its cache entry removed, **When** the page is read again, **Then** the fresh content is fetched from buildin.ai and re-cached.
3. **Given** page A is cached and page B is updated, **When** page A is read, **Then** page A's cached content is returned (no false invalidation of unrelated pages).

---

### User Story 3 - Cache Evicts Old Entries When Full (Priority: P3)

Over a long session, the cache accumulates entries. When the cache reaches its configured capacity, the least recently used entries are evicted to make room for new ones, keeping memory usage bounded.

**Why this priority**: Without eviction, the cache grows without bound. This matters for long-running MCP server sessions but is less critical than basic caching (P1) or correctness (P2).

**Independent Test**: Configure the cache with a small capacity (e.g., 3 entries), read 4 different pages, then re-read the first one. The re-read must trigger a fresh API call because the first entry was evicted.

**Acceptance Scenarios**:

1. **Given** the cache is at capacity, **When** a new page is read, **Then** the least recently used entry is evicted and the new entry is stored.
2. **Given** the cache is at capacity, **When** an already-cached page is re-read, **Then** it is served from cache (no eviction occurs, recency is updated).
3. **Given** the cache is disabled via the `Enabled` toggle, **When** any page is read, **Then** every read goes directly to the buildin.ai API.

---

### Edge Cases

- What happens when a buildin.ai API call fails mid-fetch (e.g., partial block tree)? The cache MUST NOT store incomplete or error results.
- What happens when the same page is read concurrently by two callers? The cache MUST NOT serve corrupted or partially-populated data. At most one in-flight fetch per page ID should occur.
- What happens when a page ID is valid for `GetPageAsync` but returns no blocks? The cache MUST store the empty result as a valid cache entry to avoid re-fetching.
- What happens when a delete/restore operation affects a cached page? The cache MUST invalidate the entry just like a write operation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST cache the results of page read operations (`GetPageAsync` and `GetBlockChildrenAsync`) in memory so that repeated reads of the same page do not require additional API calls. Search, database views, and user lookups MUST NOT be cached.
- **FR-002**: The cache MUST NOT persist to disk, database, or any external store. Cache contents are lost when the process exits.
- **FR-003**: The system MUST invalidate the cache entry for a page whenever that page is modified, deleted, or restored through any write operation.
- **FR-004**: The system MUST use a least-recently-used (LRU) eviction policy when the cache reaches its configured maximum number of entries.
- **FR-005**: The cache MUST NOT store error results, failed responses, or incomplete data. Only successful, complete responses are eligible for caching.
- **FR-006**: The cache MUST be configurable through both the JSON configuration file and the `Buildout__` environment variable, per the dual-channel configuration discipline. Configuration MUST include a boolean `Enabled` toggle (disables caching when `false`, regardless of capacity) and a `MaxEntries` value controlling the maximum number of cached pages.
- **FR-007**: The cache MUST be transparent to callers. The existing read interfaces (`IPageMarkdownRenderer`, `IPageEditor`) MUST NOT change their signatures. Caching is an internal implementation detail layered above the block tree fetching logic — the cache stores the fully-assembled page + block tree, not individual API call results.
- **FR-008**: The cache MUST be scoped to the core library so that both CLI and MCP presentations benefit without either needing cache-aware code.
- **FR-009**: The cache MUST handle concurrent access safely. Multiple simultaneous reads of the same page should result in at most one API fetch, not duplicate requests.
- **FR-010**: The system MUST provide observability for cache behavior (cache hits, misses, evictions) through the existing metrics infrastructure so operators can verify the cache is effective.

### Key Entities

- **Cache Entry**: An in-memory record keyed by page ID, containing the fully-assembled result (page metadata + complete block tree). Includes access tracking for LRU eviction ordering. The block tree is stored fully resolved — no partial or per-block entries.
- **Cache Configuration**: User-configurable settings: a boolean `Enabled` toggle to disable caching entirely, and a `MaxEntries` value for the maximum number of cached pages. Bound via the standard options pattern.
- **Cache Statistics**: Counters for hits, misses, and evictions, emitted through the existing metrics pipeline.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A second read of the same page within the same process completes at least 10x faster than the first read (network latency eliminated).
- **SC-002**: After a page is updated, the next read returns content reflecting the update (zero stale-read incidents).
- **SC-003**: Memory usage remains bounded at the configured capacity regardless of how many distinct pages are read over time.
- **SC-004**: Cache behavior (hit rate, eviction count) is observable in the metrics output, allowing operators to tune the cache size.

## Clarifications

### Session 2026-05-24

- Q: Should the cache cover all read operations on `IBuildinClient` (search, database views, users) or only page reads? → A: Pages only — cache only `GetPageAsync` and `GetBlockChildrenAsync`. Other reads always hit the API.
- Q: How is caching disabled — single control (MaxEntries=0) or separate boolean toggle? → A: Two controls — boolean `Enabled` toggle plus `MaxEntries`. `Enabled=false` disables caching regardless of capacity.
- Q: Should block children be cached as part of the page entry or independently by block ID? → A: Page-level entry — one cache entry per page ID containing the assembled page metadata + full block tree.

## Assumptions

- LRU is the appropriate eviction algorithm for this workload. Buildout's read pattern is dominated by repeated access to a working set of recently-viewed pages, which matches LRU's strength.
- Cache invalidation on write is sufficient. There is no need for time-based expiration (TTL) in v1 because the only source of truth for page changes is write operations made through Buildout itself. External edits made directly in buildin.ai are out of scope.
- The cache operates at the API response level (page metadata + block children), not at the rendered Markdown level. This allows the cache to benefit both the plain-read and edit-in-place paths, and avoids re-rendering being hidden behind a cache that could mask rendering bugs.
- The maximum entry count (not byte size) is the right capacity metric. Page sizes vary, but counting entries is simpler, more predictable, and sufficient for an in-memory cache in a tool that reads one page at a time.
- The cache lives in `Buildout.Core`, layered above `BlockTreeFetcher` and the page-assembly logic. It caches the fully-assembled page metadata + block tree as a single entry keyed by page ID. Only `GetPageAsync` and `GetBlockChildrenAsync` results participate (search, database views, and users pass through uncached). This aligns with Principle I (core/presentation separation) and Principle V (API abstraction), keeping the cache invisible to presentation layers.
- Caching is enabled by default with a sensible maximum entry count. Operators can disable it via the `Enabled` configuration toggle.
