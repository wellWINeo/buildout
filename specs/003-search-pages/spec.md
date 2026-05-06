# Feature Specification: Page Search

**Feature Branch**: `003-search-pages`
**Created**: 2026-05-06
**Status**: Draft
**Input**: User description: "Search function has 2 args: `query` (search query string) and `page_id` (nullable UUID — where to search; if null, search everywhere). CLI usage: `buildout search <query> --page <page_id>`. To implement searching, use the search endpoint from the Building API."

## User Scenarios & Testing *(mandatory)*

This feature delivers the second user-visible read capability of buildout: locating
buildin pages whose content matches a query, so a user (human via CLI, LLM via MCP)
can discover pages they then read with the existing `buildout get` / `buildin://`
surface from feature 002. Search is the find-step that pairs with read.

### User Story 1 — CLI: search across all accessible pages (Priority: P1)

As a developer with a buildin token, I run `buildout search "<query>"` and see a
ranked list of pages whose content matches my query. Each result shows enough
information (title and page ID at minimum) for me to identify the page I want
and pass its ID to `buildout get` to read it.

**Why this priority**: This is the smallest end-to-end slice that exercises a
new core search operation, the buildin client's search endpoint, and a
presentation surface. It is independently shippable and demonstrable to a human
in seconds — and it composes immediately with the read operation from feature
002, so the pair "search then read" works end-to-end.

**Independent Test**: With a mocked buildin client returning a fixed list of
pages for a known query, run `buildout search "<query>"` (a) into a TTY and
observe a styled list of matches with titles and IDs, (b) piped to a file and
observe a plain-text equivalent. The piped output MUST be parseable by simple
line-oriented tools (one match per line, or a structured form documented in
the contracts).

**Acceptance Scenarios**:

1. **Given** a buildin workspace containing several pages, one of whose title
   or body contains the literal string `<query>`, **When** the developer runs
   `buildout search "<query>"` with stdout connected to a styled terminal,
   **Then** the matching page appears in the output with its title and page
   ID, formatted with terminal styling, and the process exits 0.
2. **Given** the same workspace, **When** the developer runs
   `buildout search "<query>"` with stdout redirected to a file or pipe (no
   TTY), **Then** the file contains plain text (zero terminal escape codes)
   listing each match's title and page ID in a stable, documented format,
   and the process exits 0.
3. **Given** a query that matches no pages, **When** the developer runs
   `buildout search "<query>"`, **Then** the command writes a single
   human-readable "no matches" line to stdout (or empty body in plain mode),
   exits 0, and does NOT exit non-zero — "no matches" is a successful
   outcome, not an error.
4. **Given** an authentication / authorisation / transport failure, **When**
   the developer runs `buildout search`, **Then** the command writes a
   human-readable error to stderr identifying the failure class (auth,
   transport, unexpected) and exits with the same non-zero status used by
   `buildout get` for the corresponding failure class — failure-class exit
   codes MUST be consistent across commands.

---

### User Story 2 — MCP: expose search as an MCP tool (Priority: P1)

As an LLM connected to the buildout MCP server, I invoke the `search` tool
with a query (and optionally a `page_id` to scope) and receive a list of
matching pages I can then read via the existing `buildin://{page_id}`
resource. This lets me find pages by content rather than only by ID.

**Why this priority**: Discovery is a first-class LLM operation. Without
search, an LLM consuming buildout's MCP server can read pages it already
knows the ID of, but cannot find new ones — every interactive workflow that
starts from a question ("which page describes X?") fails. Pairing search
(this feature) with read (feature 002) is the smallest useful read-side
toolset for the MCP product.

**Independent Test**: Start the MCP server against a mocked buildin client
returning a fixed search result set. From a test client, list tools and
discover the `search` tool. Invoke it with a query; assert the result lists
the expected matches with title and page ID. Invoke it with both `query`
and `page_id`; assert results are restricted to descendants of `page_id`.
The cheap-LLM integration test (already present from feature 002) MUST be
extended to demonstrate the LLM successfully chains search → read against
the mocked workspace.

**Acceptance Scenarios**:

1. **Given** the MCP server is running and connected to a working buildin
   client, **When** an MCP client lists tools, **Then** the listing
   advertises a `search` tool whose description identifies it as
   "search buildin pages by query" and whose input schema declares
   `query` (string, required) and `page_id` (string, optional).
2. **Given** the `search` tool, **When** an MCP client invokes it with a
   query that matches several pages, **Then** the response is a single
   tool-result content block listing each match's title and page ID in a
   stable format that the same LLM can parse without prose hedging — the
   format MUST be byte-identical to the CLI's plain-mode output for the
   same query (see FR-014).
