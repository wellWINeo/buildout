# Feature Specification: Migrate to Buildin API V2

**Feature Branch**: `016-migrate-buildin-api-v2`  
**Created**: 2026-09-05  
**Status**: Draft  
**Input**: User description: "Migrate to Buildin API V2. Documentation: https://buildin.ai/developer-api/v2/getting-started/authentication-and-scopes. OpenAPI: https://api.buildin.ai/v2/openapi.json"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Continue Existing Workflows on V2 (Priority: P1)

A Buildout user continues to read, search, create, edit, and browse Buildin content through the retained CLI and MCP surfaces after the service integration moves to Buildin API V2. The former page delete/restore commands and tools are intentionally removed as an accepted breaking change; retained inputs, output shapes, and non-destructive editing guarantees remain stable.

**Why this priority**: Preserving retained user-visible behavior while moving supported traffic to V2 is the purpose of the migration. The explicitly removed lifecycle surface must not cause regressions in unrelated workflows.

**Independent Test**: Run the existing CLI and MCP acceptance journeys against a mocked V2 service and verify that read, search, create, update, database-view, and tree operations return the same user-visible outcomes while every operation with a V2 equivalent uses a V2 route.

**Acceptance Scenarios**:

1. **Given** a valid integration token with the required read scopes, **When** a user reads a page, searches content, renders a database view, or requests a tree, **Then** the operation succeeds through V2 and produces the established output shape and ordering.
2. **Given** a valid token with the required write scopes, **When** a user creates a page or applies a non-destructive page edit that V2 supports, **Then** the operation succeeds through V2 using native idempotency where documented and an opaque page ETag for the existing edit preflight.
3. **Given** a result set or block list larger than one service page, **When** a user performs an operation that promises complete traversal, **Then** all service pages are followed in order and no accessible result is silently omitted.
4. **Given** an existing `BotToken` configuration containing a valid Buildin token, **When** the upgraded application starts, **Then** the same configuration remains usable without copying the token and emits one secret-safe migration warning.

---

### User Story 2 - Diagnose Authentication and Permission Problems (Priority: P2)

An operator can use either an integration token or an OAuth access token and receives a safe, actionable explanation when authentication, scope, resource access, workspace-plan, conflict, or rate-limit checks prevent an operation. The explanation identifies the failure category and the affected operation without exposing the credential.

**Why this priority**: V2 applies operation-specific scopes and can return the same authorization status for several different causes. Clear diagnostics are essential for operators to grant the right access or recognize a plan/resource restriction.

**Independent Test**: Exercise representative protected operations with missing, malformed, expired, insufficiently scoped, resource-denied, plan-restricted, conflicting, and rate-limited responses and verify the user receives the correct failure category, service request identifier when available, and no token value.

**Acceptance Scenarios**:

1. **Given** no token or an invalid token, **When** a protected operation is requested, **Then** the user receives an authentication failure that explains that a valid bearer token is required.
2. **Given** a valid token lacking the operation's required scope, **When** the operation is requested, **Then** the user receives an authorization failure that identifies the required scope when the service provides enough information to do so.
3. **Given** a valid and sufficiently scoped token whose workspace plan or resource access blocks the operation, **When** the operation is requested, **Then** the user receives an authorization failure that preserves the service message and request identifier rather than being told the token is invalid.
4. **Given** a V2 error containing a status, code, message, request identifier, and validation details, **When** Buildout reports the failure, **Then** those diagnostic fields remain available to the caller while secrets and sensitive request headers remain absent.
5. **Given** a valid OAuth access token, **When** the user performs an operation covered by its authorized scopes, **Then** it is accepted as a bearer credential without requiring an OAuth authorization flow inside Buildout.

---

### User Story 3 - Maintain Contract Fidelity (Priority: P3)

A maintainer can refresh the checked-in Buildin contract from the published V2 OpenAPI document and reproduce the client surface deterministically. The review clearly separates mechanical contract changes from the stable Buildout-facing behavior and identifies any V2 capability gaps before they become user regressions.

**Why this priority**: Buildin V2 is a versioned external contract that will evolve. A reproducible refresh and an explicit documented-overlay boundary keep future changes reviewable and prevent accidental reintroduction of V1 traffic.

**Independent Test**: Refresh the V2 contract twice without an upstream change and verify the second refresh produces no changes; then inspect the operation inventory and confirm that every supported Buildout capability maps to V2, including documented operations temporarily supplied to generation through the reviewed overlay.

**Acceptance Scenarios**:

