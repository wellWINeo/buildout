# Contract: buildin endpoints touched by page creation

**Feature**: [spec.md](../spec.md) · [plan.md](../plan.md)

The exhaustive list of buildin endpoints this feature's code path
issues HTTP requests to. Asserted by the contract test in
`tests/Buildout.IntegrationTests/Cross/CreatePageReadOnlyOnExistingDataTests.cs`
(spec SC-008): across every test in this feature's integration suite,
the only endpoints WireMock should record are these four. Any other
recorded request fails the test.

| Method | Path | When | Source in `openapi.json` |
|---|---|---|---|
| `GET` | `/v1/pages/{page_id}` | Probe step 1 (R5). Identifies whether the supplied parent id is a page. | `paths./v1/pages/{page_id}.get` |
| `GET` | `/v1/databases/{database_id}` | Probe fallback (R5). Identifies whether the supplied parent id is a database (only invoked when the page-probe returned 404). | `paths./v1/databases/{database_id}.get` |
| `POST` | `/v1/pages` | The single create call. `CreatePageRequest` body includes the resolved parent, the properties (title + any database-property values), and up to 100 top-level body blocks under `children`. | `paths./v1/pages.post` |
| `PATCH` | `/v1/blocks/{block_id}/children` | Appends. Repeated up to `⌈(N − 100) / 100⌉` times for trailing top-level body blocks against the new page id, plus one call per child-bearing parent block at each nested level (R4). | `paths./v1/blocks/{block_id}/children.patch` |

No other endpoint is touched. The contract test specifically asserts
that none of these endpoints are touched:

- `PATCH /v1/pages/{page_id}` (update page)
- `PATCH /v1/blocks/{block_id}` (update block)
- `DELETE /v1/blocks/{block_id}` (delete block)
- `PATCH /v1/databases/{database_id}` (update database)
- `POST /v1/databases` (create database)
- `POST /v1/databases/{database_id}/query` (query database)
- `POST /v1/search` (search)
- `POST /v1/pages/search` (search pages)

If a future change to `IPageCreator` introduces any of these, the
contract test breaks and forces an explicit review against Principle
VI.

---

## WireMock stub helpers (`BuildinStubs`)

The integration-test fixture from feature 004 gains the following
helpers (added in `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs`):

- `RegisterPageProbe(string pageId, Page response)` — `GET
  /v1/pages/{pageId}` returns 200 + `response`. The complementary
  `RegisterPageProbeNotFound(string pageId)` returns 404.
- `RegisterDatabaseProbe(string databaseId, Database response)` —
  same for databases.
- `RegisterCreatePage(Func<CreatePageRequest, Page> respond)` — `POST
  /v1/pages`. The lambda lets a test assert on the request body and
  shape the returned `Page` (in particular the new id).
- `RegisterAppendBlockChildren(string parentBlockId, Func<AppendBlockChildrenRequest, AppendBlockChildrenResult> respond)` —
  `PATCH /v1/blocks/{parentBlockId}/children`.
- `RegisterAppendBlockChildrenFailure(string parentBlockId, int statusCode)` —
  injects a mid-stream failure for the partial-creation test.

The stubs return `IRequestRecorder` handles so tests can assert
exactly *which* requests fired in *what order* — the basis of the
contract test.
