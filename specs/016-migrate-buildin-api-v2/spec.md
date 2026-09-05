# Feature Specification: Migrate to Buildin API V2

**Feature Branch**: `016-migrate-buildin-api-v2`  
**Created**: 2026-09-05  
**Status**: Draft  
**Input**: User description: "Migrate to Buildin API V2. Documentation: https://buildin.ai/developer-api/v2/getting-started/authentication-and-scopes. OpenAPI: https://api.buildin.ai/v2/openapi.json"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Continue Existing Workflows on V2 (Priority: P1)

A Buildout user continues to read, search, create, edit, and browse Buildin content through the existing CLI and MCP surfaces after the service integration moves to Buildin API V2. Existing command names, tool names, inputs, output shapes, and non-destructive editing guarantees remain stable, so the migration does not require consumers to rewrite their workflows.

**Why this priority**: Preserving today's user-visible behavior while moving supported traffic to V2 is the purpose of the migration. A migration that breaks established workflows does not deliver a viable release.

**Independent Test**: Run the existing CLI and MCP acceptance journeys against a mocked V2 service and verify that read, search, create, update, database-view, and tree operations return the same user-visible outcomes while every operation with a V2 equivalent uses a V2 route.

**Acceptance Scenarios**:

1. **Given** a valid integration token with the required read scopes, **When** a user reads a page, searches content, renders a database view, or requests a tree, **Then** the operation succeeds through V2 and produces the established output shape and ordering.
2. **Given** a valid token with the required write scopes, **When** a user creates a page or applies a non-destructive page edit that V2 supports, **Then** the operation succeeds through V2 without changing the existing CLI or MCP contract.
3. **Given** a result set or block list larger than one service page, **When** a user performs an operation that promises complete traversal, **Then** all service pages are followed in order and no accessible result is silently omitted.
4. **Given** an existing Buildout configuration containing a valid Buildin token, **When** the upgraded application starts, **Then** the same configuration remains usable without requiring the token to be copied to a new setting.

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

**Why this priority**: Buildin V2 is a versioned external contract that will evolve. A reproducible refresh and an explicit compatibility boundary keep future changes reviewable and prevent accidental reintroduction of V1 traffic.

**Independent Test**: Refresh the V2 contract twice without an upstream change and verify the second refresh produces no changes; then inspect the operation inventory and confirm that every supported Buildout capability maps either to a V2 operation or to an explicitly approved temporary compatibility exception.

**Acceptance Scenarios**:

1. **Given** an unchanged published V2 contract, **When** a maintainer refreshes and regenerates the client twice, **Then** the second run produces no version-control changes.
2. **Given** the current V2 contract, **When** a maintainer reviews Buildout's operation coverage, **Then** all V2-supported existing capabilities are mapped to V2 and the only remaining V1 compatibility operations are page archive/restore and block deletion.
3. **Given** a future V2 contract that adds an equivalent for a compatibility operation, **When** the migration is updated, **Then** that operation can leave the compatibility list without changing the CLI or MCP contract.
4. **Given** V2-only capabilities such as semantic search, page movement, database mutations, direct Markdown export, or file transfer, **When** this migration is completed, **Then** no new user-facing command or tool is introduced solely because the endpoint exists.

### Edge Cases

