# Data Model: Buildin API V2 Migration

The migration adds no persisted business entities. It changes the external wire
contract and enriches transient core models. Retained presentation meanings stay
stable except for the accepted lifecycle removal and `Archived` to `InTrash`
naming change.

## V2 API Contract Snapshot

Represents the checked-in external source of truth.

| Field | Type | Rule |
|-------|------|------|
| `OpenApiVersion` | string | Must be `3.1.0` for the accepted snapshot. |
| `Title` | string | Must be `Buildin Developer API V2`. |
| `Version` | string | Must be `2.0.0` for this feature baseline. |
| `SourceUri` | URI | `https://api.buildin.ai/v2/openapi.json`. |
| `Paths` | ordered map | Production resource paths must use `/v2/`; public contract metadata routes are not generated into runtime capability code. |
| `ContentHash` | string | Computed by validation tooling for deterministic comparison; not sent at runtime. |
| `Overlay` | OpenAPI Overlay 1.0 document | Adds only documented block DELETE while the canonical OpenAPI omits it. |

### State transition

`downloaded` → `identity validated` → `atomically checked in` → `overlay
preconditions checked` → `overlay applied` → `normalized for generation` →
`generated` → `compiled/fixture verified`.

Any identity, route-prefix, parse, or generation check failure stops before the
existing snapshot/generated client is replaced.

## Domain Operation

A stable method on `IBuildinClient`, independent of API version.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | yes | Existing semantic method name. |
| `RequiredScopes` | ordered string set | yes | Static documentation/diagnostic metadata; empty for current-user identity. |
| `RouteClass` | constant `V2` | yes | Internal routing only; never accepted from CLI/MCP callers. |
| `IsWrite` | bool | yes | Controls retry safety; only native-idempotent page creation may be retried automatically. |
| `ResultContract` | domain type | yes | Existing Buildout model, never a generated DTO. |

### Validation rules

- All production operations route to V2; a V1 route is invalid.
- Input IDs are validated before dispatch where the operation contract requires a
  UUID; a local validation failure makes zero HTTP requests and is not wrapped as
  an unexpected failure.
- Cursors are opaque strings and are never parsed or constructed.

## Bearer Credential Resolution

An opaque secret resolved from the primary access-token name or its deprecated
configuration alias.

| Field | Type | Rule |
|-------|------|------|
| `PrimaryKey` | constant string | `AccessToken`; environment form `Buildout__AccessToken`. |
| `LegacyKey` | constant string | `BotToken`; environment form `Buildout__BotToken`; deprecated compatibility fallback. |
| `Value` | secret string | Required, non-empty; may be an integration token or OAuth access token. |
| `Transport` | constant | `Authorization: Bearer <value>` only. |
| `AllowedHost` | host | Derived from configured `BaseUrl`; token is not forwarded to other hosts. |

Resolution states:

| AccessToken | BotToken | Result |
|-------------|----------|--------|
| present | absent | Use `AccessToken`; no warning. |
| absent | present | Use `BotToken`; emit one deprecation warning. |
| present | present | Use `AccessToken`; emit one warning that the legacy key was ignored. |
| absent | absent | Fail startup naming `AccessToken` and the deprecated fallback, without a secret value. |

The warning is emitted once per process, names keys only, uses CLI stderr or MCP
logging, and never touches MCP protocol stdout. The credential has no persisted
type discriminator. Buildout does not inspect its format, refresh it, log it,
place it in metrics, or copy it to another setting.

## Native Request Safety Context

Transient metadata for one logical V2 operation. It is not persisted and is not
part of an API request body.

| Field | Type | Rule |
|-------|------|------|
| `IdempotencyKey` | opaque string? | Generated once for a logical page create, sent as `Idempotency-Key`, and reused only with the byte-equivalent body for retries of that invocation. A separate create receives a new key. |
| `Attempt` | integer in `[1, 2]` | Initial request plus at most one automatic retry; does not identify a user or resource. |

Buildout keeps no idempotency database or cross-invocation deduplication cache.
The idempotency key may not appear in URLs, request bodies, logs, metrics, or
public output. The ETag is separate response metadata exposed only through the
already-established opaque revision field/error detail.

## API Failure

