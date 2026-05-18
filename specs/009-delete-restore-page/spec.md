# Feature Specification: Page Delete and Restore

**Feature Branch**: `009-delete-restore-page`
**Created**: 2026-05-18
**Status**: Draft
**Input**: User description: "prepare specification for delete function (page deletion). Explore also does OpenAPI spec provide method to restore deleted pages, if yes - include restore into this feature"

## OpenAPI Probe Result

The buildin.ai OpenAPI document (`openapi.json`) does **not** expose a
dedicated `DELETE /v1/pages/{page_id}` operation. Pages are archived and
restored through the same path: `PATCH /v1/pages/{page_id}` with
`UpdatePageRequest.archived` set to `true` (delete) or `false`
(restore). The `archived` field is writable on the request and
readable on the `Page` response, so restore is a first-class
capability of the API and **is included in this feature**. No new
client method is required in `Buildout.Core` — the existing
`IBuildinClient.UpdatePageAsync` covers both directions.

## User Scenarios & Testing *(mandatory)*

This feature completes the page-lifecycle surface area of buildout:
spec 002 produced read, spec 006 produced create, spec 008 produced
in-place edit, and this spec produces delete + restore.

The conceptual shift is that "delete" is **soft** — a server-side
archive that hides the page from normal browse/search but preserves
its blocks, comments, backlinks, and identity intact. Because the
operation is reversible by design, the feature MUST ship the
inverse (`restore`) alongside the primary (`delete`), so an LLM
agent that deletes a page in error has a clear, atomic undo path.

The two operations are exposed as **two separate MCP tools and two
separate CLI commands** (never a single tool with an `archived`
boolean parameter), because:

1. LLM agents reason more reliably about verb-named tools than
   about parameter-encoded state transitions.
2. The constitution's Principle VI (non-destructive editing) requires
   destructive operations to be impossible to invoke by accident; a
   shared tool whose meaning flips with a boolean is exactly the
   accident vector that principle exists to prevent.
3. Tool descriptions can be tuned independently — `delete_page` can
   emphasise reversibility and the existence of `restore_page`;
   `restore_page` does not need that disclaimer.

### User Story 1 — Delete (archive) a page (Priority: P1)

As a developer or LLM agent that has identified a buildin page to
remove from active circulation, I run `buildout delete <page_id>`
(or invoke the MCP `delete_page` tool with `{ "page_id": "<id>" }`)
and the page becomes archived: subsequent reads return it with
`archived: true`, and buildin's search and parent-listing surfaces
hide it from normal browse views per server semantics. The page's
blocks, comments, and backlinks are preserved; the page can be
recovered later via `restore_page` / `buildout restore`.

**Why this priority**: This is the primary user-requested capability.
Without it, the only way to remove a page from active use is to
delete every block individually (spec 008's `replace_block` does not
help — it preserves the page) or to do it manually through the
buildin UI. An LLM agent operating against buildin needs a single
verb for "make this page go away".

**Independent Test**: With a mocked buildin client whose
`UpdatePageAsync` records its calls, invoke the core
`DeletePageAsync(pageId)` for a page whose mock state has
`archived: false`. Assert: (a) exactly one call was made to
`UpdatePageAsync`, (b) the call's request body had `archived: true`
and **no other fields set** (properties, icon, cover all unset, so the
PATCH targets only the archive flag), (c) the returned `Page` has
`archived: true`, and (d) the call's `pageId` matches the input.

**Acceptance Scenarios**:

1. **Given** a page that exists and is currently active (`archived: false`),
   **When** an agent invokes `delete_page` with that page's ID,
   **Then** the response indicates success, the returned page has
   `archived: true`, and a follow-up `get_page_markdown` (or
   `getPage`) for the same ID returns the page with `archived: true`.
2. **Given** the same active page, **When** a developer runs
   `buildout delete <page_id>` from the terminal, **Then** the CLI
   exits with status 0 and emits a structured success line
   (machine-parseable: status + page_id + final archived state +
   `changed: true`).
3. **Given** a page that is already archived (`archived: true`),
   **When** an agent invokes `delete_page` for that page, **Then** the
   response indicates success (exit status 0 / MCP success), the
   returned page still has `archived: true`, and the response carries
   a `changed: false` indicator so the caller can distinguish a real
   transition from a no-op. The CLI MUST NOT exit with an error code
   for this case (idempotency contract).
