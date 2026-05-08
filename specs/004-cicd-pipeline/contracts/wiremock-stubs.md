# Contract: WireMock Stubs for Buildin API

This document defines the WireMock stubs that mimic the buildin.ai API
endpoints used by the integration test suite. Each stub is manually
defined in `BuildinStubs.cs` using WireMock.NET's fluent C# API and
validated against the corresponding endpoint in `openapi.json`.

## Endpoints

### `POST /v1/pages/search` — Search Pages

**OpenAPI source**: `openapi.json` → `paths./v1/pages/search.post`

**WireMock stub**:

```csharp
server
    .Given(Request.Create()
        .WithPath("/v1/pages/search")
        .UsingPost()
        .WithHeader("Content-Type", "application/json*"))
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithHeader("Content-Type", "application/json")
        .WithBodyAsJson(searchPagesDefaultResponse));
```

**Default response** (matches `V1PagesSearchResponse` schema):

```json
{
  "object": "list",
  "results": [],
  "has_more": false,
  "next_cursor": null
}
```

**Test overrides**: Individual tests may call
`BuildinStubs.RegisterSearchPages(server, customJson)` to provide
specific result sets with page IDs, titles, and parent metadata.

### `GET /v1/pages/{page_id}` — Get Page

**OpenAPI source**: `openapi.json` → `paths./v1/pages/{page_id}.get`

**WireMock stub**:

```csharp
server
    .Given(Request.Create()
        .WithPath(new RegexMatcher("^/v1/pages/[0-9a-f-]+$"))
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithHeader("Content-Type", "application/json")
        .WithBodyAsJson(getPageDefaultResponse));
```

**Default response** (matches `Page` schema with `properties`):

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "created_time": "2025-01-15T10:30:00Z",
  "last_edited_time": "2025-01-16T14:00:00Z",
  "archived": false,
  "url": "https://api.buildin.ai/pages/00000000",
  "properties": {
    "title": {
      "title": []
    }
  }
}
```

**Path matching**: Regex `^/v1/pages/[0-9a-f-]+$` matches any page UUID.
Tests verify the correct page ID is requested via WireMock's request
journal.

### `GET /v1/blocks/{block_id}/children` — Get Block Children

**OpenAPI source**: `openapi.json` → `paths./v1/blocks/{block_id}/children.get`

**WireMock stub**:

```csharp
server
    .Given(Request.Create()
        .WithPath(new RegexMatcher("^/v1/blocks/[0-9a-f-]+/children$"))
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithHeader("Content-Type", "application/json")
        .WithBodyAsJson(getBlockChildrenDefaultResponse));
```

**Default response** (matches `PaginatedList<Block>` schema):

```json
{
  "object": "list",
  "results": [],
  "has_more": false,
  "next_cursor": null
}
```

### `GET /v1/users/me` — Get Current User

**OpenAPI source**: `openapi.json` → `paths./v1/users/me.get`

**WireMock stub**:

```csharp
server
    .Given(Request.Create()
        .WithPath("/v1/users/me")
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithHeader("Content-Type", "application/json")
        .WithBodyAsJson(getMeDefaultResponse));
```

**Default response** (matches `UserMe` schema):

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Test Bot",
  "avatar_url": "https://example.com/avatar.png",
  "type": "bot",
  "person": { "email": "bot@example.com" }
}
```

## Contract Verification

Each stub's default response is verified by `WireMockContractTests`:

1. Start WireMock server with all stubs registered.
2. Create a `BotBuildinClient` wired to the server.
3. Call each method (`GetMeAsync`, `GetPageAsync`,
   `GetBlockChildrenAsync`, `SearchPagesAsync`).
4. Assert the response deserializes into the hand-written model without
   error — proving the stub matches the Kiota-generated model shape,
   which was generated from `openapi.json`.

If `openapi.json` changes, the contract tests fail, signalling that
stubs need updating.