1. **Given** an unchanged published V2 contract, **When** a maintainer refreshes and regenerates the client twice, **Then** the second run produces no version-control changes.
2. **Given** the current V2 documentation and OpenAPI document, **When** a maintainer reviews Buildout's operation coverage, **Then** block deletion uses its documented V2 operation and no production V1 route remains.
3. **Given** the published V2 OpenAPI currently omits documented block deletion, **When** a maintainer refreshes the contract, **Then** a one-action reviewed overlay supplies only that operation and fails when upstream catches up so a duplicate definition cannot persist.
4. **Given** V2-only capabilities such as semantic search, page movement, database mutations, direct Markdown export, or file transfer, **When** this migration is completed, **Then** no new user-facing command or tool is introduced solely because the endpoint exists.

### Edge Cases

- The token is missing, malformed, expired, or otherwise invalid: report an authentication failure and never echo the token.
- A valid token lacks a required scope: report authorization failure and, when known, name the missing scope.
- A valid token is blocked by resource permissions or workspace plan: preserve that distinction when the service response permits it; do not misclassify it as invalid authentication.
- The service returns `429 rate_limited`: preserve the service error and any retry guidance available to the caller; only a page-create attempt protected by the same `Idempotency-Key` and identical body may be retried automatically.
- The service returns `409 conflict` or `idempotency_conflict`: surface the conflict as a distinct API failure and do not present it as success.
- A page-create retry occurs after an ambiguous transport failure: reuse the exact `Idempotency-Key` and body for the same logical invocation so V2, rather than a Buildout-side deduplication store, determines the outcome.
- A block edit uses an old page revision: compare the opaque ETag from the editing snapshot with a fresh page ETag before the first block write; do not send undocumented `If-Match` headers to block endpoints or claim the multi-request reconciliation is atomic.
- A response contains a new optional field or an unknown future object variant: continue processing known data where safe and preserve a diagnosable failure where safe processing is impossible.
- A page, database query, search, or block-child listing contains exactly 100 items or requires multiple cursors: respect the V2 page-size limit and cursor order without duplicates or omissions.
- An identifier is not a valid V2 resource identifier: reject it clearly before, or report the V2 validation response after, making no unrelated request.
- A page edit includes block removal: use `DELETE /v2/blocks/{block_id}` only for the removed block IDs and preserve all unrelated blocks.
- A user invokes a former page delete/restore surface: the CLI reports an unknown command and MCP does not advertise or register the removed tool; no lifecycle request is sent.
- V2 represents trash state as `in_trash`: use `InTrash` consistently in hand-maintained domain models and serialized Buildout outputs rather than retaining the V1-era `Archived` name.
- The V2 contract describes a block type that Buildout does not render or author: retain the established opaque/unsupported-block behavior and do not silently lose content.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All existing Buildout operations that have a direct equivalent in the published Buildin V2 contract MUST use the V2 operation in production.
- **FR-002**: After migration, the retained CLI commands (`create`, `get`, `search`, `update`, `tree`, and database `view`) and retained MCP resources/tools MUST preserve their established names, inputs, output shapes, exit/error semantics, and non-destructive guarantees unless this specification explicitly states otherwise. The revision field remains an opaque string but changes from a local checksum value to a V2 ETag. Page `delete`/`restore` commands and `delete_page`/`restore_page` tools are explicitly removed as an accepted breaking change.
- **FR-003**: The V2 migration MUST cover the retained capabilities for current-user identity, page create/read, block read/update/delete/child listing/child append, database create/read/update/query, and search.
- **FR-004**: Protected V2 requests MUST authenticate with `Authorization: Bearer <token>`. Tokens MUST NOT be placed in query strings, request bodies, logs, metrics, errors, or committed fixtures.
- **FR-005**: The configured bearer credential MUST accept both Buildin integration tokens and OAuth access tokens. Implementing an OAuth authorization, consent, callback, refresh, or token-storage flow is out of scope.
- **FR-006**: `AccessToken` and `Buildout__AccessToken` MUST be the primary configuration names. Existing `BotToken` and `Buildout__BotToken` values MUST remain accepted as deprecated fallbacks without requiring a second copy of the secret.
- **FR-007**: User and operator documentation MUST state the minimum scopes required by existing functionality: `pages.read`, `pages.write`, `blocks.read`, `blocks.write`, `databases.read`, `databases.write`, and `search.read`. It MUST also state that current-user identity requires a valid token without an additional scope and that `users.email.read` is not required by existing Buildout functionality.
- **FR-008**: Scope documentation MUST map each CLI command and MCP resource/tool to the scopes it can require, including combined read/write workflows such as page editing and hierarchy traversal.
- **FR-009**: Authentication failures, authorization failures, not-found responses, conflicts, rate limits, validation failures, transport failures, and unexpected failures MUST remain distinguishable to callers.
- **FR-010**: For a V2 API error, Buildout MUST preserve the service status, error code, message, request identifier, and structured details when present, without exposing sensitive headers or credentials.
- **FR-011**: Cursor-based V2 list operations MUST honor the published page-size range of 1 through 100, preserve service ordering, and prevent duplicates or omissions while fulfilling an existing complete-result workflow.
- **FR-012**: Request and response translation MUST preserve the established Buildout domain meaning for page, database, parent, property, rich-text, block, pagination, and current-user data while using V2-native `InTrash` naming in hand-maintained models and outputs.
- **FR-013**: Every block type currently supported by Buildout's read and write workflows MUST retain its documented round-trip behavior after migration. V2 block types outside current support MUST follow the existing compatibility-matrix policy rather than being silently discarded.
- **FR-014**: Block deletion MUST use `DELETE /v2/blocks/{block_id}`. Feature 016 MUST NOT add page DELETE or page-update `in_trash` to the generation overlay and MUST NOT expose page delete/restore operations.
- **FR-015**: No production operation MAY call a V1 route after this feature is complete.
- **FR-016**: API routing MUST remain internal to the shared Buildout domain boundary. CLI and MCP consumers MUST NOT select an API version or route.
- **FR-017**: A failed V2 block-deletion operation MUST fail visibly. Buildout MUST NOT report block removal as successful unless the service confirms the requested outcome.
- **FR-018**: The CLI `delete` and `restore` commands, MCP `delete_page` and `restore_page` tools, their skills/prompts, the core page-lifecycle service/models, registrations, and dedicated tests MUST be removed. No deprecated runtime alias or hidden compatibility route may remain.
- **FR-019**: The version-controlled external contract MUST identify itself as Buildin Developer API V2 version 2.0.0 and use the published V2 OpenAPI document plus a one-action reviewed overlay for documented-but-omitted block DELETE as its source of truth. The overlay MUST be removed when the published OpenAPI contains that operation.
- **FR-020**: Refreshing and regenerating from an unchanged V2 contract MUST be deterministic, and generated artifacts MUST remain clearly separated from hand-maintained behavior.
- **FR-021**: Contract refresh documentation MUST point to the published V2 OpenAPI location and make accidental regeneration from the former V1 source detectable during review.
- **FR-022**: The migration MUST NOT expose new user-facing functionality for V2-only operations. Such capabilities require separate specifications.
- **FR-023**: Existing caching, cancellation, telemetry, ordering, output-fidelity, and destructive-action safeguards MUST continue to apply to V2-backed operations.
- **FR-024**: All migration tests MUST run against controlled mock responses and MUST NOT require a live Buildin service or real token.
- **FR-025**: The migration acceptance suite MUST cover representative success and error responses for every V2 operation used by a retained Buildout workflow, including block deletion, and MUST assert that no production V1 or removed page-lifecycle request occurs.
- **FR-026**: `CreatePageAsync` MUST send a V2 `Idempotency-Key` generated once per logical create invocation and MAY make at most one automatic retry after a `429` or transient transport failure, honoring cancellation and `Retry-After` when present. That retry MUST reuse the same key with an identical body. Buildout MUST NOT implement a client-side idempotency cache or deduplication store, and a separate logical create invocation MUST receive a new key.
- **FR-027**: Editing page reads MUST preserve the response `ETag` as an opaque revision token, and locally computed Markdown/content hashes MUST NOT be used as page-version authorities. Because no retained workflow calls page PATCH, feature 016 has no production `If-Match` use.
- **FR-028**: Native safety headers MUST be sent only on endpoints that publish them. Because the published V2 contract does not expose `If-Match` on block update, append, or delete operations, Buildout MUST use a fresh page-ETag comparison before block reconciliation, MUST keep partial-write failures visible, and MUST NOT describe the multi-request block edit as atomic.
- **FR-029**: Page, database, and block trash state or intent MUST use `InTrash` in hand-maintained models and map directly to V2 `in_trash`; Buildout MUST NOT retain `Archived` as a domain or wire property.
- **FR-030**: When the deprecated `BotToken` configuration name supplies the credential, Buildout MUST emit one secret-safe deprecation warning per process directing the operator to `AccessToken`. If both names resolve, `AccessToken` MUST win and the warning MUST state only that the legacy key was ignored. CLI warnings go to stderr; MCP warnings use its logging channel and MUST NOT corrupt stdio protocol output.

