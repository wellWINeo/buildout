# Quickstart: In-Memory Read Cache

**Feature**: `012-read-cache` | **Date**: 2026-05-24

## Overview

The read cache is enabled by default. No configuration is required to benefit — repeated reads of the same page within a single process lifetime return instantly without API calls.

## Enable / Disable

**JSON configuration** (`~/.config/buildout/config.json`):
```json
{
  "Cache": {
    "Enabled": true,
    "MaxEntries": 50
  }
}
```

**Environment variables**:
```bash
export Buildout__Cache__Enabled="true"
export Buildout__Cache__MaxEntries="50"
```

Set `Enabled` to `false` to disable caching entirely. Every read will hit the buildin.ai API.

## Tuning MaxEntries

The default of 50 entries is sufficient for most sessions. Increase for long-running MCP server sessions that access many distinct pages. Decrease for memory-constrained environments.

Each cached entry holds a page metadata record + its complete block tree. Typical memory per entry: 1–10 KB.

## Observability

Cache behavior is emitted through the existing metrics pipeline (OTel when `Telemetry:Enabled` is `true`):

| Metric | Meaning |
|---|---|
| `buildout.cache.hits.total` | Number of reads served from cache |
| `buildout.cache.misses.total` | Number of reads that required an API fetch |
| `buildout.cache.evictions.total` | Number of entries evicted due to capacity |

A high hit-to-miss ratio indicates effective caching. Frequent evictions suggest `MaxEntries` should be increased.

## What Gets Cached

- Page reads via `IPageMarkdownRenderer.RenderAsync` (MCP resource reads, CLI `get`).
- Page reads via `IPageEditor.FetchForEditAsync` (MCP `get-page-markdown` tool, edit-in-place).

## What Does NOT Get Cached

- Search operations.
- Database view renders.
- User lookups.
- Any write operation (these invalidate the cache for the affected page).

## Cache Lifetime

- **Scope**: In-process only. No disk, no database.
- **Duration**: Process lifetime. Restarting the process starts with an empty cache.
- **Invalidation**: Automatic on any write (update, delete, restore, create) affecting the cached page.
