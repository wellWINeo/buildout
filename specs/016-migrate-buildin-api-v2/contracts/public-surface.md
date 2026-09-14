# Contract: Public Surface and Accepted Breaking Changes

## Removed surfaces

Feature 016 intentionally removes:

- CLI commands `delete` and `restore`, including their settings and skill files.
- MCP tools `delete_page` and `restore_page`, including tool handlers and prompt/
  server-instruction references.
- Core `IPageLifecycle`, `PageLifecycle`, and `PageLifecycleOutcome` types and DI
  registration.

They are not deprecated aliases: CLI discovery rejects the former command names,
MCP discovery does not advertise the tools, and no hidden compatibility route or
page-lifecycle request remains.

## Retained surfaces

- CLI: `create`, `get`, `search`, `update`, `tree`, and `db view`.
- MCP resource: existing Buildin page resource URI/name/MIME contract.
- MCP tools: `create_page`, `get_page_markdown`, `search`, `update_page`,
  `database_view`, and `tree`.

Retained inputs, output structures, exit/error semantics, Markdown rendering,
ordering, reconciliation summaries, database views, tree behavior, and
non-destructive guarantees remain stable except for two accepted naming changes:

- The opaque `revision` value is now a service ETag rather than an eight-character
  locally computed CRC. Consumers must not parse it.
- Exposed trash-state fields use `inTrash`/`InTrash`, not `archived`/`Archived`.

Block reconciliation compares the supplied revision with a fresh page ETag before
its first write. It remains a non-atomic sequence because V2 block writes do not
publish `If-Match`; a later failure retains the established partial-patch error.

## Credential compatibility stub

`AccessToken` and `Buildout__AccessToken` are primary. `BotToken` and
`Buildout__BotToken` remain a configuration-only deprecated fallback:

- Only legacy value present: use it and emit one deprecation warning per process.
- Both present: use `AccessToken` and emit one warning that the legacy key was
  ignored.
- Warning text names keys only; it never contains either value.
- CLI uses stderr. MCP uses its logging channel and never protocol stdout.

The alias selects neither a V1 route nor a bot-specific authentication mechanism.
It can carry either integration or OAuth bearer credentials.

## Stable failure classes

| Failure | CLI behavior | MCP/resource behavior | Diagnostic preservation |
|---------|--------------|-----------------------|-------------------------|
| Validation / bad identifier | Existing invalid-usage/validation exit semantics | Existing invalid-params/error response | Safe service code/details when present; zero unrelated requests for local ID failure. |
| Authentication (401) | Existing auth failure exit class | Existing error envelope | Status, code, message, request ID; no token. |
| Authorization (403) | Existing externally compatible failure exit class, wording identifies authorization | Existing error envelope, wording identifies authorization | Service message/request ID plus non-assertive required-scope guidance. |
| Not found (404) | Existing not-found exit class | Existing not-found/error convention | Status, code, message, request ID. |
| Conflict (409) | Existing unexpected/API failure exit class | Existing API error convention | Distinct `conflict` or `idempotency_conflict`; never success. A client-side ETag preflight mismatch retains the separate stale-revision class. |
| Rate limit (429) | Existing API failure exit class | Existing API error convention | `rate_limited`, request ID, retry guidance; only page create may retry once with its unchanged native key/body. |
| Transport | Existing transport exit class | Existing transport error convention | Safe transport message; only page create may retry once with its unchanged native key/body. |
| Unexpected | Existing unexpected exit class | Existing internal/API error convention | Safe message and inner cause for diagnostics. |

## Output and destructive invariants

- Markdown for every supported block type retains its documented round-trip
  behavior; unknown variants use the established unsupported/opaque representation.
- Multi-page search, block traversal, database view, and tree results contain no
  duplicates or omissions and retain service ordering.
- Block removal reports success only after V2 block DELETE confirms it.
- A mixed edit uses V2 for reads, updates, appends, and explicit block deletes;
  unrelated blocks are untouched and partial failure is never reported as success.
- The route journal contains zero production `/v1/` or page-lifecycle requests.

## Runtime boundary

The target runtime contains no V1 client, V1 route fallback, API-version selector,
or page-lifecycle compatibility service. Domain/DTO mapping is the normal typed API
boundary, not a fallback. The one-action OpenAPI overlay is generation input, and
`BuildinStubs.cs` is test infrastructure; neither is a runtime compatibility client.