3. **Given** the `search` tool, **When** an MCP client invokes it with a
   query that matches nothing, **Then** the response is a successful
   tool result whose body indicates "no matches" — NOT an MCP error.
4. **Given** an authentication / authorisation / transport failure,
   **When** an MCP client invokes the `search` tool, **Then** the server
   returns an MCP-protocol error (not a successful tool result with an
   error blob in the body) whose message identifies the failure class.

---

### User Story 3 — Scoped search via `--page` / `page_id` (Priority: P2)

As a developer working in a large workspace, I run
`buildout search "<query>" --page <page_id>` to restrict matches to a
single page's subtree, so I can find content I know exists in a specific
area without wading through unrelated matches elsewhere. The same scoping
is available to the LLM through the MCP tool's optional `page_id`
argument.

**Why this priority**: The flag is part of the user's stated CLI surface,
but unscoped search (US1/US2) is itself useful and shippable; scoping is
an ergonomic refinement that becomes valuable as the workspace grows. It
is independently testable and ships in the same release without blocking
US1.

**Independent Test**: With a mocked buildin client returning a fixed
result set whose pages have known parent chains, run
`buildout search "<query>" --page <root_id>` where `<root_id>` is the
ancestor of exactly some of those pages. Assert that only the descendant
pages appear in the output, in the same order they appeared in the
unscoped result. Repeat through the MCP tool with the same `page_id`;
assert identical filtering.

**Acceptance Scenarios**:

1. **Given** a workspace with pages A, B, C where B and C are descendants
   of A and several other pages are not, **When** the developer runs
   `buildout search "<query>" --page <A_id>` and the unscoped query
   would match A, B, C, and one unrelated page, **Then** the scoped
   output contains exactly A, B, and C (in the same relative order) and
   excludes the unrelated page.
2. **Given** a `page_id` whose page exists but has no descendants
   matching the query, **When** the developer runs the scoped search,
   **Then** the output is the "no matches" form described in US1 — exit
   0, no error.
3. **Given** a `page_id` that does not exist or is unauthorised, **When**
   the developer runs the scoped search, **Then** the command surfaces
   the same failure class it would for `buildout get <page_id>` against
   the same ID (see feature 002 FR-009), with the same exit code.

---

### Edge Cases

- **Empty query**: An empty or whitespace-only `<query>` is rejected at
  the CLI / MCP boundary with a clear validation error before any
  buildin call is made — both surfaces return a usage / invalid-arguments
  error rather than calling buildin with an empty query.
- **Query containing markdown / shell metacharacters**: The query is
  treated as opaque text; the system does not interpret asterisks,
  backticks, pipes, etc. The CLI relies on the user's shell to quote;
  no extra unescaping is performed.
- **Unicode / RTL / emoji query**: Queries in any Unicode script pass
  through to the buildin search endpoint unchanged; results are
  rendered unchanged; no normalisation that would alter user-visible
  characters.
- **No matches**: A successful outcome (exit 0 / non-error MCP tool
  result) with an explicit "no matches" indicator. Never a silent empty
  output that could be confused with a transport failure.
- **Many matches / pagination**: When buildin's search response
  paginates (`next_cursor`), the core operation walks every cursor to
  exhaustion before returning, mirroring the read operation's
  "correctness over throughput" stance from feature 002. v1 introduces
  no result cap, no streaming output, no caching.
- **Archived pages**: Archived pages are excluded from results by
  default. An opt-in flag to include archived pages is out of scope for
  v1 and is a future, additive change.
- **Page vs database results**: The buildin search endpoint may return
  both page-shaped and database-shaped results. v1 treats every result
  uniformly as "a thing with a title and an ID"; the result format
  includes the buildin object type so callers can tell them apart, but
  no behavioural branching beyond that exists in this feature.
- **Result with no title**: A page or database whose title is empty is
  rendered with a clearly-marked placeholder (e.g. `(untitled)`), so
  the result line is never just an ID.
- **Authentication failure**: Same handling as feature 002 — distinct
  failure-class exit code on the CLI; MCP-protocol error from MCP.
- **Transport failure mid-pagination**: A failure on any cursor page
  beyond the first surfaces as a transport failure for the whole
  search; partial results are not returned, since callers cannot tell
  partial from complete in a meaningful way.
- **Large result format on TTY**: Many matches printed to a small TTY
  rely on the user's pager / terminal scrollback; the CLI does not
  paginate output internally, does not invoke `less`, and does not
  attempt to fit results to terminal height.

## Requirements *(mandatory)*

### Functional Requirements

#### Core search (shared by both surfaces)

- **FR-001**: The shared core library MUST expose a single search
  operation that takes (a) a non-empty query string and (b) an optional
  page-scope identifier, and returns an ordered list of matches. Both
  presentation surfaces MUST go through this operation; neither may
  call buildin's search endpoint directly nor reimplement result
  formatting.
