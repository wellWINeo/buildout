# Phase 0 Research: Buildin API V2 Migration

This research resolves the implementation-shaping questions for feature 016.
The V2 OpenAPI downloaded again on 2026-09-06 is OpenAPI 3.1.0,
`Buildin Developer API V2`, version `2.0.0`, with SHA-256
`413c8a8aa356a942b9f46e829b92e1cd047d470fbdf9d91edfbcbd6094cca49d`.
The live metadata endpoint checked on 2026-09-06 reports `pathCount: 17`,
`generatedAt: 2026-09-05T02:59:07.000Z`, and ETag
`da96784fcf4d3b29e4baaf5ded926ec9e2d3ef10a18a4536fecd456906493c90`;
the `pathCount: 22` shown on the documentation page is an example payload, not
current metadata.
Official V2 documentation additionally defines page/block DELETE and writable
page `in_trash`; the current OpenAPI omits those definitions. After removal of
Buildout's page lifecycle surfaces, only block DELETE is required by a retained
workflow and therefore belongs in the overlay.

Primary evidence:

- [Delete Page](https://buildin.ai/developer-api/v2/write/delete-page)
- [Delete Block](https://buildin.ai/developer-api/v2/write/delete-block)
- [Update Page](https://buildin.ai/developer-api/v2/write/update-page)
- [V2 Conventions](https://buildin.ai/developer-api/v2/reference/conventions)
- [V2 OpenAPI and metadata](https://buildin.ai/developer-api/v2/reference/openapi)
- [OpenAPI Overlay Specification 1.0.0](https://spec.openapis.org/overlay/v1.0.0.html)

## 1. Source of truth and supported route inventory

- **Decision**: Replace the repository-root `openapi.json` with an unmodified
  snapshot of the published V2 document and apply a separate reviewed overlay
  containing only documented block DELETE. The migration-relevant generated
  surface contains 13 V2 operations backing retained identity/page/block/database/
  search workflows.
- **Rationale**: The effective contract has 17 paths and 22 total operations;
  13 cover retained Buildout capabilities and 9 remain intentionally unexposed.
  The contract's first server remains
  `https://api.buildin.ai`, while the existing configurable base URL continues
  to override it for tests and alternate environments.
- **Alternatives considered**:
  - Continue consuming the GitHub-hosted V1 contract — rejected because it is
    not the V2 source of truth and makes accidental V1 regeneration invisible.
  - Expose semantic search, page movement, property-item reads, database
    mutations, direct Markdown, or file transfer now — rejected because these
    eight operations have no current Buildout-facing capability and FR-022
    requires separate specifications.

## 2. V2-only deletion boundary and lifecycle removal

- **Decision**: Remove all production V1 routing. Map semantic block deletion to
  `DELETE /v2/blocks/{block_id}`. Completely remove CLI `delete`/`restore`, MCP
  `delete_page`/`restore_page`, the core page-lifecycle service/models, their
  registrations, skills/prompts, and dedicated tests. Do not overlay or expose
  page DELETE or page-update `in_trash` for current use.
- **Rationale**: The user explicitly accepts the breaking lifecycle removal.
  Removing the full vertical slice is smaller and clearer than carrying aliases
  for capabilities no longer in Buildout's public contract.
- **Alternatives considered**:
  - Keep a small V1 adapter — rejected because the documented V2 operations make
    it unnecessary and SC-002 now requires zero V1 calls.
  - Keep deprecated PATCH-based lifecycle aliases — rejected by the accepted
    clean-break decision.
  - Expose native page DELETE without restore — rejected because adding a new
    destructive public workflow requires a separate specification.

## 3. Stable domain facade and modern trash naming

- **Decision**: Keep `IBuildinClient` as the sole version-neutral boundary while
  extending its internal result/request models to carry opaque V2 ETags and
  per-invocation write-safety metadata. Remove the unused page-update lifecycle
  method/request. Rename hand-maintained page, database, and block trash fields or
  intent from `Archived` to `InTrash`, mapping directly to V2 `in_trash`. Retain
  `DeleteBlockAsync` and route it to V2 DELETE.
- **Rationale**: `Archived` is V1-era terminology. The accepted breaking change
  removes an unnecessary translation concept while keeping generated DTOs behind
  the core boundary. ETags and idempotency keys remain opaque transport metadata.
- **Alternatives considered**:
  - Retain `Archived` and map it indefinitely — rejected in favor of modern V2
    naming and the accepted breaking change.
  - Keep an unused page-update facade method — rejected because it would be a
    compatibility stub with no retained caller.
  - Expose an API-version selector — rejected by FR-016 and Principle V.

## 4. Bearer credentials, configuration, and scopes

- **Decision**: Introduce `AccessToken` / `Buildout__AccessToken` as the primary
  opaque bearer-credential names. Retain `BotToken` / `Buildout__BotToken` as a
  deprecated fallback. If both resolve, `AccessToken` wins and the legacy key is
  ignored. Emit one key-name-only warning per process on legacy use or presence,
  through CLI stderr or MCP logging rather than MCP protocol stdout. Send the
  selected value only as `Authorization: Bearer <token>`, restrict forwarding to
  the configured API host, and introduce no OAuth flow. Maintain an explicit
  operation/workflow scope table.
- **Rationale**: V2 defines global HTTP bearer authentication and explicitly
  accepts both token types. Its `x-required-scopes` metadata is descriptive, not
  an OAuth flow the generated client can enforce. A generic primary name matches
  both credential forms, while the warned fallback gives existing installations
  a migration path without copying the secret immediately.
- **Alternatives considered**:
  - Remove `BotToken` immediately — rejected because the user requested a
    deprecated compatibility stub.
  - Add a distinct `OAuthToken` — rejected because both credentials are
    operationally identical bearer tokens.
  - Implement consent, callback, refresh, or token storage — rejected as out of
    scope.

## 5. V2 wire-to-domain translation

- **Decision**: Keep hand-maintained Buildout domain records and rewrite only
  the generated-model adapters. Map V2 `in_trash` directly to `InTrash`,
  `page_type` to existing search/object meaning, the workspace parent variant to
  `ParentWorkspace`, type-keyed block payloads to established block records, and
  V2 annotations to the existing rendering semantics. Treat unknown optional
  fields as forward-compatible additional data and unknown block variants through
  the existing unsupported/opaque policy.
- **Rationale**: Generated V2 types differ materially from V1: block payloads
  are discriminated type-named fields rather than generic `data`; search results
  are a page/database union; parents, bot identity, rich text, property values,
  and trash naming changed. Isolating these changes prevents CLI/MCP and domain
  services from depending on the wire contract.
- **Alternatives considered**:
  - Replace domain records with generated types — rejected because generated
    churn would leak into converters, services, and presentation projects.
  - Discard unknown variants — rejected because content loss violates output
    fidelity and FR-013.

## 6. Search compatibility

- **Decision**: Back both existing client search methods with
  `POST /v2/search`. Preserve each domain method's current projection,
  pagination, order, filters, and output behavior. `SearchPagesAsync` filters or
  projects V2 results so the existing `SearchService` continues to receive the
  page-like records it expects; it does not expose V2-only semantic search.
- **Rationale**: V2 has one keyword search route returning a page/database union,
  while the old client has two historical methods. One adapter avoids divergent
  route behavior and preserves the established search service contract.
- **Alternatives considered**:
  - Keep `/v1/pages/search` for one overload — rejected by FR-001/FR-015.
  - Return raw mixed generated objects to `SearchService` — rejected because it
    moves V2 knowledge above the core client boundary.

## 7. Structured failures and safe diagnostics

- **Decision**: Expand the core API error model to preserve status, stable code,
  message, request ID, structured details, and available retry metadata. Classify
  authentication (401), authorization (403), not found (404), validation (400),
  conflict (409), rate limit (429), transport, and unexpected failures distinctly.
  A 403 may mention the operation's documented scopes, but must preserve the
  service message and must not claim a missing scope when resource access or plan
  restriction is also possible.
- **Rationale**: V2's shared error schema supplies fields the current `ApiError`
  drops, and V2 uses stable codes such as `conflict`, `idempotency_conflict`, and
  `rate_limited`. Safe structured diagnostics meet FR-009/FR-010 without logging
  credentials or headers.
- **Alternatives considered**:
  - Continue flattening to message/raw body — rejected because callers lose
    request IDs and validation details.
  - Branch on human-readable messages — rejected because the contract explicitly
    makes status/code the stable classification surface.

## 8. Pagination, cancellation, and retries

- **Decision**: Centralize cursor validation/traversal rules: accept page sizes
  1 through 100; preserve returned order; advance only with `has_more` and a
  non-null next cursor; detect repeated/non-advancing cursors; propagate
  cancellation; never automatically retry writes except at most one page-create
  retry carrying the same `Idempotency-Key` and byte-equivalent body. Existing
  complete-result services continue draining pages through their domain loops,
  backed by exact V2 cursor mapping.
- **Rationale**: Search, block children, and database query share V2's list
  envelope. Guarding cursor progress prevents infinite loops/duplicates, while
  single-attempt non-idempotent writes avoid duplicate content when rate limits or
  transport failures make outcomes uncertain. V2's native create-page key makes
  retrying that one operation safe without a Buildout-side deduplication store.
- **Alternatives considered**:
  - Trust `has_more` without validating `next_cursor` — rejected because malformed
    responses can loop or omit results.
  - Transparently retry all 429 responses — rejected for writes that do not
    publish native idempotency support.

## 9. Kiota generation strategy

- **Decision**: Keep the published OpenAPI snapshot byte-for-byte authoritative,
  apply a separate OpenAPI Overlay 1.0 document with exactly one update (block
  DELETE), then produce a
  deterministic generation-only normalization before invoking pinned Kiota
  1.31.1. The applier verifies the reviewed upstream hash and fails if an overlay
  operation now exists upstream. The normalizer names inline union members and adds
  discriminator mappings where `const` tags are insufficient for Kiota. Generate
  only V2 paths under a V2 namespace, keep overlay/normalizer outside `Generated/`,
  and gate acceptance on compile plus representative serialization tests.
- **Rationale**: A generation probe completed and produced 357 files, but Kiota
  warned that `Block`, `BlockInput`, `UpdateBlockRequest`, `Parent`,
  `PropertyValue`, `PropertySchema`, search/list unions, and other composed types
  lack usable discriminators. The raw output contains empty discriminator values
  and loses typed search `results`, so successful generation alone is not a
  usable-client proof. The same derived input first applies the standardized
  overlay for documented block DELETE, then makes generator-specific
  repairs reproducible and reviewable.
- **Alternatives considered**:
  - Hand-edit generated C# — rejected because regeneration would erase fixes.
  - Edit/downgrade the canonical V2 snapshot — rejected because it obscures
    upstream changes and violates the source-of-truth requirement.
  - Hand-write the entire V2 client — rejected because it abandons reproducible
    external-contract generation.

## 10. Deterministic refresh and testing

- **Decision**: Fetch to a temporary file, validate title/version/V2-only paths,
  and atomically replace the canonical snapshot. Both shell regeneration entry
  points invoke the same normalizer and pinned Kiota flags. Replace the skipped
  determinism test with an offline isolated two-pass file-manifest/hash comparison.
  Cover every used V2 operation, including block DELETE,
  through WireMock; assert zero V1 calls and rerun all converter round trips and
  cross-surface acceptance tests.
- **Rationale**: The current fetch script writes the old V1 GitHub document to
  the caller's directory, and the current determinism test is skipped. An offline
  second-pass comparison proves stable generation without making normal test runs
  depend on Buildin or a real token.
- **Alternatives considered**:
  - Fetch the live contract during ordinary tests — rejected by Principle IV and
    FR-024.
  - Accept a clean git status after only one in-place run — rejected because it
    can miss nondeterminism and can disturb unrelated working-tree changes.

## 11. Caching, telemetry, and presentation parity

- **Decision**: Leave caching and retained higher-level domain workflows above
  `IBuildinClient` unchanged. Remove the page-lifecycle vertical slice and its
  cache invalidation tests. Preserve cancellation and edit invalidation behavior.
  Record each facade operation exactly once with low-cardinality method/outcome/
  failure-category tags, never paths, IDs, request IDs, tokens, or headers. Keep
  retained CLI/MCP names, schemas, success output, and exit/MCP error semantics
  stable; remove all page delete/restore registrations and documentation.
- **Rationale**: This limits migration blast radius, retains existing observability
  and non-destructive safeguards, and makes the V2-only route boundary measurable
  without leaking user data.
- **Alternatives considered**:
  - Add a new cache or presentation version — rejected because the existing
    abstractions already isolate the external API and no public behavior requires
    either.

## 12. Native idempotency and optimistic concurrency

- **Decision**: For `POST /v2/pages`, generate one opaque `Idempotency-Key` per
  logical create invocation and reuse it only for one retry after `429` or a
  transient transport failure, honoring cancellation and `Retry-After`. For
  write-oriented page reads, capture the response `ETag`; expose it
  through the existing public revision string. Remove the CRC-based
  `RevisionTokenComputer`. Before a multi-call block reconciliation, compare the
  supplied ETag with a fresh page ETag, but do not send undocumented headers to
  block endpoints or claim atomicity.
- **Rationale**: The canonical V2 OpenAPI already declares `Idempotency-Key` on
  page creation, `ETag` on page reads, and `If-Match` on page update. After page
  lifecycle removal, no retained workflow calls page PATCH. Block update, append,
  and delete do not declare `If-Match`; using it there would be an unsupported quirk. An
  opaque ETag removes Buildout's home-grown page-version hash while preserving
  the existing CLI/MCP revision field.
- **Alternatives considered**:
  - Apply `Idempotency-Key` and `If-Match` to every write — rejected because the
    V2 contract publishes them only on specific endpoints.
  - Retain the CRC of rendered Markdown as the revision authority — rejected
    because V2 supplies the authoritative page version.
  - Perform a no-op page PATCH before block writes — rejected because it adds a
    write without making the subsequent block calls atomic.

## 13. Native V2 boundary and compatibility inventory

- **Decision**: Ship no V1 runtime client, route fallback, version selector, or
  page-lifecycle compatibility stub. Treat DTO/domain mapping as the normal API
  boundary and use V2-native `InTrash` naming. Retain only one explicit runtime
  compatibility stub: deprecated `BotToken` configuration resolution with a
  warning. Keep the one-action generation overlay and WireMock `BuildinStubs`
  test-only.
- **Rationale**: These boundaries preserve public behavior without translating
  production requests back to V1. Calling them out prevents test stubs and normal
  domain adapters from being mistaken for protocol fallbacks. The only service
  limitation that remains visible is non-atomic multi-call block reconciliation.
- **Alternatives considered**:
  - Preserve generated V1 code as a fallback — rejected by FR-015 and SC-002.
  - Expose V1/V2 selection to callers — rejected by the constitution and FR-016.
  - Eliminate the `BotToken` alias immediately — rejected because the user chose
    a warned migration path for existing installations.

## Outcome

All technical unknowns are resolved. Phase 1 proceeds with a V2-only generated
surface, a one-action block-DELETE overlay, removed page lifecycle surfaces,
native page-create idempotency, opaque ETag revisions instead of local hashes,
V2-native `InTrash` naming, primary `AccessToken` configuration, deterministic
normalization, and mock-only validation. No V1 runtime stub remains; deprecated
`BotToken` resolution is the sole runtime compatibility stub, and the documented
non-atomic block-edit boundary is the only concurrency limitation.