- The token is missing, malformed, expired, or otherwise invalid: report an authentication failure and never echo the token.
- A valid token lacks a required scope: report authorization failure and, when known, name the missing scope.
- A valid token is blocked by resource permissions or workspace plan: preserve that distinction when the service response permits it; do not misclassify it as invalid authentication.
- The service returns `429 rate_limited`: preserve the service error and any retry guidance available to the caller; do not retry writes automatically in a way that could duplicate them.
- The service returns `409 conflict` or `idempotency_conflict`: surface the conflict as a distinct API failure and do not present it as success.
- A response contains a new optional field or an unknown future object variant: continue processing known data where safe and preserve a diagnosable failure where safe processing is impossible.
- A page, database query, search, or block-child listing contains exactly 100 items or requires multiple cursors: respect the V2 page-size limit and cursor order without duplicates or omissions.
- An identifier is not a valid V2 resource identifier: reject it clearly before, or report the V2 validation response after, making no unrelated request.
- A page edit includes block removal or a user invokes page delete/restore: use only the approved compatibility operation needed for that behavior; all supported portions of a mixed edit remain subject to the migration boundary.
- A compatibility operation receives a credential that is valid for V2 but not accepted by V1: fail safely with an actionable compatibility limitation; never silently skip the requested deletion or restoration.
- V2 represents deleted state as `in_trash` while existing Buildout behavior uses an archived/deleted concept: preserve the existing user-visible meaning consistently across reads and lifecycle results.
- The V2 contract describes a block type that Buildout does not render or author: retain the established opaque/unsupported-block behavior and do not silently lose content.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All existing Buildout operations that have a direct equivalent in the published Buildin V2 contract MUST use the V2 operation in production.
- **FR-002**: After migration, the existing CLI commands (`create`, `get`, `search`, `update`, `delete`, `restore`, `tree`, and database `view`) and MCP resources/tools MUST retain their established names, inputs, outputs, exit/error semantics, and non-destructive guarantees unless this specification explicitly states otherwise.
- **FR-003**: The V2 migration MUST cover the existing capabilities for current-user identity, page create/read/update, block read/update/child listing/child append, database create/read/update/query, and search.
- **FR-004**: Protected V2 requests MUST authenticate with `Authorization: Bearer <token>`. Tokens MUST NOT be placed in query strings, request bodies, logs, metrics, errors, or committed fixtures.
- **FR-005**: The configured bearer credential MUST accept both Buildin integration tokens and OAuth access tokens. Implementing an OAuth authorization, consent, callback, refresh, or token-storage flow is out of scope.
- **FR-006**: Existing valid token configuration MUST continue to work after upgrade without requiring a new mandatory setting or a second copy of the secret.
- **FR-007**: User and operator documentation MUST state the minimum scopes required by existing functionality: `pages.read`, `pages.write`, `blocks.read`, `blocks.write`, `databases.read`, `databases.write`, and `search.read`. It MUST also state that current-user identity requires a valid token without an additional scope and that `users.email.read` is not required by existing Buildout functionality.
- **FR-008**: Scope documentation MUST map each CLI command and MCP resource/tool to the scopes it can require, including combined read/write workflows such as page editing and hierarchy traversal.
- **FR-009**: Authentication failures, authorization failures, not-found responses, conflicts, rate limits, validation failures, transport failures, and unexpected failures MUST remain distinguishable to callers.
- **FR-010**: For a V2 API error, Buildout MUST preserve the service status, error code, message, request identifier, and structured details when present, without exposing sensitive headers or credentials.
- **FR-011**: Cursor-based V2 list operations MUST honor the published page-size range of 1 through 100, preserve service ordering, and prevent duplicates or omissions while fulfilling an existing complete-result workflow.
- **FR-012**: Request and response translation MUST preserve the established Buildout domain meaning for page, database, parent, property, rich-text, block, pagination, and current-user data despite V1/V2 naming or shape differences.
- **FR-013**: Every block type currently supported by Buildout's read and write workflows MUST retain its documented round-trip behavior after migration. V2 block types outside current support MUST follow the existing compatibility-matrix policy rather than being silently discarded.
- **FR-014**: Page archive/restore and block deletion MUST remain available through a narrowly bounded V1 compatibility path because the supplied V2 contract exposes no equivalent operation or writable deleted-state field.
- **FR-015**: No production operation other than page archive/restore and block deletion MAY call a V1 route after this feature is complete.
- **FR-016**: Compatibility routing MUST remain internal to the shared Buildout domain boundary. CLI and MCP consumers MUST NOT select API versions or observe a different contract for compatibility operations.
- **FR-017**: A failed compatibility operation MUST fail visibly. Buildout MUST NOT report deletion, restoration, or block removal as successful unless the service confirms the requested outcome.
- **FR-018**: The V1 compatibility path MUST be removable when Buildin publishes V2 equivalents without requiring changes to presentation-layer contracts or unrelated domain workflows.
- **FR-019**: The version-controlled external contract MUST identify itself as Buildin Developer API V2 version 2.0.0 and use the published V2 document as its source of truth.
- **FR-020**: Refreshing and regenerating from an unchanged V2 contract MUST be deterministic, and generated artifacts MUST remain clearly separated from hand-maintained behavior.
- **FR-021**: Contract refresh documentation MUST point to the published V2 OpenAPI location and make accidental regeneration from the former V1 source detectable during review.
- **FR-022**: The migration MUST NOT expose new user-facing functionality for V2-only operations. Such capabilities require separate specifications.
- **FR-023**: Existing caching, cancellation, telemetry, ordering, output-fidelity, and destructive-action safeguards MUST continue to apply to V2-backed operations.
- **FR-024**: All migration tests MUST run against controlled mock responses and MUST NOT require a live Buildin service or real token.
- **FR-025**: The migration acceptance suite MUST cover representative success and error responses for every V2 operation used by an existing Buildout workflow, plus every approved V1 compatibility operation.