- **FR-002**: The core search operation MUST paginate the underlying
  buildin search response (`next_cursor`) to exhaustion before
  returning, mirroring feature 002's pagination stance. Performance
  optimizations (concurrent fetching, streaming output, response
  caching, partial rendering, max-result caps) are explicitly out of
  scope for v1.
- **FR-003**: When the page-scope identifier is absent (CLI flag
  unspecified, MCP `page_id` field omitted or null), the search MUST
  return every match buildin would return for that query, in buildin's
  ranking order, modulo the archived-page exclusion in FR-007.
- **FR-004**: When the page-scope identifier is provided, the returned
  list MUST contain only matches that are either (a) the scope page
  itself, or (b) descendants of the scope page in the buildin parent
  hierarchy. The relative order from buildin's response MUST be
  preserved for the matches that survive the filter.
- **FR-005**: Each match MUST include, at minimum: page ID,
  displayable title (with an explicit "(untitled)" placeholder when
  empty), and the buildin object type (page / database). The match
  shape MAY include additional fields (last-edited timestamp, parent
  ID, archived flag) if buildin returns them and those fields are
  useful to callers; new per-match fields are additive and do not
  break the contract.
- **FR-006**: Buildin-internal noise (opaque non-page IDs, raw
  property blobs, internal metadata not enumerated in FR-005) MUST NOT
  appear in the rendered output of either presentation surface, in
  line with constitution Principle II.
- **FR-007**: Archived pages MUST be excluded from results by default.
  A future opt-in is permitted but not in scope for v1.
- **FR-008**: A query that is empty or contains only whitespace MUST
  be rejected at both presentation surfaces before any buildin call is
  made, with a clear "query required / non-empty" validation error.

#### CLI surface

- **FR-009**: The CLI MUST expose a `search <query>` command that
  takes the query as a single positional argument and accepts an
  optional `--page <page_id>` flag carrying the page-scope identifier.
- **FR-010**: The CLI MUST detect at runtime whether stdout is a
  styled terminal and render results accordingly, mirroring the
  TTY-detection rules from feature 002 (FR-007 of feature 002):
  styled list when stdout is a styled terminal; plain-text rendering
  with zero terminal escape codes when stdout is a pipe / redirect /
  `NO_COLOR` / non-styled terminal.
- **FR-011**: The CLI's plain-mode output MUST be a stable,
  line-oriented format with one match per output unit, suitable for
  piping to `grep`, `head`, `awk`, etc. The exact line format is a
  contract decision deferred to `/speckit-plan`, but two requirements
  are fixed: each line MUST contain the page ID and title, and the
  format MUST be byte-identical to the body the MCP search tool
  returns for the same query (see FR-014).
- **FR-012**: The CLI MUST distinguish, in its exit code and stderr
  output, the same failure classes feature 002 distinguishes — page
  not found (relevant to scoped search), authentication /
  authorisation, transport failure, unexpected — using the same exit
  codes feature 002 documented. "No matches" is NOT a failure class;
  it exits 0.

#### MCP surface

- **FR-013**: The MCP server MUST advertise a `search` tool whose
  input schema declares `query` (string, required, non-empty) and
  `page_id` (string, optional) and whose description identifies it as
  a buildin page search by query. The tool MUST be discoverable in
  standard MCP tool listing.
- **FR-014**: A successful invocation MUST return a single tool-result
  content block whose text body is byte-identical to the CLI's
  plain-mode output for the same arguments (FR-011). This shared body
  is what an LLM reads and what a human pipes to disk; the two MUST
  not diverge.
- **FR-015**: When the underlying core operation fails, the MCP server
  MUST surface the failure as an MCP-protocol error with a message
  that identifies the failure class (matching FR-012 categories), not
  as a successful tool result containing an error description.
- **FR-016**: A successful search returning zero matches MUST be a
  non-error tool result whose body is the "no matches" form (FR-011's
  format with no result lines). It MUST NOT be an MCP error.

#### Cross-cutting

- **FR-017**: All tests for this feature — unit tests for the search
  operation, integration tests for the CLI command and the MCP tool —
  MUST run against a mocked buildin client; no test may call a real
  buildin host (constitution Principle IV).
- **FR-018**: The cheap-LLM integration test introduced by feature
  002 MUST be extended (or a sibling MUST be added) to demonstrate an
  LLM chaining `search` → `buildin://{page_id}` against a mocked
  workspace, validating that the LLM-readable search output is
  consumable as input to the existing read resource.
- **FR-019**: The buildin token continues to be supplied via
  configuration (env var or user-scoped configuration), per feature
  002's FR-015. No new auth surface is introduced.

### Key Entities

- **Search query**: an opaque, non-empty user-supplied string passed
  verbatim to buildin's search endpoint.
