# Contract: Core API Routing

## Boundary

`IBuildinClient` remains the only client interface consumed by Buildout domain
services and presentation layers. Generated V2 types never cross this boundary,
and no production V1 client or route remains.

Hand-maintained trash fields use `InTrash` and map directly to V2 `in_trash`.
`DeleteBlockAsync` remains the semantic destructive operation and routes to V2
block DELETE. Callers never select an API version or route.

## Operation routing table

| Domain operation | Production method/path | Required scope | Route rule |
|------------------|------------------------|----------------|------------|
| `GetMeAsync` | `GET /v2/users/me` | valid token; no extra scope | V2 only |
| `CreatePageAsync` | `POST /v2/pages` | `pages.write` | V2 only; one `Idempotency-Key` per logical invocation |
| versioned `GetPageAsync` | `GET /v2/pages/{page_id}` | `pages.read` | V2 only; preserve response `ETag` for later writes |
| `GetBlockAsync` | `GET /v2/blocks/{block_id}` | `blocks.read` | V2 only |
| `UpdateBlockAsync` | `PATCH /v2/blocks/{block_id}` | `blocks.write` | V2 only |
| `DeleteBlockAsync` | `DELETE /v2/blocks/{block_id}` | `blocks.write` | V2 only |
| `GetBlockChildrenAsync` | `GET /v2/blocks/{block_id}/children` | `blocks.read` | V2 only |
| `AppendBlockChildrenAsync` | `PATCH /v2/blocks/{block_id}/children` | `blocks.write` | V2 only |
| `CreateDatabaseAsync` | `POST /v2/databases` | `databases.write` | V2 only |
| `GetDatabaseAsync` | `GET /v2/databases/{database_id}` | `databases.read` | V2 only |
| `UpdateDatabaseAsync` | `PATCH /v2/databases/{database_id}` | `databases.write` | V2 only |
| `QueryDatabaseAsync` | `POST /v2/databases/{database_id}/query` | `databases.read` | V2 only |
| `SearchAsync` / `SearchPagesAsync` | `POST /v2/search` | `search.read` | Same V2 route, stable domain projections |

No production request may use `/v1`.

## Removed page lifecycle surface

The following surfaces and their supporting core slice do not exist after feature
016:

- CLI `delete` and `restore`
- MCP `delete_page` and `restore_page`
- `IPageLifecycle`, `PageLifecycle`, and `PageLifecycleOutcome`
- the hand-maintained page-update request/facade method used only by that service
- lifecycle-specific skills, prompts, registrations, and tests

CLI discovery rejects the removed commands, MCP discovery omits the removed tools,
and neither path causes a Buildin request. Page DELETE and page-update `in_trash`
are not supplied by the overlay or exposed as replacement public functionality.

## Native request-safety headers

- `POST /v2/pages` receives a new opaque `Idempotency-Key` for each logical
  create. At most one retry after `429` or a transient transport failure reuses
  the exact key and body, honors cancellation/`Retry-After`, and a new invocation
  uses a new key. There is no local idempotency store.
- `GET /v2/pages/{page_id}` preserves the response `ETag` when the caller needs a
  block-edit preflight.
- No retained current Buildout endpoint receives `If-Match`. Page PATCH publishes
  it but has no retained caller; block PATCH, child append, and block DELETE do not
  publish it.

The existing CLI/MCP revision string therefore becomes a service-issued ETag.
Before block reconciliation, Buildout performs a fresh page read and rejects a
mismatched ETag before the first block write. The following block calls are not
atomic and may still produce the established partial-patch failure if a later call
fails; the contract does not invent a no-op page PATCH or undocumented block header.

## Request rules

- Protected requests carry exactly one bearer `Authorization` header.
- The token is absent from URL, query, body, logs, metrics, exceptions, and test
  fixtures/snapshots.
- The configured base URL applies to all routes. Authentication is not forwarded
  when a request resolves to a different host.
- UUID-shaped resource identifiers are validated before dispatch where required.
- Cursors are opaque and forwarded exactly; page size is 1 through 100 inclusive.
- Cancellation tokens flow to every HTTP call and pagination loop.
- Writes receive no automatic retry except at most one page-create retry protected
  by the same `Idempotency-Key` and identical body. A 409 remains visible; 429
  retry guidance is preserved even when no retry is performed.

## Response rules

- Generated DTOs are translated immediately to existing Buildout domain models.
- V2 `in_trash` is exposed as `InTrash` in hand-maintained models and outputs.
- List results preserve service order and cursor metadata.
- Unknown optional fields remain processable; unknown block variants follow the
  existing unsupported-block policy and are never silently omitted.
- Page/database search unions do not change established search output or order.

## Route enforcement

Tests inspect the mock server journal after representative workflows:

- Every request path begins `/v2/`; any `/v1/` request fails the suite.
- CLI/MCP discovery contains no page delete/restore surfaces, their supporting
  core service is absent, and invoking the former CLI names sends no HTTP request.
- Page-create retry attempts use the same non-empty `Idempotency-Key` and
  byte-equivalent body; independent creates use different keys.
- Editing reads return the page ETag as the existing revision value, no local
  revision hash is computed, and block writes never receive undocumented headers.
- Mixed page edits use V2 reads/updates/appends and V2 block DELETE only for
  explicit removed block IDs.
- A failed block DELETE remains visible; no success is synthesized locally.
