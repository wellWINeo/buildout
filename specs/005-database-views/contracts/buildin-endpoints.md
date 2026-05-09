# Contract: Buildin Endpoints Touched

This feature touches **exactly two** buildin endpoints. The
read-only invariant in `core-renderer.md` is enforced by a contract
test that asserts no other endpoint is called by the rendering code
path.

## Endpoints

### `GET /v1/databases/{database_id}`

- **Buildin client method**: `IBuildinClient.GetDatabaseAsync(databaseId, ct)`
  (existing)
- **Returns**: `Database` (existing model)
- **Used by**: `DatabaseViewRenderer` once per render call, before
  any query, to obtain the title and `PropertySchema` map.

### `POST /v1/databases/{database_id}/query`

- **Buildin client method**: `IBuildinClient.QueryDatabaseAsync(databaseId, request, ct)`
  (existing)
- **Returns**: `QueryDatabaseResult` with `Results`, `HasMore`,
  `NextCursor` (existing model)
- **Used by**: `DatabaseViewRenderer` in a loop. The first call
  uses an empty cursor. Each subsequent call uses
  `request with { StartCursor = previous.NextCursor }` while
  `previous.HasMore == true`.

## WireMock stubs (Buildout.IntegrationTests)

New entries in `Buildin/BuildinStubs.cs`:

- `RegisterGetDatabase(WireMockServer, string databaseId, Database body)`
  — stubs `GET /v1/databases/{id}` returning the supplied body.
- `RegisterQueryDatabase(WireMockServer, string databaseId, params QueryDatabaseResult[] pages)`
  — stubs `POST /v1/databases/{id}/query` with cursor matching:
  the first stub matches a request body with no `start_cursor`;
  subsequent stubs match the cursor returned by the preceding
  page. Set the last page's `HasMore = false` and `NextCursor =
  null`.

`RegisterQueryDatabase(server, id, page1, page2)` therefore
encodes a two-page sequence end-to-end without requiring tests to
reason about WireMock matching expressions.

Both stub helpers serialize via the project's existing JSON options
(`JsonSerializerOptions` from the `IBuildinClient` implementation),
ensuring the response shape matches the buildin client's
deserialization contract.

## Contract test (read-only assertion)

`tests/Buildout.IntegrationTests/Cross/DatabaseViewReadOnlyTests.cs`
runs every renderer scenario against a WireMock server with **only**
`GET /v1/databases/{id}` and `POST /v1/databases/{id}/query` stubs
registered (plus the page / block endpoints already required by the
existing page-read tests, since the page-read code path is now also
exercised). Any other endpoint matched returns a 500 response. A
test that completes without producing a 500 demonstrates that no
other endpoint was hit.

This is structurally weaker than asserting on the WireMock request
log directly, but matches the existing
`Buildin/WireMockContractTests.cs` pattern from feature 004 and is
robust to future endpoint additions.

## Page-read code path

When `PageMarkdownRenderer` encounters a `ChildDatabaseBlock`, it
delegates to `ChildDatabaseConverter`, which calls
`IDatabaseViewRenderer.RenderInlineAsync`. That method touches:

- `GET /v1/databases/{embedded_database_id}` — once per block.
- `POST /v1/databases/{embedded_database_id}/query` — looped to
  exhaustion per block, identically to the standalone path.

A page with N `child_database` blocks therefore produces **at
least 2N** buildin reads in addition to the page's own block reads:
exactly N `GET database` calls (one per block) plus at least N
`POST query` calls (one per block, more whenever a block's
embedded database returns `has_more=true` and pagination needs
follow-through).

## Out of scope (will not be touched)

- `POST /v1/databases` (create)
- `PATCH /v1/databases/{id}` (update)
- `POST /v1/pages`, `PATCH /v1/pages/{id}` (page writes)
- `GET /v1/blocks/{id}` and family (block operations)
- `POST /v1/search`, `POST /v1/pages/search` (search)
- `GET /v1/users/me`

If a future enhancement (e.g., resolving a relation property's
target titles inline) requires touching an additional endpoint,
that change requires its own spec and a re-evaluation of the
read-only invariant.