### Key Entities

- **V2 API Contract**: The published, versioned description of Buildin V2 operations, request and response shapes, errors, scopes, and service locations, supplemented by the one-action reviewed overlay for block DELETE until it appears upstream.
- **Bearer Credential**: A Buildin integration token or OAuth access token supplied through primary `AccessToken` configuration or deprecated `BotToken` fallback and sent only in the authorization header.
- **Scope**: A permission attached to a credential that authorizes a category of page, block, database, user, or search operation.
- **Domain Operation**: A stable Buildout-facing capability used by CLI and MCP consumers, independent of the external route or version that fulfills it.
- **Legacy Token Alias**: The deprecated `BotToken`/`Buildout__BotToken` configuration name accepted only when resolving the bearer credential; it never selects a V1 route or authentication mode.
- **API Failure**: A diagnosable service failure containing a status and, when supplied, an error code, message, request identifier, and structured details.
- **Paginated Result**: An ordered collection segment plus cursor metadata used to continue reading results without duplication or omission.
- **Native Request Safety Context**: Per-logical-operation transport metadata containing an opaque V2 idempotency key or ETag precondition; it is never a route selector, persisted deduplication record, or user-visible output field beyond the existing revision string.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of retained read, search, create, update, database-view, and tree acceptance journeys pass with the documented V2-native `InTrash` and ETag naming changes.
- **SC-002**: 100% of production Buildin calls use V2 routes; zero V1 calls occur.
- **SC-003**: Users can complete their first V2-backed read with `AccessToken`; an existing valid `BotToken` configuration also works while emitting exactly one secret-safe deprecation warning.
- **SC-004**: Integration and OAuth tokens with the same authorized scopes produce equivalent outcomes for all covered operations in the mocked acceptance suite.
- **SC-005**: Representative `401`, `403`, `404`, `409`, and `429` responses are classified correctly in 100% of migration acceptance cases, and every available service request identifier is retained.
- **SC-006**: Searches and block traversals spanning at least three service pages return exactly the expected item count and order, with zero duplicates and zero omissions.
- **SC-007**: Every currently supported block type passes the project-required bidirectional round-trip checks after migration, with zero newly undocumented lossy conversions.
- **SC-008**: Running the contract refresh and client regeneration twice against an unchanged V2 document produces zero changes on the second run.
- **SC-009**: Automated secret-safety checks and log assertions find zero bearer token values in logs, metrics, error messages, snapshots, or committed test data.
- **SC-010**: CLI command discovery contains neither `delete` nor `restore`, MCP tool discovery contains neither `delete_page` nor `restore_page`, and edits that remove blocks use V2 block DELETE with zero V1 or page-lifecycle requests.
- **SC-011**: In mocked page-create retry cases, 100% of attempts for one logical invocation carry the same non-empty `Idempotency-Key` and byte-equivalent body, while a separate invocation carries a different key.
- **SC-012**: In mocked block-edit cases, the ETag returned by the editing read is compared byte-for-byte with a fresh page ETag before the first write; a mismatch produces the established revision-conflict behavior, no locally computed revision hash is used, and partial multi-call failures remain explicit.