Transient domain representation of a failed service call.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Category` | enum | yes | `Authentication`, `Authorization`, `NotFound`, `Validation`, `Conflict`, `RateLimit`, `Transport`, or `Unexpected`. |
| `StatusCode` | integer | for service failures | HTTP status returned by Buildin. |
| `Code` | string? | no | Stable V2 code such as `validation_error`, `forbidden`, `conflict`, or `rate_limited`. |
| `Message` | string | yes | Service or safe transport message; credential and sensitive headers excluded. |
| `RequestId` | string? | no | Body `request_id`, falling back to `X-Request-Id` when available. |
| `Details` | ordered `ApiFailureDetail` list | no | Structured V2 validation/limit details, preserving known and future fields safely. |
| `RetryAfter` | duration/date string? | no | Preserved `Retry-After` guidance; only page creation may use it for its one idempotency-protected retry. |
| `RawBody` | string? | no | Retained only when safe/sanitized; never exposed if it could contain a token/header. |

### API failure classification

| Status/code | Category | Caller meaning |
|-------------|----------|----------------|
| 401 / `unauthorized` | Authentication | Missing, malformed, expired, or invalid token. |
| 403 / `forbidden` | Authorization | Token is valid; scope, resource access, or workspace plan may block the operation. |
| 404 / `not_found` | NotFound | Resource absent or invisible. |
| 400 / validation or unsupported code | Validation | Request/filter/sort/block shape rejected. |
| 409 / conflict code | Conflict | Concurrent write or idempotency conflict. |
| 429 / `rate_limited` | RateLimit | Preserve retry guidance; only page creation may retry once with its unchanged key/body. |
| transport exception | Transport | No reliable service outcome. |
| other | Unexpected | Safe fallback with original exception as inner cause. |

## API Failure Detail

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Path` | string? | no | Invalid request location. |
| `Reason` | string? | no | Stable explanatory reason when supplied. |
| `Limit` | number? | no | Applicable service limit. |
| `Actual` | number? | no | Submitted/observed value. |
| `AdditionalFields` | read-only map | no | Unknown structured fields retained for forward compatibility after secret filtering. |

## Paginated Result

The existing `PaginatedList<T>`, `PageSearchResults`, and
`QueryDatabaseResult` remain the domain projections of V2 list envelopes.

| Field | Type | Rule |
|-------|------|------|
| `Results` | ordered list | Append exactly in service order; never silently drop known items. |
| `HasMore` | bool | Continue only when true. |
| `NextCursor` | string? | Must be non-null and different from prior cursors when `HasMore` is true. |
| request `PageSize` | int? | If supplied, must be in `[1, 100]`. |

### Traversal state

Each complete-result workflow maintains a per-call set of seen cursors. A repeated
or non-advancing cursor fails diagnostically instead of returning duplicates or
looping. Cancellation terminates traversal without issuing another request.

## Resource Models

The following existing models remain version-neutral:

- `Page`: V2 `in_trash` maps directly to `InTrash`; `page_type` maps to existing
  object meaning; properties/title remain Buildout domain values. A versioned page read
  carries the response `ETag` beside the page rather than inside the resource body.
- `Database`: V2 `in_trash` maps directly to `InTrash`; title, schema, parent, inline
  state, and URL retain current semantics.
- `Block` hierarchy: V2 type-keyed payloads map to current concrete block records;
  V2 `in_trash` maps directly to `InTrash` on reads and updates;
  explicit block removal routes through semantic `DeleteBlockAsync` to V2 DELETE.
- `Parent`: V2 `workspace`, `page_id`, `database_id`, and `block_id` variants map
  to the existing parent hierarchy.
- `RichText`, `Annotations`, `Mention`, `PropertyValue`, and `PropertySchema`:
  preserve existing converter behavior; unknown safe optional fields are ignored
  or retained without breaking known data.
- `UserMe`: V2 `bot_user` identity maps only fields with a stable domain meaning;
  no owner email is inferred and `users.email.read` is not required.

## Removed Page Lifecycle Model

`IPageLifecycle`, `PageLifecycle`, `PageLifecycleOutcome`, and the hand-maintained
`UpdatePageRequest` are removed because no retained public workflow uses them.
CLI/MCP discovery contains no page delete/restore surface, and the effective
generation overlay does not add page DELETE or writable page `in_trash`.

Page/database responses may still expose their canonical V2 `in_trash` state as
domain `InTrash`; that data mapping does not create a lifecycle operation.

## Editing Revision

The existing public `Revision` string becomes the opaque `ETag` returned by a V2
page read. `RevisionTokenComputer` and locally computed Markdown hashes are removed.

For a block reconciliation, Buildout compares the supplied revision with a freshly
read page ETag before the first block write. The following
block update/append/delete calls remain independently committed because those V2
operations do not publish `If-Match`; partial failure state remains explicit.

After a committed block edit, a fresh page read supplies `NewRevision`. A dry run
does not write, so it returns the validated current ETag.
