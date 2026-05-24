# Implementation Plan: In-Memory Read Cache

**Branch**: `012-read-cache` | **Date**: 2026-05-24 | **Spec**: `specs/012-read-cache/spec.md`
**Input**: Feature specification from `/specs/012-read-cache/spec.md`

## Summary

Add an in-memory, LRU-evicted read cache to `Buildout.Core` that stores fully-assembled page metadata + block trees keyed by page ID. The cache eliminates redundant `GetPageAsync` + `GetBlockChildrenAsync` calls when the same page is read multiple times in a single process lifetime. Write operations (update, delete, restore, create) invalidate affected cache entries. The cache is transparent to callers — no interface signatures change — and both CLI and MCP presentations benefit automatically.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK-style csproj), nullable reference types + warnings-as-errors enabled
**Primary Dependencies**: `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`, `System.Collections.Concurrent` (BCL)
**Storage**: In-memory only, `LinkedList` + `Dictionary`-backed with `lock` for thread safety, no persistence
**Testing**: xUnit v3 + NSubstitute 5.3.0
**Target Platform**: Cross-platform (.NET 10 runtime)
**Project Type**: Core library shared by CLI and MCP presentations
**Performance Goals**: 10x latency reduction on cache hit vs. first read (network RTT eliminated)
**Constraints**: Bounded memory via configurable `MaxEntries`; process lifetime only; no disk/external store
**Scale/Scope**: Single process, typically 10s–100s of distinct pages per session

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Core/Presentation Separation | PASS | Cache lives entirely in `Buildout.Core`. Presentations unaware. |
| II. LLM-Friendly Output Fidelity | PASS | Cache stores raw `Page` + `List<BlockSubtree>`, not rendered Markdown. Rendering always runs fresh. |
| III. Bidirectional Round-Trip Testing | PASS | N/A — cache does not alter block↔Markdown conversion. |
| IV. Test-First Discipline | PASS | Unit tests for cache logic (LRU, invalidation, concurrency, error filtering). Integration tests for cache-through read path. |
| V. Buildin API Abstraction | PASS | `IBuildinClient` interface unchanged. Cache is layered above it. |
| VI. Non-Destructive Editing | PASS | N/A — cache does not change write semantics. |
| VII. Dual-Channel Configuration | PASS | `CacheOptions` bound via JSON config + `Buildout__Cache__*` env vars. `IValidateOptions<CacheOptions>` for startup validation. |
| VIII. Skills & Prompts Parity | PASS | No new CLI commands or MCP tools introduced. |

**Violations**: None.

## Project Structure

### Documentation (this feature)

```text
specs/012-read-cache/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── Buildout.Core/
│   ├── Caching/
│   │   ├── CacheOptions.cs                  # Configuration: Enabled, MaxEntries
│   │   ├── CacheOptionsValidator.cs          # IValidateOptions<CacheOptions>
│   │   ├── IPageReadCache.cs                # Internal: TryGet, Set, Invalidate, Statistics
│   │   ├── PageReadCache.cs                 # LRU implementation
│   │   ├── NullPageReadCache.cs             # No-op implementation (cache disabled path)
│   │   ├── IPageContentProvider.cs          # Internal: FetchAsync(pageId, ct)
│   │   ├── CachingPageContentProvider.cs    # Fetch-through decorator: check cache, miss → fetch → cache
│   │   ├── PassthroughPageContentProvider.cs # Always-fetches provider (cache disabled path)
│   │   ├── PageCacheEntry.cs                # Cached (Page, List<BlockSubtree>) record
│   │   ├── PageContent.cs                   # (Page, List<BlockSubtree>) record returned by provider
│   │   └── CacheStatistics.cs               # Hit/miss/eviction counters
│   ├── Buildin/
│   │   └── IBuildinClient.cs                # UNCHANGED
│   ├── Markdown/
│   │   ├── IPageMarkdownRenderer.cs         # UNCHANGED
│   │   ├── PageMarkdownRenderer.cs          # MODIFIED: accepts IPageContentProvider instead of raw client calls
│   │   └── Editing/
│   │       ├── IPageEditor.cs               # UNCHANGED
│   │       └── PageEditor.cs                # MODIFIED: accepts IPageContentProvider for reads; calls cache.Invalidate after writes
│   ├── PageLifecycle/
│   │   ├── IPageLifecycle.cs                # UNCHANGED
│   │   └── PageLifecycle.cs                 # MODIFIED: calls cache.Invalidate after delete/restore
│   ├── Diagnostics/
│   │   └── BuildoutMeter.cs                 # MODIFIED: adds cache hit/miss/eviction counters
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs   # MODIFIED: registers CacheOptions, IPageReadCache, IPageContentProvider
├── Buildout.Configuration/                  # UNCHANGED (configuration loader already dual-channel)
├── Buildout.Cli/                            # UNCHANGED (transparent)
└── Buildout.Mcp/                            # UNCHANGED (transparent)

tests/
├── Buildout.UnitTests/
│   └── Caching/
│       ├── PageReadCacheTests.cs            # LRU eviction, invalidation, concurrency, error filtering
│       ├── CacheOptionsValidatorTests.cs    # Config validation
│       ├── CachingPageContentProviderTests.cs  # Fetch-through logic
│       ├── PageEditorInvalidationTests.cs   # Update invalidation
│       ├── PageLifecycleInvalidationTests.cs   # Delete/restore invalidation
│       ├── PageCreatorInvalidationTests.cs  # Parent invalidation on create
│       └── NullPageReadCacheTests.cs        # No-op implementation tests
└── Buildout.IntegrationTests/               # Existing tests verify cache-through behavior unchanged
```

**Structure Decision**: All cache code lives in a new `Caching/` directory within `Buildout.Core`. Existing files (`PageMarkdownRenderer`, `PageEditor`, `PageLifecycle`, `PageCreator`, `BuildoutMeter`, `ServiceCollectionExtensions`) are modified to integrate the cache. No new projects or presentation-layer changes.

## Complexity Tracking

> No violations — table intentionally empty.
