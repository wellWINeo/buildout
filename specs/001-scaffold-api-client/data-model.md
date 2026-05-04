# Phase 1 Data Model: Scaffold + Buildin API Client

This document captures the entities, types, and state transitions introduced by
this feature. The shapes here are the public surface that downstream features
will consume (via `Buildout.Core`); they are NOT the Kiota-generated types,
which are implementation detail.

## Entities

### `BuildinClientOptions`

POCO bound from configuration via `Microsoft.Extensions.Configuration`.

| Property | Type | Default | Validation | Notes |
|---|---|---|---|---|
| `BaseUrl` | `Uri` | `https://api.buildin.ai/` | MUST be absolute and use `https` (allow `http` only when `UnsafeAllowInsecure == true`). | Overrides the OpenAPI `servers` block at runtime. |
| `BotToken` | `string` | (required) | Non-empty; never logged; redacted in error output. | Loaded from configuration / env (`BUILDOUT__BUILDIN__BOTTOKEN`). |
| `HttpTimeout` | `TimeSpan` | 30 s | `> 0`. | Applied to the underlying `HttpClient`. |
| `UnsafeAllowInsecure` | `bool` | `false` | — | Test-only escape hatch for local mock servers. Production code paths MUST validate this is `false`. |

**Validation rules**:

- Validation runs at DI registration time via
  `IValidateOptions<BuildinClientOptions>`. Failures throw at startup, not at
  first call — fail-fast.
- `BotToken` is treated as a secret throughout the codebase: never logged, never
  included in exception messages, never serialised in test output.

### `IBuildinClient`

The hand-written abstraction. One method per buildin operation declared in
`openapi.json`. Method shapes:

```csharp
Task<TResponse> <Operation>Async(
    <TypedRequestOrPathParams>,
    CancellationToken cancellationToken = default);
```

For operations with both path parameters and a body, both are surfaced as
typed parameters. Methods return `Task<TResponse>` for success-path data;
errors flow through exceptions (see `BuildinError` / `BuildinApiException`
below).

The complete method list for v1 maps 1:1 to `openapi.json` operationIds:

| OperationId | HTTP | Path | Method shape |
|---|---|---|---|
| `getMe` | GET | `/v1/users/me` | `Task<UserMe> GetMeAsync(CancellationToken)` |
| `getPage` | GET | `/v1/pages/{page_id}` | `Task<Page> GetPageAsync(string pageId, CancellationToken)` |
| `createPage` | POST | `/v1/pages` | `Task<Page> CreatePageAsync(CreatePageRequest request, CancellationToken)` |
| `updatePage` | PATCH | `/v1/pages/{page_id}` | `Task<Page> UpdatePageAsync(string pageId, UpdatePageRequest request, CancellationToken)` |
| `getBlock` | GET | `/v1/blocks/{block_id}` | `Task<Block> GetBlockAsync(string blockId, CancellationToken)` |
| `updateBlock` | PATCH | `/v1/blocks/{block_id}` | `Task<Block> UpdateBlockAsync(string blockId, UpdateBlockRequest request, CancellationToken)` |
| `deleteBlock` | DELETE | `/v1/blocks/{block_id}` | `Task DeleteBlockAsync(string blockId, CancellationToken)` |
| `getBlockChildren` | GET | `/v1/blocks/{block_id}/children` | `Task<PaginatedList<Block>> GetBlockChildrenAsync(string blockId, BlockChildrenQuery? query, CancellationToken)` |
| `appendBlockChildren` | PATCH | `/v1/blocks/{block_id}/children` | `Task<AppendBlockChildrenResult> AppendBlockChildrenAsync(string blockId, AppendBlockChildrenRequest request, CancellationToken)` |
| `createDatabase` | POST | `/v1/databases` | `Task<Database> CreateDatabaseAsync(CreateDatabaseRequest request, CancellationToken)` |
| `getDatabase` | GET | `/v1/databases/{database_id}` | `Task<Database> GetDatabaseAsync(string databaseId, CancellationToken)` |
| `updateDatabase` | PATCH | `/v1/databases/{database_id}` | `Task<Database> UpdateDatabaseAsync(string databaseId, UpdateDatabaseRequest request, CancellationToken)` |
| `queryDatabase` | POST | `/v1/databases/{database_id}/query` | `Task<QueryDatabaseResult> QueryDatabaseAsync(string databaseId, QueryDatabaseRequest request, CancellationToken)` |
| `v1Search` | POST | `/v1/search` | `Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken)` |
| `searchPages` | POST | `/v1/pages/search` | `Task<PageSearchResults> SearchPagesAsync(PageSearchRequest request, CancellationToken)` |

