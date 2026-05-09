# Feature Specification: Database Views (Read-Only)

**Feature Branch**: `005-database-views`
**Created**: 2026-05-09
**Status**: Draft
**Input**: User description: "Add support to CLI/MCP for database views (read-only)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Render a Database as a Table View (Priority: P1)

As a buildin user working in a terminal, I want to read a database and see its
rows rendered as a table in plain text/markdown, so I can review my data
without leaving the shell or opening a browser.

**Why this priority**: Table is the default and most-used view shape. Without
it the feature has no minimum-viable surface; every other view type can be
deferred. It also exercises the full pipeline (database fetch + paginated row
query + property formatting + plain output) end-to-end.

**Independent Test**: Run `buildout db view <database_id>` against a fixture
database with three rows and three properties. Verify the standard output is
a markdown pipe-table with one header row and three body rows, ordered as
returned by the query endpoint, and no terminal escape codes are present
when piped.

**Acceptance Scenarios**:

1. **Given** a valid database id and an authenticated client, **When** the
   user runs the table view command, **Then** the output begins with a
   header line `# <database title> — table view`, followed by a markdown
   pipe-table whose columns match the database's visible properties and
   whose rows match every page returned across all paginated query
   responses.
2. **Given** a database with more rows than fit in a single query response,
   **When** the view is rendered, **Then** the rendering includes every row
   exactly once (no duplicates, no omissions) by following pagination
   cursors to exhaustion before output begins.
3. **Given** the user pipes the command to a file (non-TTY), **When** the
   view is rendered, **Then** the output contains no ANSI escape codes and
   is byte-identical to the body returned by the equivalent MCP call.
4. **Given** a database id that does not exist, **When** the user runs the
   command, **Then** the process exits with the same error class and exit
   code already used for missing pages (404 → "database not found", exit 3).

---

### User Story 2 - Choose Among Multiple View Styles (Priority: P2)

As a user, I want to render the same database under different view styles
(board, gallery, list, calendar, timeline) by passing a flag, so I can pick
the shape that best matches the data without a second tool.

**Why this priority**: Once the table pipeline works, the additional view
styles are formatting variants on the same query result — useful but not
required for an initial release. Each style is independently shippable.

**Independent Test**: For each non-table style, run
`buildout db view <database_id> --style <style>` against a fixture suited
to that style and verify the documented rendering rules (see Key Entities
→ View Style) hold, and that output remains plain text with no escape
codes when redirected.

**Acceptance Scenarios**:

1. **Given** `--style board` and a property to group by, **When** the view
   renders, **Then** rows are partitioned by that property's value and each
   group is shown as a labeled section with the rows under it; if three or
   fewer non-empty groups exist, the renderer MAY use side-by-side ASCII
   columns; otherwise it MUST use stacked sections to keep output legible
   in narrow terminals.
2. **Given** `--style gallery`, **When** the view renders, **Then** each
   row is shown as a card block with its title and up to three additional
   properties; cover images are represented as a textual placeholder, not
   downloaded.
3. **Given** `--style list`, **When** the view renders, **Then** each row is
   shown as a single bulleted line with the title and a parenthesized
   summary of selected properties.
4. **Given** `--style calendar` or `--style timeline` and a date property
   identifier, **When** the view renders, **Then** rows are grouped under
   date (or date-range) headings sorted ascending, since a month-grid
   layout is not legible in a terminal.
5. **Given** an unknown `--style` value, **When** the user runs the
   command, **Then** the process exits with the validation error code (2)
   and lists the supported styles.

---

### User Story 3 - Same Operation Available Over MCP (Priority: P2)

As an MCP client (e.g., an editor agent), I want to invoke the same
database-view rendering as a tool call and receive the rendered text, so my
agent can read databases the way the CLI shows them.

**Why this priority**: Parity with CLI is a project invariant (Principle:
shared core, both surfaces). It is P2 because it depends on the rendering
work in P1/P2 above, not because it is optional.

**Independent Test**: Invoke the MCP tool `database_view` with a database
id (and optional style/group/date arguments) against the WireMock-based
buildin mock server. Verify the returned text is byte-identical to the
plain-mode CLI output for the same arguments.

**Acceptance Scenarios**:

1. **Given** a registered MCP server, **When** a client lists tools,
   **Then** a `database_view` tool is advertised with arguments for
   database id, style, optional group-by property name, and optional
   date property name.
2. **Given** a successful MCP call, **When** the response is returned,
   **Then** its body is byte-identical to the plain-mode CLI output for
   the same arguments and is not styled with terminal codes.
3. **Given** a buildin error (404, 401/403, transport, or other API
   error), **When** the MCP tool runs, **Then** the error is mapped to
   the same MCP error code already used by the page-read and search
   tools for that error class.

