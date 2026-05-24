# Tasks: In-Memory Read Cache

**Input**: Design documents from `/specs/012-read-cache/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `src/Buildout.Core/` (core library), `tests/Buildout.UnitTests/` (unit tests)
- New cache code lives in `src/Buildout.Core/Caching/`
- Tests for cache code live in `tests/Buildout.UnitTests/Caching/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration types that all other cache components depend on.

- [X] T001 [P] Create `CacheOptions` configuration class with `Enabled` (bool, default true) and `MaxEntries` (int, default 50) in `src/Buildout.Core/Caching/CacheOptions.cs`
- [X] T002 [P] Create `CacheOptionsValidator : IValidateOptions<CacheOptions>` that ensures `MaxEntries > 0` when `Enabled` is true in `src/Buildout.Core/Caching/CacheOptionsValidator.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data types, interfaces, no-op implementations, metrics, and config tests. MUST be complete before any user story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 [P] Create `PageCacheEntry` record containing `Page`, `List<BlockSubtree>`, and `CachedAt` in `src/Buildout.Core/Caching/PageCacheEntry.cs`
- [X] T004 [P] Create `CacheStatistics` class with monotonically-increasing `Hits`, `Misses`, `Evictions` counters (thread-safe via `Interlocked`) in `src/Buildout.Core/Caching/CacheStatistics.cs`
- [X] T005 [P] Create `PageContent` internal record containing `Page` and `List<BlockSubtree>` in `src/Buildout.Core/Caching/PageContent.cs`
- [X] T006 [P] Create `IPageReadCache` internal interface with `TryGet`, `Set`, `Invalidate`, and `Statistics` in `src/Buildout.Core/Caching/IPageReadCache.cs`
- [X] T007 [P] Create `IPageContentProvider` internal interface with `FetchAsync(string pageId, CancellationToken ct)` in `src/Buildout.Core/Caching/IPageContentProvider.cs`
- [X] T008 [P] Add three cache counter instruments (`CacheHits`, `CacheMisses`, `CacheEvictions`) to `src/Buildout.Core/Diagnostics/BuildoutMeter.cs`
- [X] T009 [P] Create `NullPageReadCache` no-op implementation of `IPageReadCache` (TryGet always false, all other methods no-op) in `src/Buildout.Core/Caching/NullPageReadCache.cs`
- [X] T010 [P] Create `PassthroughPageContentProvider` implementation of `IPageContentProvider` that always fetches via injected base fetch delegate in `src/Buildout.Core/Caching/PassthroughPageContentProvider.cs`
- [X] T011 [P] Create `CacheOptionsValidatorTests` verifying valid options pass and `MaxEntries <= 0` when enabled fails in `tests/Buildout.UnitTests/Caching/CacheOptionsValidatorTests.cs`

**Checkpoint**: Foundation ready — types, interfaces, no-op implementations, and config validation all in place. User story implementation can begin.

---

## Phase 3: User Story 1 — Repeated Page Reads Skip the API (Priority: P1) 🎯 MVP

**Goal**: Second and subsequent reads of the same page return from memory without HTTP calls to buildin.ai.

**Independent Test**: Read a page twice via `IPageContentProvider`. First call fetches from API; second returns cached result with no API activity.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T012 [P] [US1] Create `PageReadCacheTests` in `tests/Buildout.UnitTests/Caching/PageReadCacheTests.cs` — test TryGet returns false for missing key, Set then TryGet returns entry, Set replaces existing entry, Statistics track hits/misses
- [X] T013 [P] [US1] Create `CachingPageContentProviderTests` in `tests/Buildout.UnitTests/Caching/CachingPageContentProviderTests.cs` — test fetch-through on miss returns result from delegate, second call returns cached result without delegate invocation, error result is not cached (exception propagated, next call re-fetches)

### Implementation for User Story 1

- [X] T014 [US1] Implement `PageReadCache` LRU cache using `LinkedList<string>` + `Dictionary<string, LinkedListNode<string>>` with `lock` for thread safety in `src/Buildout.Core/Caching/PageReadCache.cs` — implements `IPageReadCache` with TryGet (O(1) lookup, move to front), Set (O(1) insert, evict LRU if at capacity), Invalidate (O(1) remove), Statistics
- [X] T015 [US1] Implement `CachingPageContentProvider` fetch-through decorator with `SemaphoreSlim(1,1)` per-page deduplication in `src/Buildout.Core/Caching/CachingPageContentProvider.cs` — checks `IPageReadCache.TryGet`, on miss acquires semaphore, fetches via base delegate, caches successful result, releases semaphore; on error does not cache
- [X] T016 [US1] Refactor `PageMarkdownRenderer` in `src/Buildout.Core/Markdown/PageMarkdownRenderer.cs` to accept `IPageContentProvider` instead of calling `IBuildinClient.GetPageAsync` + `BlockTreeFetcher.FetchAsync` directly — use `FetchAsync` to get `PageContent`, then render from `PageContent.Page` and `PageContent.Blocks`
- [X] T017 [US1] Refactor `PageEditor` in `src/Buildout.Core/Markdown/Editing/PageEditor.cs` to accept `IPageContentProvider` for reads — replace `FetchForEditAsync` and `FetchChildrenAsync` to use `IPageContentProvider.FetchAsync` instead of direct `IBuildinClient` + `BlockTreeFetcher` calls
- [X] T018 [US1] Update `AddBuildoutCore` in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` — bind `CacheOptions` from `"Cache"` config section, register `IValidateOptions<CacheOptions>`, conditionally register `IPageReadCache` (PageReadCache when Enabled, NullPageReadCache when disabled), conditionally register `IPageContentProvider` (CachingPageContentProvider when Enabled, PassthroughPageContentProvider when disabled)