- **Page-scope identifier**: an optional buildin page UUID; when
  present, it constrains the returned list to that page and its
  descendants. When absent, the scope is the entire token-accessible
  workspace.
- **Search match**: one element of the returned list; minimally has a
  page ID, a displayable title, and a buildin object type (page /
  database). May carry additional metadata.
- **Search result**: the ordered list of matches returned by one
  invocation of the core operation.
- **Search tool / `search` command**: the MCP tool and CLI command,
  respectively, that wrap the core operation. Their inputs map to the
  core operation's parameters; their outputs MUST share a
  byte-identical body in plain mode (FR-014).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given a mocked workspace with a known page that contains
  a known string in its title and another page that contains the same
  string in its body, `buildout search "<string>"` returns both pages
  with their correct IDs and titles. Verified by a fixture-driven
  test.
- **SC-002**: An LLM driving the MCP `search` tool followed by the
  `buildin://{page_id}` resource can answer the question "which page
  describes X?" against a mocked workspace, end-to-end, without
  hand-coded glue. Demonstrated via an integration test using the
  same cheap testing LLM as feature 002.
- **SC-003**: For the same query and the same buildin response
  fixture, the CLI's plain-mode stdout is byte-identical to the body
  returned by the MCP search tool. Verified by a test that runs both
  surfaces over the same fixture and compares output.
- **SC-004**: Scoped search (`--page <id>` / MCP `page_id`) returns a
  strict subset of unscoped search for the same query, against the
  same fixture — never an over-broad superset, never out-of-order.
  Verified by a test that runs both shapes against an ancestor-chain
  fixture.
- **SC-005**: Failure modes (page not found for the scope, auth
  failure, transport failure) are surfaced distinctly in CLI exit
  codes and MCP error messages, with the same exit-code mapping
  feature 002 uses. Verified by negative-path tests.
- **SC-006**: Empty / whitespace-only query is rejected at the CLI
  and MCP boundary without any buildin call being made. Verified by a
  test that asserts the mocked buildin client recorded zero calls.
- **SC-007**: The full test suite for this feature, including the
  extended cheap-LLM integration test, completes in well under 30
  seconds on a developer laptop with no outbound network access to
  buildin — combined with feature 002's suite, the cumulative offline
  feature-test budget remains within the constitution's spirit
  (test-first, fast).
- **SC-008**: A user who already has feature 002 working can pair
  search with read in a single shell pipeline ending in `xargs
  buildout get`, using only standard text utilities (`head`, `awk`,
  `cut`) to extract the page ID from a search result line — without
  parsing JSON, without further configuration. This validates the
  plain-mode output format from FR-011 is genuinely shell-composable
  and that the page ID is in a stable, extractable position on each
  result line.

## Assumptions

- The buildin search endpoint accepts (at minimum) a query string and
  returns a list of pages / databases with parent metadata sufficient
  to walk the parent chain. No native parent-scope filter is assumed;
  the scoped behaviour in FR-004 is delivered by post-filtering the
  response against the buildin parent hierarchy. The exact
  ancestor-walk strategy (eager full-walk, lazy on-demand, cached) is
  a `/speckit-plan` decision.
- The Bot-API typed client from feature 001 is the buildin transport.
  The client's existing surface is extended (or its underlying
  generated search request is wrapped) to expose `Search` on
  `IBuildinClient` — same shape as the existing `GetPage` /
  `GetBlockChildren` extensions from feature 002. The User-API path
  remains future work behind the same interface (constitution
  Principle V).
- Search results are returned in whatever ranking buildin provides;
  this feature does not re-rank, score, or sort. Re-ranking is a
  future, additive change.
- Both presentation surfaces (CLI and MCP) are in scope. The user's
  stated input only showed CLI usage, but the constitution mandates
  that any new domain capability lands on both surfaces simultaneously
  (Principle I), so the MCP search tool ships in the same feature.
- Result format on both surfaces is plain-text / line-oriented, NOT
  Markdown. The read operation in feature 002 produces Markdown
  because pages have rich structure; search results are flat list
  output and produce maximum value as shell-pipeable text. Markdown
  rendering of search results would not improve LLM readability over
  plain text and would complicate the "byte-identical CLI/MCP body"
  contract (FR-014).
- The exact plain-mode line format, the exact "no matches" rendering,
  the exact "(untitled)" placeholder string, and the exact exit-code
  numbers are all `/speckit-plan` decisions; this spec only fixes the
  invariants that callers depend on.
- TTY detection, error-class taxonomy, exit-code mapping, secrets
  handling, mock-HTTP mechanism, and the cheap-LLM integration
  harness are all reused from feature 002 unchanged. This feature
  adds no new cross-cutting infrastructure.
