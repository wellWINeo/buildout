# Contract: Buildin Endpoints

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

This document is the exhaustive list of buildin API endpoints this feature uses. No endpoint
outside this list may be called by any code path introduced in feature 008. Enforced by
`Buildout.IntegrationTests/Cross/UpdateReadOnlyOnOtherPagesTests.cs` (SC-012).

---

## Read endpoints (FetchForEditAsync)

| Method | Path | `IBuildinClient` method | Notes |
|--------|------|------------------------|-------|
| `GET` | `/v1/pages/{page_id}` | `GetPageAsync` | Fetch page title; already stubbed for `GetCommand` tests |
| `GET` | `/v1/blocks/{block_id}/children` | `GetBlockChildrenAsync` | Paginated; recursive; already stubbed |

These are the same endpoints `PageMarkdownRenderer` already uses. No new WireMock stubs
required for the read path.

---

## Write endpoints (UpdateAsync)

| Method | Path | `IBuildinClient` method | Notes |
|--------|------|------------------------|-------|
| `PATCH` | `/v1/blocks/{block_id}` | `UpdateBlockAsync` | For blocks with changed payloads; **new stub** in `BuildinStubs.RegisterUpdateBlock` |
| `DELETE` | `/v1/blocks/{block_id}` | `DeleteBlockAsync` | For blocks absent from patched tree; **new stub** in `BuildinStubs.RegisterDeleteBlock` |
| `PATCH` | `/v1/blocks/{parent_id}/children` | `AppendBlockChildrenAsync` | For new blocks; already stubbed from feature 006 |

---

## Endpoints this feature MUST NOT call

Any endpoint not listed above. In particular:

| Forbidden endpoint | `IBuildinClient` method | Why |
|--------------------|------------------------|-----|
| `POST /v1/pages` | `CreatePageAsync` | Feature 006 only |
| `PATCH /v1/pages/{id}` | `UpdatePageAsync` | Page properties are out of scope (spec Assumption) |
| `POST /v1/databases` | `CreateDatabaseAsync` | Out of scope |
| `PATCH /v1/databases/{id}` | `UpdateDatabaseAsync` | Out of scope |
| `POST /v1/databases/{id}/query` | `QueryDatabaseAsync` | Out of scope |
| `POST /v1/search` | `SearchAsync` | Out of scope |

The contract test (`UpdateReadOnlyOnOtherPagesTests.cs`) asserts that, across every
integration test in this feature's suite, the WireMock fixture receives no HTTP requests
matching the forbidden patterns above.

---

## WireMock stubs to add

```csharp
// In BuildinStubs.cs:

// Stub UpdateBlockAsync — PATCH /v1/blocks/{id}
public static void RegisterUpdateBlock(WireMockServer server, string blockId, Block updatedBlock)

// Stub DeleteBlockAsync — DELETE /v1/blocks/{id}
public static void RegisterDeleteBlock(WireMockServer server, string blockId)

// Stub for partial-failure scenarios (UpdateBlockAsync returns 500 mid-reconciliation)
public static void RegisterUpdateBlockFailure(WireMockServer server, string blockId, int statusCode)
```

`RegisterAppendBlockChildren` and `RegisterGetBlockChildren` are already present from
features 004 and 006.