4. **Given** a page ID that does not exist (or that the bot cannot
   access), **When** an agent invokes `delete_page`, **Then** the call
   fails with a *not found* / *permission denied* error class
   (distinct from each other and from generic transport errors) and
   no PATCH is issued against any other page.

---

### User Story 2 — Restore (un-archive) a previously deleted page (Priority: P1)

As a developer or LLM agent that has identified a previously archived
buildin page that needs to be returned to active circulation, I run
`buildout restore <page_id>` (or invoke the MCP `restore_page` tool
with `{ "page_id": "<id>" }`) and the page becomes active again:
subsequent reads return it with `archived: false` and buildin's
search/listing surfaces show it again per server semantics.

**Why this priority**: An "undo for delete" is part of what makes
soft-delete safe to expose to LLM agents at all. The constitution's
non-destructive-editing principle implies that any operation an LLM
can take by accident must have a documented, callable inverse;
this is that inverse. Ship together with delete or do not ship.

**Independent Test**: With a mocked buildin client whose
`UpdatePageAsync` records its calls, invoke the core
`RestorePageAsync(pageId)` for a page whose mock state has
`archived: true`. Assert: (a) exactly one call was made to
`UpdatePageAsync`, (b) the call's request body had `archived: false`
and no other fields set, (c) the returned `Page` has
`archived: false`, and (d) the call's `pageId` matches the input.

**Acceptance Scenarios**:

1. **Given** a page that exists and is archived (`archived: true`),
   **When** an agent invokes `restore_page` with that page's ID,
   **Then** the response indicates success, the returned page has
   `archived: false`, and a follow-up read for the same ID returns
   the page with `archived: false`.
2. **Given** the same archived page, **When** a developer runs
   `buildout restore <page_id>`, **Then** the CLI exits with
   status 0 and emits a structured success line (status + page_id +
   final archived state + `changed: true`).
3. **Given** a page that is already active (`archived: false`),
   **When** an agent invokes `restore_page` for that page, **Then**
   the response indicates success, the returned page still has
   `archived: false`, and the response carries `changed: false` to
   distinguish the no-op from a real transition.
4. **Given** a page ID that does not exist (or that the bot cannot
   access), **When** an agent invokes `restore_page`, **Then** the
   call fails with the same *not found* / *permission denied* error
   classes used by `delete_page`, so the caller's error-handling
   code is symmetric across the two operations.

---

### User Story 3 — Consistent shell scripting and chaining (Priority: P2)

As a developer chaining buildout commands in a shell script or as an
LLM agent chaining MCP tools in a workflow, I rely on
delete/restore returning machine-parseable outputs and stable error
classes so I can branch on them without parsing free-form text.

**Why this priority**: Spec 002 / 006 / 008 established a pattern of
machine-parseable CLI output and named error classes in the MCP
surface. Delete and restore become much less useful in
automation if they break that pattern; this story exists to
explicitly carry the convention forward.

**Independent Test**: With a mocked client, exercise the CLI's
`delete` and `restore` commands and parse their stdout as the
documented structured format. Confirm that the `changed`,
`archived`, `page_id`, and `error_class` (when applicable) fields
appear in the documented positions, and that the exit code is 0
for success and idempotent no-ops, non-zero for error classes.

**Acceptance Scenarios**:

1. **Given** a script that runs `buildout delete <id> && buildout restore <id>`,
   **When** the script executes against an active page, **Then** both
   commands exit 0, the first reports `changed: true`, the second
   reports `changed: true`, and the page ends up active again.
2. **Given** an MCP client that catches `error_class == "page_not_found"`
   from `delete_page`, **When** it then catches `error_class == "page_not_found"`
   from `restore_page` for the same ID, **Then** the error class
   string is byte-identical across the two tools (consistent
   error-class vocabulary).

---

### Edge Cases

- **Already-archived page deleted again**: succeed with `changed: false`.
  Never an error. Idempotency contract.
- **Already-active page restored again**: succeed with `changed: false`.
  Never an error. Idempotency contract.
- **Page ID not found**: error class `page_not_found` (consistent with
  the convention established in earlier specs); no retries.