## Assumptions

- The published documentation for V2 operations, together with the supplied OpenAPI document titled `Buildin Developer API V2` and versioned `2.0.0`, is authoritative for this feature. A reviewed generation overlay bridges only the block DELETE omission required by retained behavior.
- Migration means moving all existing capabilities with published V2 equivalents to V2 while preserving existing user behavior; it does not mean exposing every new V2 endpoint.
- Buildin's V2 documentation defines `DELETE /v2/blocks/{block_id}` while the current OpenAPI omits it, so generation applies one overlay action rather than retaining V1 traffic. Documented page lifecycle operations are not required after their Buildout surfaces are removed.
- Removal of page delete/restore surfaces and the `Archived` → `InTrash` naming change are accepted breaking changes. All other retained user-visible CLI and MCP contracts remain migration constraints.
- V2 documents `Idempotency-Key` for page creation and `If-Match` for page update. Feature 016 uses idempotency for create; after lifecycle removal no retained workflow calls page PATCH, and the migration will not invent unsupported block-write preconditions.
- `AccessToken` is the primary credential name. `BotToken` is a temporary configuration-only compatibility stub with an explicit deprecation warning; it does not preserve any V1 API behavior.
- Tokens are provisioned outside Buildout. OAuth consent, refresh, revocation, and secure long-term token storage are separate features.
- The published production service remains `https://api.buildin.ai`; alternate service locations continue to use the project's established configuration rules.
- The V2-only semantic-search, page-move, page-property, database-mutation, Markdown-export, and file-transfer operations are candidates for later features, not part of this migration.
- Performance optimization beyond preventing pagination defects and regressions is out of scope; the migration retains existing performance expectations.