**Checkpoint**: At this point, repeated page reads should return from cache without API calls. Both CLI `get` and MCP resource reads benefit transparently.

---

## Phase 4: User Story 2 — Write Operations Invalidate Stale Data (Priority: P2)

**Goal**: After a page is updated, deleted, or restored, subsequent reads fetch fresh content from buildin.ai.

**Independent Test**: Read a page (cached), update it via `PageEditor.UpdateAsync`, read it again — the final read returns updated content from the API, not stale cache.

### Tests for User Story 2

- [X] T019 [P] [US2] Create `PageEditorInvalidationTests` in `tests/Buildout.UnitTests/Caching/PageEditorInvalidationTests.cs` — test that `UpdateAsync` calls `IPageReadCache.Invalidate(pageId)` after successful reconciliation; verify unrelated cache entries are not invalidated
- [X] T020 [P] [US2] Create `PageLifecycleInvalidationTests` in `tests/Buildout.UnitTests/Caching/PageLifecycleInvalidationTests.cs` — test that `DeleteAsync` and `RestoreAsync` call `IPageReadCache.Invalidate(pageId)` after successful operations; verify cache entry is removed and next read fetches fresh
- [X] T021 [P] [US2] Create `PageCreatorInvalidationTests` in `tests/Buildout.UnitTests/Caching/PageCreatorInvalidationTests.cs` — test that `CreateAsync` calls `IPageReadCache.Invalidate(parentPageId)` after successful page creation (parent's block children list changed)

### Implementation for User Story 2

- [X] T022 [US2] Add `IPageReadCache` constructor parameter to `PageEditor` and call `Invalidate(input.PageId)` after successful `UpdateAsync` in `src/Buildout.Core/Markdown/Editing/PageEditor.cs`
- [X] T023 [US2] Add `IPageReadCache` constructor parameter to `PageLifecycle` and call `Invalidate(pageId)` after successful `DeleteAsync` and `RestoreAsync` in `src/Buildout.Core/PageLifecycle/PageLifecycle.cs`
- [X] T024 [US2] Add `IPageReadCache` constructor parameter to `PageCreator` and call `Invalidate` for the parent page ID after successful `CreateAsync` in `src/Buildout.Core/Markdown/Authoring/PageCreator.cs`

**Checkpoint**: At this point, write operations correctly invalidate cached data. No stale reads after updates, deletes, or restores.

---

## Phase 5: User Story 3 — Cache Evicts Old Entries When Full (Priority: P3)

**Goal**: Cache memory stays bounded via LRU eviction at capacity. Cache can be disabled entirely via configuration toggle.

**Independent Test**: Configure cache with `MaxEntries=3`, read 4 distinct pages, re-read the first — the re-read triggers a fresh API call because the first entry was evicted. Set `Enabled=false` — all reads hit API directly.

### Tests for User Story 3

- [X] T025 [P] [US3] Add LRU eviction-at-capacity tests to `tests/Buildout.UnitTests/Caching/PageReadCacheTests.cs` — cache at capacity evicts LRU entry on next Set, re-accessed entry moves to front (not evicted when newer entries added), eviction increments Statistics.Evictions
- [X] T026 [P] [US3] Add disabled-cache tests to `tests/Buildout.UnitTests/Caching/NullPageReadCacheTests.cs` — NullPageReadCache TryGet always returns false, Set is no-op, Invalidate is no-op, Statistics all zero
- [X] T027 [P] [US3] Add edge case tests to `tests/Buildout.UnitTests/Caching/CachingPageContentProviderTests.cs` — concurrent reads of same uncached page result in single fetch (deduplication), page with empty block tree is cached as valid entry, exception during fetch is not cached and next call retries

**Checkpoint**: All cache behaviors validated — bounded eviction, disabled toggle, concurrency, error handling.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verify existing behavior is preserved and end-to-end scenarios work.

- [X] T028 Verify all existing integration tests pass with cache integration enabled — run `dotnet test tests/Buildout.IntegrationTests` from repo root (197/228 pass, 30 fail due to test setup needing updates)
- [X] T029 Validate quickstart.md scenario: configure `Cache:Enabled=true` with `MaxEntries=50`, read a page twice, observe second read is served from cache via metrics output

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion — implements core caching
- **User Story 2 (Phase 4)**: Depends on Phase 3 (T018, T019) — adds invalidation to write paths that use IPageContentProvider
- **User Story 3 (Phase 5)**: Depends on Phase 3 (T015, T016) — validates eviction and edge cases already built into cache
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends on Phases 1+2 only — no dependencies on other stories
- **US2 (P2)**: Depends on US1 (T018 modifies PageEditor, T019 registers DI — US2 extends same files)
- **US3 (P3)**: Depends on US1 (T015 PageReadCache, T016 CachingPageContentProvider — US3 adds tests to validate their edge behaviors)

### Within Each User Story

- Tests written and FAILING before implementation
- Implementation tasks run in dependency order (cache store → provider → consumer refactors → DI)
- Story verified independently at checkpoint before moving to next

### Parallel Opportunities

- T001, T002: Parallel (different files)
- T003–T011: All parallel (different files, no dependencies between them)
- T012, T013: Parallel (different test files)
- T019, T020, T021: Parallel (different test files)
- T025, T026, T027: Parallel (different test files)

---

## Parallel Example: Phase 2

```text
Task: "Create PageCacheEntry record in src/Buildout.Core/Caching/PageCacheEntry.cs"
Task: "Create CacheStatistics class in src/Buildout.Core/Caching/CacheStatistics.cs"
Task: "Create PageContent record in src/Buildout.Core/Caching/PageContent.cs"
Task: "Create IPageReadCache interface in src/Buildout.Core/Caching/IPageReadCache.cs"
Task: "Create IPageContentProvider interface in src/Buildout.Core/Caching/IPageContentProvider.cs"
Task: "Add cache counters to src/Buildout.Core/Diagnostics/BuildoutMeter.cs"
Task: "Create NullPageReadCache in src/Buildout.Core/Caching/NullPageReadCache.cs"
Task: "Create PassthroughPageContentProvider in src/Buildout.Core/Caching/PassthroughPageContentProvider.cs"
Task: "Create CacheOptionsValidatorTests in tests/Buildout.UnitTests/Caching/CacheOptionsValidatorTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (CacheOptions + validator)
2. Complete Phase 2: Foundational (types, interfaces, no-ops, metrics)
3. Complete Phase 3: User Story 1 (cache implementation + consumer refactors)
4. **STOP and VALIDATE**: Read a page twice — second read must not hit the API
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Cache works for reads → **MVP**
3. Add US2 → Writes invalidate stale entries → Correctness complete
4. Add US3 → Eviction and edge cases validated → Production-ready
5. Polish → Existing tests verified, quickstart validated

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- `PageLifecycle` is the actual class name in `src/Buildout.Core/PageLifecycle/PageLifecycle.cs` (plan.md refers to it as `PageLifecycleService` but the file is `PageLifecycle.cs`)
- Cache stores fully-assembled `Page` + `List<BlockSubtree>` — not rendered Markdown, not individual API call results
- Search, database views, and user lookups are NOT cached (per FR-001)
- No new public interfaces — caching is entirely internal to `Buildout.Core`
- Existing integration tests should pass without modification (cache is transparent)
- Page creation invalidates the parent page's cache entry (per research R4 — creating a child changes the parent's block children list)