- **Bot lacks edit permission**: error class `permission_denied` (or
  whatever the buildin client surfaces today for write authorisation
  failures — must match the existing CLI/MCP convention, not invent
  a new one); no retries; surfaced verbatim across both CLI and MCP.
- **Transient transport failure (5xx, timeout)**: surface as the same
  transport-error class the rest of the buildout surface uses; no
  built-in retry in this feature.
- **Database rows (a buildin "page" that is a row in a database)**:
  treated identically to a normal page — `PATCH /v1/pages/{page_id}`
  accepts both. No special branch in the feature.
- **Cascade behaviour to children**: out of scope at the feature level.
  This feature does not implement client-side cascade and does not
  describe child-page archival behaviour beyond "matches whatever
  buildin's server does when you PATCH the parent with
  `archived: true`". If buildin servers cascade, callers see
  cascade; if they don't, callers don't. The feature does not
  inspect children to verify.
- **A page concurrently being edited by spec 008's update tool**:
  delete wins or the underlying API returns its own conflict —
  the feature does not synchronise with spec 008's revision token.
  Restoration after such a delete simply un-archives; any
  pending update operations are the caller's responsibility.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `Buildout.Core` MUST expose a `DeletePageAsync(pageId, cancellationToken)`
  operation that resolves to a result type carrying the updated `Page`
  and a `Changed` boolean indicating whether the call moved the page
  from active to archived (`true`) or found it already archived
  (`false`).
- **FR-002**: `Buildout.Core` MUST expose a `RestorePageAsync(pageId, cancellationToken)`
  operation symmetric to FR-001, returning the updated `Page` and a
  `Changed` boolean indicating whether the call moved the page from
  archived to active.
- **FR-003**: Both operations MUST be implemented as a single call to
  `IBuildinClient.UpdatePageAsync` whose request body contains
  **only** the `archived` field set to the target value. No other
  field of `UpdatePageRequest` (properties, icon, cover) MAY be
  populated by either operation. Any other implementation (multiple
  calls, alternate endpoints, additional fields) is a defect.
- **FR-004**: Determination of `Changed` MUST come from observing the
  `archived` field on the page *before* the call (via `IBuildinClient.GetPageAsync`)
  and comparing to the target state. If the page is already in the
  target state, the operation MUST still succeed and MUST NOT issue
  the PATCH (no-op short-circuit), to avoid unnecessary write traffic
  against buildin and to keep the "no real change" path observable
  to spec 007's instrumentation as a separate code path.
- **FR-005**: The MCP server MUST expose **two distinct tools**:
  `delete_page` (input `{ page_id: string }`) and `restore_page`
  (input `{ page_id: string }`). Each tool's `description` MUST
  state the operation it performs and MUST cross-reference the
  inverse tool (`delete_page` mentions `restore_page`, and vice
  versa). The `delete_page` description MUST explicitly state that
  the operation is reversible and that block contents are preserved.
- **FR-006**: The MCP server MUST NOT expose a unified tool that
  accepts an `archived` boolean. Toggling state through a parameter
  is forbidden by this feature (see User Scenarios rationale and
  constitution Principle VI).
- **FR-007**: The CLI MUST expose two commands using
  `Spectre.Console.Cli`: `buildout delete <page_id>` and
  `buildout restore <page_id>`. Each accepts exactly one positional
  argument (the page ID) and no required flags.
- **FR-008**: Both CLI commands MUST emit structured output suitable
  for shell scripting: at minimum the page ID, the final `archived`
  state, the `changed` flag, and (on error) an `error_class` field.
  The exact serialisation (single line vs. block vs. JSON) is a
  `/speckit-plan` decision aligned with the structured-output
  convention already used by `buildout create`, `buildout update`,
  and `buildout search`.
- **FR-009**: Both CLI commands MUST exit with status 0 on success
  including idempotent no-ops, and with a non-zero status keyed to
  the error class on failure. Error-class-to-exit-code mapping MUST
  match whatever earlier specs already established (it is not a
  new vocabulary).
- **FR-010**: Both operations MUST flow through the per-call
  instrumentation defined in spec 007 (one span per call, one
  error-tagged log entry per failure, error class as a span/log
  attribute). This feature does not introduce new metric names or
  span attributes beyond what spec 007 already emits — the existing
  `page_not_found`, `permission_denied`, and transport-error classes
  are reused.