Each typed request and response shape lives in `Buildout.Core/Buildin/Models/`
and is hand-written. This insulates callers from Kiota's wrapper-class
treatment of `oneOf`/`anyOf` and from any future regeneration churn (see
`research.md` R2, R12).

### `BotBuildinClient` (concrete)

| Member | Type | Notes |
|---|---|---|
| ctor | `(IRequestAdapter, IOptions<BuildinClientOptions>, ILogger<BotBuildinClient>)` | Test-friendly: substitute `IRequestAdapter` directly. |
| ctor (host-side) | `(HttpClient, IAuthenticationProvider, IOptions<BuildinClientOptions>, ILogger<BotBuildinClient>)` | Production path: builds the Kiota request adapter internally. |
| `IBuildinClient` methods | (as above) | Each translates inputs into Kiota generated calls and outputs back to hand-written models. |

### `BuildinError` / `BuildinApiException`

Buildin client error model. Three categories per spec FR-011:

```csharp
public abstract record BuildinError;
public sealed record TransportError(Exception Cause) : BuildinError;
public sealed record ApiError(int StatusCode, string? Code, string Message, string? RawBody) : BuildinError;
public sealed record UnknownError(int StatusCode, string RawBody) : BuildinError;
```

Methods on `IBuildinClient` expose the error path via a thrown
`BuildinApiException` whose `.Error` property is the discriminated record above.
This balances:

- Strong typing for callers that want to inspect the failure shape.
- Idiomatic .NET ergonomics — async APIs throwing on failure is the default
  expectation in the .NET ecosystem (matches Kiota's own documented behaviour).

State transitions: stateless. Each call is independent.

### `IAuthenticationProvider` (Kiota abstraction reused)

Provided by `Microsoft.Kiota.Abstractions`. We supply a concrete
`BotTokenAuthenticationProvider : BaseBearerTokenAuthenticationProvider` that
reads the token from `BuildinClientOptions.BotToken` at request time. A future
User-API + OAuth implementation supplies a different `IAuthenticationProvider`
to its own concrete `IBuildinClient` impl; the interface is unchanged.

## Domain models (hand-written, in `Buildout.Core/Buildin/Models/`)

These mirror the OpenAPI schemas at the public surface but with idiomatic .NET
shapes:

| Public type | Source schema | Polymorphism translation |
|---|---|---|
| `User` | `User` | flat record |
| `UserMe` | `UserMe` | flat record |
| `Page` | `Page` | flat record |
| `Database` | `Database` | flat record |
| `Block` | `Block` | abstract record + sealed subclasses keyed off `type` field (translated from Kiota's `BlockData` wrapper) |
| `Parent` | `Parent` | abstract record: `ParentDatabase`, `ParentPage`, `ParentBlock`, `ParentSpace` |
| `Icon` | `Icon` | abstract record: `IconEmoji`, `IconExternal`, `IconFile` |
| `PropertyValue` | `PropertyValue` | abstract record + 13 sealed subclasses |
| `PropertySchema` | `PropertySchema` | abstract record + 14 sealed subclasses |
| `RichText` | `RichText` / `RichTextItem` | flat records |
| `PaginatedList<T>` | `PaginatedList` (generic-ised) | `next_cursor`, `has_more`, `results` |

The `BotBuildinClient` performs the translation in private mapping methods. The
mapping layer is unit-tested in `tests/Buildout.UnitTests/Buildin/`.

## Configuration keys

Bound under the `Buildin` configuration section. Environment-variable equivalents
use the `BUILDOUT__BUILDIN__` prefix.

| Key | Env var | Default |
|---|---|---|
| `Buildin:BaseUrl` | `BUILDOUT__BUILDIN__BASEURL` | `https://api.buildin.ai/` |
| `Buildin:BotToken` | `BUILDOUT__BUILDIN__BOTTOKEN` | (required) |
| `Buildin:HttpTimeout` | `BUILDOUT__BUILDIN__HTTPTIMEOUT` | `00:00:30` |
| `Buildin:UnsafeAllowInsecure` | `BUILDOUT__BUILDIN__UNSAFEALLOWINSECURE` | `false` |