### Key Entities

- **V2 API Contract**: The published, versioned description of Buildin V2 operations, request and response shapes, errors, scopes, and service locations used as the migration source of truth.
- **Bearer Credential**: A Buildin integration token or OAuth access token supplied through Buildout's existing secret configuration path and sent only in the authorization header.
- **Scope**: A permission attached to a credential that authorizes a category of page, block, database, user, or search operation.
- **Domain Operation**: A stable Buildout-facing capability used by CLI and MCP consumers, independent of the external route or version that fulfills it.
- **Compatibility Operation**: An existing domain behavior with no equivalent in the supplied V2 contract; limited in this feature to page archive/restore and block deletion.
- **API Failure**: A diagnosable service failure containing a status and, when supplied, an error code, message, request identifier, and structured details.
- **Paginated Result**: An ordered collection segment plus cursor metadata used to continue reading results without duplication or omission.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing read, search, create, update, database-view, and tree acceptance journeys pass with unchanged user-facing contracts after migration.
- **SC-002**: 100% of production calls for capabilities with a published V2 equivalent use V2 routes; zero V1 calls occur outside the two documented compatibility behaviors.
- **SC-003**: Users with an existing valid integration-token configuration can complete their first V2-backed read without changing configuration or adding setup steps.
- **SC-004**: Integration and OAuth tokens with the same authorized scopes produce equivalent outcomes for all covered operations in the mocked acceptance suite.
- **SC-005**: Representative `401`, `403`, `404`, `409`, and `429` responses are classified correctly in 100% of migration acceptance cases, and every available service request identifier is retained.
- **SC-006**: Searches and block traversals spanning at least three service pages return exactly the expected item count and order, with zero duplicates and zero omissions.
- **SC-007**: Every currently supported block type passes the project-required bidirectional round-trip checks after migration, with zero newly undocumented lossy conversions.
- **SC-008**: Running the contract refresh and client regeneration twice against an unchanged V2 document produces zero changes on the second run.
- **SC-009**: Automated secret-safety checks and log assertions find zero bearer token values in logs, metrics, error messages, snapshots, or committed test data.
- **SC-010**: Page delete, page restore, and edits that remove blocks continue to pass their existing user-level acceptance journeys while being the only approved uses of V1.

## Assumptions

- The supplied OpenAPI document, titled `Buildin Developer API V2` and versioned `2.0.0`, is authoritative for this feature.
- Migration means moving all existing capabilities with published V2 equivalents to V2 while preserving existing user behavior; it does not mean exposing every new V2 endpoint.
- The supplied V2 contract has no page archive/restore operation, no block-delete operation, and no writable `in_trash` field. A temporary V1 compatibility path is therefore required to avoid silently removing existing Buildout features.
- A Buildin credential used for a compatibility operation is expected to remain valid for that V1 operation. If Buildin does not support that combination, Buildout reports the limitation rather than weakening or simulating the operation.
- Existing user-visible CLI and MCP contracts are stable migration constraints. Internal domain types may evolve as long as those contracts and semantic outcomes remain compatible.
- Tokens are provisioned outside Buildout. OAuth consent, refresh, revocation, and secure long-term token storage are separate features.
- The published production service remains `https://api.buildin.ai`; alternate service locations continue to use the project's established configuration rules.
- The V2-only semantic-search, page-move, page-property, database-mutation, Markdown-export, and file-transfer operations are candidates for later features, not part of this migration.
- Performance optimization beyond preventing pagination defects and regressions is out of scope; the migration retains existing performance expectations.