---

### User Story 4 - Embedded Databases Render Inline When Reading a Page (Priority: P3)

As a user reading a page that has a database embedded inside it (a
"child database" block in buildin's page contents), I want the page
read to expand that database inline as a view, so I see the page's
substantive content in one output instead of a placeholder I have to
chase with a second command.

**Why this priority**: Useful but secondary. Standalone `db view`
already covers the explicit case; inline expansion is an ergonomic
improvement for `get`. P3 because it depends on the renderer from
P1 and on the existing page-read pipeline, and because pages without
embedded databases are unaffected.

**Independent Test**: Read (via CLI `get` and via the MCP page
resource) a fixture page that contains exactly one `child_database`
block. Verify the rendered markdown contains the embedded database's
table-style rendering inline at the position of the `child_database`
block, and that pages with no `child_database` blocks render
identically to before this feature.

**Acceptance Scenarios**:

1. **Given** a page containing one or more `child_database` blocks,
   **When** the page is read, **Then** each such block is replaced in
   the output by the embedded database's table-style rendering,
   prefixed by a sub-section heading carrying the database's title
   and preserving the page's surrounding block order.
2. **Given** a page with no `child_database` blocks, **When** the
   page is read, **Then** the output is byte-identical to what the
   page-read produced before this feature (no regressions on the
   common case).
3. **Given** a `child_database` block whose target database the
   credential cannot read (404, 401, 403, transport error), **When**
   the page is read, **Then** the page render does NOT abort — a
   single-line placeholder is substituted at that block's position
   identifying the failure class (e.g. `[child database: not found
   — <title>]` for 404, `[child database: access denied — <title>]`
   for 401/403; full mapping in `data-model.md`), and surrounding
   content renders normally.
4. **Given** an embedded database with many rows, **When** it is
   rendered inline, **Then** the renderer paginates rows to
   exhaustion (same correctness rule as standalone), and the
   embedded rendering uses the table style only — no `--style`,
   `--group-by`, or `--date-property` options apply to inline
   expansions in this version.
5. **Given** a database whose rows are pages that themselves contain
   `child_database` blocks, **When** the outer page is read,
   **Then** only the outer page's `child_database` blocks are
   expanded — the rows of the embedded database are NOT recursively
   expanded into their own embedded databases. Recursion is one
   level deep.

---

### Edge Cases

- **Empty database (zero rows)**: Render the metadata header and an empty
  state line (e.g. `(no rows)`) instead of a table with only headers.
- **Database with no visible non-title properties**: Table view degrades
  to a single-column listing of titles; other styles fall back to the
  list style.
- **Very wide rows (many columns or long values)**: Property values are
  truncated to a documented per-cell character budget with a trailing
  ellipsis; the full untruncated query result is not echoed. If the
  number of selected columns would produce a table wider than a
  documented soft limit, the renderer switches to a stacked per-row
  layout.
- **Group-by property missing from a row**: That row is placed in a
  group named `(none)` rather than being dropped.
- **Date-property missing from a row in calendar/timeline view**: That
  row is grouped under a `(undated)` heading at the end.
- **Property types that cannot be represented inline** (files,
  relations, rollups, formulas with rich content): Rendered as a short
  placeholder (e.g. file count, related-id count) rather than the raw
  payload.
- **Pagination interrupted mid-stream** (transport error after some
  pages): No partial output is emitted; the command exits with the
  transport-error class so the user does not act on truncated data.
- **Auth failure (401/403)**: Same exit/MCP error class as existing
  features; never silently render an empty result.
- **Unknown `--style` or unknown group-by/date property name**:
  Validation error before any network call, with a message listing
  valid options derived from the database schema where applicable.
- **Page with multiple `child_database` blocks**: Each is fetched
  and rendered independently in document order; one failing
  expansion does not affect the others.
- **`child_database` block whose database the credential cannot
  read**: Replaced by a single-line placeholder, never causes the
  page render to fail.
- **Page contains a `child_database` block with no embedded id /
  malformed payload**: Same placeholder behavior as above; the
  surrounding page renders normally.
- **Database mention in page text** (existing
  `DatabaseMentionConverter`): Continues to render as a reference,
  unchanged. Mentions are not expanded by this feature; only the
  `child_database` block type is.

## Requirements *(mandatory)*

### Functional Requirements

#### Core / Rendering

- **FR-001**: The system MUST expose a single read-only operation that
  takes a database identifier (and optional view-style parameters) and
  returns a fully rendered text representation, with no separate
  configuration step required.
- **FR-002**: The system MUST fully paginate the database's row query
  before producing any output, so the rendered view reflects every row
  visible to the calling credential.
- **FR-003**: The system MUST support the following view styles:
  `table`, `board`, `gallery`, `list`, `calendar`, `timeline`. `table`
  MUST be the default when no style is specified.
- **FR-004**: For each style, the system MUST render output that is
  legible in a 80-column terminal: long values are truncated with an
  ellipsis, very wide tables degrade to a stacked layout, and boards
  with more than three non-empty groups render sequentially rather than
  side-by-side.
- **FR-005**: The rendered output MUST begin with a metadata header
  identifying the database title, the style in use, and any active
  parameters (group-by property, date property), so a piped reader can
  understand what was rendered without prior context.
- **FR-006**: The system MUST format property values per their
  declared property type (title, text, number, date, select, multi-
  select, checkbox, url, people, files, relation, rollup, formula,
  created/updated time), substituting a documented placeholder for
  property types whose contents cannot be shown inline.
- **FR-007**: The operation MUST be strictly read-only: it MUST NOT
  create, modify, archive, or delete any database, page, block, or
  property under any code path.

#### CLI Surface

- **FR-008**: The CLI MUST expose this operation as a command following
  the existing argument-then-options style used by `get` and `search`,
  taking a positional database identifier and accepting at least:
  a `--style` option, a `--group-by` option for board view, and a
  `--date-property` option for calendar and timeline views.
- **FR-009**: When standard output is a terminal, the CLI MAY apply
  terminal styling consistent with how page-read and search currently
  style their output; when standard output is not a terminal, the CLI
  MUST emit only plain text with no escape codes.
- **FR-010**: Validation errors (unknown style, unknown property name,
  missing required argument) MUST cause the process to exit with the
  validation-error exit code already in use, before any network call,
  with a message naming the offending input and the valid alternatives.

#### MCP Surface

- **FR-011**: The MCP server MUST expose the same operation as a tool
  whose arguments correspond one-to-one with the CLI's positional
  argument and options.
- **FR-012**: The MCP tool body MUST be byte-identical to the CLI's
  plain (non-TTY) output for the same inputs.
- **FR-013**: Buildin API errors (404, 401/403, transport, generic)
  MUST be mapped to the same MCP error codes already used by existing
  read-only tools.

#### Page-Read Integration

- **FR-016**: The page-read pipeline (used by both the CLI `get`
  command and the MCP page resource) MUST recognize `child_database`
  blocks in a page's contents and replace each one in the rendered
  markdown with the embedded database's table-style rendering,
  preserving the surrounding block order.
- **FR-017**: Inline expansion MUST use the table style with default
  parameters; it MUST NOT introduce new flags or arguments on `get`
  or on the page resource. The `db view` command and the
  `database_view` MCP tool remain the only surfaces that accept
  view-style parameters.
- **FR-018**: A failure to render an embedded database (404, 401/403,
  transport, malformed block payload) MUST NOT abort the page render;
  a single-line placeholder identifying the failure class MUST be
  emitted in place of that block, and surrounding content MUST render
  unchanged.
- **FR-019**: Inline expansion MUST be exactly one level deep: the
  rows of an embedded database are NOT recursively expanded into
  their own embedded databases.
- **FR-020**: Pages with no `child_database` blocks MUST produce
  byte-identical output to the page-read behavior that existed
  before this feature (no regression on the common case).
- **FR-021**: Database mentions inside page text MUST continue to
  render as references via the existing mention converter; this
  feature MUST NOT change mention rendering.

#### Cross-cutting

- **FR-014**: The operation MUST NOT make any real network call to
  buildin during automated tests; existing mock-server discipline
  applies unchanged.
- **FR-015**: The implementation MUST share a single core renderer
  between CLI and MCP; neither surface is permitted to re-implement
  view rendering. The same renderer MUST be reused by the inline
  expansion in FR-016.

### Key Entities

- **Database**: An existing buildin entity with a title, a property
  schema (a named, typed set of columns), and a queryable set of rows
  (each row being a page with property values matching the schema).
  Already modeled in the project; this feature consumes it unchanged.
- **Row**: A page returned by the database query operation. Carries
  values for the database's properties.
- **View Style**: A render shape applied to the database's rows.
  Defined entirely client-side by this feature; it is *not* a stored
  buildin entity. Six styles are in scope:
  - **table** — pipe-table; columns are visible properties; rows are
    database rows; degrades to stacked-per-row when too wide.
  - **board** — partitioned by a group-by property; up to three
    non-empty groups laid out side-by-side as ASCII columns,
    otherwise stacked sections.
  - **gallery** — one card block per row; cover image rendered as a
    textual placeholder; up to three secondary properties shown.
  - **list** — one bulleted line per row with title and a short
    property summary.
  - **calendar** — rows grouped under date headings derived from a
    date property; ascending order.
  - **timeline** — rows grouped by start date with their date range
    and duration; ascending order.
- **Render Parameters**: The user-supplied controls that pick a style
  and tell it what property to group/sort by. Validated before any
  network call.
- **Child Database Block**: A buildin block that embeds a database
  inside a page's contents. Carries the embedded database's id (and
  in the buildin payload typically a denormalized title). Rendered
  by this feature as an inline table-style view at the block's
  position; never expanded recursively.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user who knows a database id can render that database
  as a table in a single shell command, with no preceding configuration
  step, and pipe the output to another tool without seeing terminal
  escape codes.
- **SC-002**: For each of the six view styles, there is at least one
  fixture-based test that asserts the rendered output exactly matches a
  golden file, and that test does not depend on any real buildin
  network call.
- **SC-003**: For the same arguments and the same fixture, the CLI's
  plain-mode output and the MCP tool body are byte-identical (verified
  by an automated test).
- **SC-004**: A database whose rows do not fit in a single query
  response renders all rows in the output (verified by a fixture that
  forces at least one cursor follow-through).
- **SC-005**: All four already-modeled error classes (not-found,
  unauthorized, transport, generic) surface through the new command
  and the new MCP tool with the same exit codes / MCP error codes used
  by existing read-only features (verified by tests).
- **SC-006**: The command refuses with a validation error — and never
  issues a network call — when given an unknown style, an unknown
  group-by property, or an unknown date property.
- **SC-007**: The view operation cannot mutate buildin state under any
  input combination (verified by a contract test asserting only GET /
  POST-query endpoints are touched).
- **SC-008**: A page that contains one or more `child_database` blocks
  is rendered (via both the CLI `get` command and the MCP page
  resource) with each `child_database` block replaced by the embedded
  database's table-style rendering, verified by a fixture-based
  integration test.
- **SC-009**: A page that contains a `child_database` block whose
  target database returns 404/401/403/transport-error renders
  successfully with a single-line placeholder at the block's
  position; the surrounding page content matches a golden fixture
  byte-for-byte.
- **SC-010**: A page that contains zero `child_database` blocks
  produces output byte-identical to the page-read output produced
  before this feature lands (no-regression test pinned by golden
  fixture).

## Assumptions

- **Views are rendered client-side, not fetched.** The buildin OpenAPI
  contract included in this repository does not define any "view"
  resource, schema, or endpoint; it exposes databases, the database
  query operation, pages, blocks, search, and current user. This
  feature therefore treats *view style* as a purely client-side render
  shape applied to the rows returned by the existing database query
  endpoint. If buildin later publishes a server-side view API, the
  shape and naming used here may need to be revisited; that revisit is
  out of scope for this feature.
- **Property visibility, ordering, and column widths are decided by
  the renderer**, not by a stored per-database view configuration,
  since none is exposed by the API. Reasonable defaults are: include
  every property in the database's schema, place the title property
  first, hide property types whose contents cannot be shown inline
  unless explicitly relevant to the chosen style.
- **Filtering and sorting follow whatever the existing database query
  endpoint supports** (pagination + creation/update timestamp
  filters). No new client-side filter language is introduced by this
  feature.
- **CLI styling rules are inherited unchanged** from the existing
  page-read and search features (TTY detection, NO_COLOR honored, no
  styling on non-TTY output), and the MCP body is always plain.
- **The shared core renderer is the single source of truth**; CLI and
  MCP are thin adapters that select TTY styling on/off. This mirrors
  the pattern already established for page reading and search.
- **Integration tests run against the WireMock-based buildin mock**
  introduced by feature 004; this feature adds new stubs for any
  database/query payload shapes not already covered, but does not
  change the mocking strategy.
- **No changes to the buildin client interface signature** beyond what
  is already needed to read databases and query rows; the feature does
  not require new buildin client methods.
- **A `ChildDatabaseBlock` model is introduced** in `Buildout.Core`
  alongside the existing block types (paragraph, heading, list,
  todo, code, quote, divider, image, embed, table). Its
  discriminator value follows buildin's payload (`child_database`).
  This is a new model, not a new client method, and is required to
  support inline expansion (FR-016).
- **Inline expansion fans out to the same buildin endpoints
  (`GET /v1/databases/{id}` and `POST /v1/databases/{id}/query`)**
  per `child_database` block found. The read-only invariant
  (SC-007) holds unchanged: those are the only endpoints the
  rendering code path may touch.
- **Recursion depth is fixed at one** for inline expansion. Pages
  embedded as rows of an embedded database are not recursively
  expanded. Removing this cap requires a follow-up feature.