- **FR-011**: On error from the buildin API (any 4xx/5xx), the
  operations MUST surface the error in the existing buildout error
  taxonomy without retries. Delete and restore are not transient
  operations; a caller hitting a 5xx is expected to retry at its
  own layer if it wants to.
- **FR-012**: No interactive confirmation prompt MAY be added to
  the CLI commands. Destructive intent is communicated by the
  command verb (`delete`) and by the documented existence of
  `restore`. Adding `--force` or an interactive `yes/no` would
  break shell automation and is explicitly out of scope.
- **FR-013**: Both operations MUST be safe to invoke against a page
  whose type is a database row (in buildin's data model, a database
  row *is* a page). The implementation MUST NOT branch on page kind
  before issuing the PATCH.

### Key Entities

- **Page Reference**: the page ID (UUID) — the only input either
  operation accepts.
- **Archive State**: a boolean on the buildin `Page` model
  (`archived`), readable on every page response and writable on
  every `UpdatePageRequest`. The entire feature is a controlled
  flip of this boolean.
- **Operation Result**: a small value type carrying the updated
  `Page` and a `Changed: bool` indicator. Surfaced to MCP as JSON,
  to CLI as a structured stdout line, and to in-process callers
  as a typed record.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An LLM agent can delete a page it has just listed via
  `searchPages` (or read via `get_page_markdown`) using exactly one
  tool call, and a follow-up read of the same page returns
  `archived: true` — verified end-to-end in
  `Buildout.IntegrationTests` against the mocked buildin client.
- **SC-002**: An LLM agent can restore a page it previously deleted
  using exactly one tool call, and a follow-up read returns
  `archived: false` — verified end-to-end in
  `Buildout.IntegrationTests`.
- **SC-003**: Deleting a page that is already archived (or restoring
  a page that is already active) does **not** issue a buildin PATCH
  call and does **not** fail; observable in unit tests that count
  outgoing client calls and assert exactly zero.
- **SC-004**: A shell script chaining `buildout delete` and
  `buildout restore` against the same page completes both commands
  with exit status 0 and parseable output, and the page ends up in
  the same archived state as before the script started — verified
  by a CLI integration test.
- **SC-005**: A caller observing the documented error classes
  (`page_not_found`, `permission_denied`, transport class) can
  distinguish them programmatically without parsing free-form
  text — verified by tests that assert on `error_class` strings,
  not on message bodies.
- **SC-006**: Adding either tool to MCP increases the tool-listing
  count by exactly one per tool (two total), with descriptions that
  pass spec 007's MCP integration test (the cheap testing LLM
  correctly selects `delete_page` for a "delete this page" prompt
  and `restore_page` for "undo the delete", measured over a small
  benchmark of prompts).

## Assumptions

- The buildin.ai API's archive semantics (cascade to children,
  hiding from search, preservation of comments/backlinks/IDs) match
  the Notion-family convention this team already designs against.
  This feature does not validate or enforce server-side cascade.
- Spec 007's per-call instrumentation already covers the
  `IBuildinClient.UpdatePageAsync` call path. No new spans, metrics,
  or log attribute names are introduced.
- The existing CLI structured-output convention used by
  `buildout create` / `update` / `search` is the same convention
  this feature adopts. The exact format is reused, not re-decided.
- The error-class vocabulary (`page_not_found`, `permission_denied`,
  transport class, etc.) already exists across spec 002 / 006 / 008.
  This feature reuses those names verbatim.
- The current `IBuildinClient.UpdatePageAsync` accepts a request
  body in which all fields are optional, so issuing a PATCH that
  sets only `archived` does not blank out properties, icon, or
  cover. This is the behaviour of buildin's PATCH endpoint and is
  taken as given.
- Hard delete (permanent removal, not archive) is **not** in scope
  because the buildin OpenAPI does not expose it. If a future
  buildin API version adds such an endpoint, a separate spec will
  be needed.
- Bulk delete/restore (multiple page IDs in one call) is **not** in
  scope. Callers wanting bulk behaviour invoke the tool / CLI
  command in a loop.
- Page properties such as icon, cover, and title are out of scope
  for any operation in this feature — they are not touched on
  delete or restore.
