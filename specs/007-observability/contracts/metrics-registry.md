# Metrics Registry: Observability — Logs & Metrics

**Feature**: 007-observability
**Date**: 2026-05-14

This document is the single source of truth for all metric names, types, labels, and units. Any code emitting or consuming these metrics MUST reference this registry.

## Meter

| Field | Value |
|-------|-------|
| Name | `Buildout` |
| Version | `1.0.0` |

## Business Operations

### buildout.operations.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{operation}` |
| Description | Total number of buildout operations executed |
| Spec ref | FR-006 |

| Label | Type | Values |
|-------|------|--------|
| `operation` | `string` | `page_read`, `search`, `page_create`, `database_view` |
| `outcome` | `string` | `success`, `failure` |

### buildout.operation.duration

| Field | Value |
|-------|-------|
| Type | `Histogram<double>` |
| Unit | `s` |
| Description | Duration of buildout operations |
| Histogram buckets | Default OTel (explicit bounds: 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10) |
| Spec ref | FR-007 |

| Label | Type | Values |
|-------|------|--------|
| `operation` | `string` | `page_read`, `search`, `page_create`, `database_view` |
| `outcome` | `string` | `success`, `failure` |

### buildout.blocks.processed.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{block}` |
| Description | Total number of blocks read or written |
| Spec ref | FR-010 |

| Label | Type | Values |
|-------|------|--------|
| `operation` | `string` | `page_read`, `page_create` |

### buildout.search.results.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{result}` |
| Description | Total number of search results returned |
| Spec ref | FR-011 |

(No labels — global counter)

### buildout.pages.created.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{page}` |
| Description | Total number of pages created by parent type |
| Spec ref | FR-012 |

| Label | Type | Values |
|-------|------|--------|
| `parent_kind` | `string` | `page`, `database` |

### buildout.database.view.renders.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{render}` |
| Description | Total number of database view renders by style |
| Spec ref | FR-013 |

| Label | Type | Values |
|-------|------|--------|
| `style` | `string` | `table`, `board`, `gallery`, `list`, `calendar`, `timeline` |

## Buildin API Client

### buildout.api.calls.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{call}` |
| Description | Total number of buildin API calls |
| Spec ref | FR-008 |

| Label | Type | Values |
|-------|------|--------|
| `method` | `string` | `GetPageAsync`, `CreatePageAsync`, `SearchPagesAsync`, `GetBlockChildrenAsync`, `AppendBlockChildrenAsync`, `GetDatabaseAsync`, `QueryDatabaseAsync`, + all 16 IBuildinClient methods |
| `outcome` | `string` | `success`, `failure` |
| `error_type` | `string` | `transport`, `api`, `unknown`, `` (empty for success) |

### buildout.api.call.duration

| Field | Value |
|-------|-------|
| Type | `Histogram<double>` |
| Unit | `s` |
| Description | Duration of buildin API calls |
| Histogram buckets | Same as operation.duration |
| Spec ref | FR-009 |

| Label | Type | Values |
|-------|------|--------|
| `method` | `string` | Same as api.calls.total |
| `outcome` | `string` | `success`, `failure` |

## MCP Surface

### buildout.mcp.tool.invocations.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{invocation}` |
| Description | Total MCP tool invocations |
| Spec ref | FR-014 |

| Label | Type | Values |
|-------|------|--------|
| `tool` | `string` | `search`, `database_view`, `create_page` |
| `outcome` | `string` | `success`, `failure` |

### buildout.mcp.tool.duration

| Field | Value |
|-------|-------|
| Type | `Histogram<double>` |
| Unit | `s` |
| Description | Duration of MCP tool invocations |
| Spec ref | FR-015 |

| Label | Type | Values |
|-------|------|--------|
| `tool` | `string` | `search`, `database_view`, `create_page` |
| `outcome` | `string` | `success`, `failure` |

### buildout.mcp.resource.reads.total

| Field | Value |
|-------|-------|
| Type | `Counter<long>` |
| Unit | `{read}` |
| Description | Total MCP resource reads |
| Spec ref | FR-016 |

| Label | Type | Values |
|-------|------|--------|
| `outcome` | `string` | `success`, `failure` |

## Technical / Infrastructure (Built-in)

These metrics are provided by OpenTelemetry instrumentations — no custom instruments needed.

### http.client.duration

| Field | Value |
|-------|-------|
| Source | `OpenTelemetry.Instrumentation.HttpClient` |
| Spec ref | FR-017 |

Labels include `http.method`, `http.status_code`, `server.address`, `network.protocol.version`.

### process.runtime.dotnet.*

| Field | Value |
|-------|-------|
| Source | `OpenTelemetry.Instrumentation.Runtime` |
| Spec ref | FR-018 |

Includes: `process.runtime.dotnet.gc.heap.memory`, `process.runtime.dotnet.threadpool.queue.length`, `process.runtime.dotnet.alloc.rate`, etc.
