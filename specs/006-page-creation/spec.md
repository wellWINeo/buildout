# Feature Specification: Page Creation from Markdown

**Feature Branch**: `006-page-creation`
**Created**: 2026-05-13
**Status**: Draft
**Input**: User description: "page creation (support back conversion from markdown to buildin API blocks), identify which params should accept such tool"

## Clarifications

### Session 2026-05-13

- Q: How should the tool discriminate between page, database, and workspace parents — probe the buildin API, or require an explicit kind parameter? → A: Auto-probe the parent kind from the supplied id on every create; the tool exposes no `--parent-kind` / `parent_kind` parameter on either surface. The probe is a single GET against the buildin API (page-by-id, falling back to database-by-id) issued before `createPage`, and its result determines whether `--property` flags are accepted (database) or rejected (page/workspace).
- Q: What shape should the MCP `create_page` success response take — a plain text id body, a JSON object, or an MCP `resource_link`? → A: A single MCP `resource_link` content item whose URI is `buildin://<new_page_id>`. The MCP tool returns no text body. The byte-identical CLI/MCP invariant established by features 002/003 does NOT apply to this tool — the CLI's `--print id` text output and the MCP `resource_link` are equivalent at the level of "carries the new page id", not at the level of "byte-identical wire form". Round-trip tests assert the id matches, not the encoding.

## User Scenarios & Testing *(mandatory)*

This feature delivers the first user-visible *write* capability of buildout:
turning a Markdown document into a new buildin page. It is the inverse of
feature 002's page read — the same block compatibility matrix, the same set of
inline mentions, the same surface parity (CLI + MCP). Reading and creating a
page now form a complete round trip per constitution Principle III.

The user's stated ask — "identify which params should accept such tool" —
is answered in the Requirements section (FR-009 / FR-011 enumerate the CLI and
MCP parameters) and summarised in Key Entities → Create-page parameters.

### User Story 1 — CLI: create a page from a local Markdown file (Priority: P1)

As a developer with a buildin parent (a page I can write under, or my
workspace), I run `buildout create --parent <parent_id> <path/to/file.md>`
and a new buildin page appears under that parent, with the Markdown converted
to native buildin blocks and the page's title taken from the document's
leading `# Heading`. The command prints the new page's ID (and, in TTY mode,
its title and URL-shaped `buildin://<page_id>`) so I can pipe it into
`buildout get` to confirm what I just wrote.

**Why this priority**: The smallest end-to-end slice that exercises the new
Markdown→blocks converter, the buildin client's `createPage` +
`appendBlockChildren` calls, and a presentation surface. Without it the
feature has no demonstrable behaviour, and the round-trip principle (II/III)
remains half-built.

**Independent Test**: With a mocked buildin client that records every request
sent to `POST /v1/pages` and every batch sent to `PATCH /v1/blocks/{id}/children`,
run `buildout create --parent <page_id> fixtures/sample.md` against a fixture
that contains every supported block type (heading 1–3, paragraph with inline
formatting, bulleted list, numbered list, todo, fenced code, quote, divider).
Assert: (a) exactly one `createPage` call with the parent set correctly and
the title pulled from the leading H1, (b) one or more `appendBlockChildren`
batches whose concatenated payload is the document's body converted block-by-
block in source order, (c) stdout contains the new page ID on a single line
in plain mode, (d) exit code 0. Round-trip: read the new page back through
`buildout get` (against the same mocked store) and assert the resulting
Markdown equals the input Markdown by the compatibility-matrix equivalence
already established in feature 002.

**Acceptance Scenarios**:

1. **Given** a Markdown file beginning with `# My Page` followed by a body
   that contains a paragraph, a bulleted list, and a fenced code block,
   **When** the developer runs
   `buildout create --parent <page_id> file.md`, **Then** a new page is
   created under `<page_id>` with title `My Page`, the body blocks appear in
   document order, and the process writes the new page ID to stdout and
   exits 0.
2. **Given** the same file piped into the command
   (`cat file.md | buildout create --parent <page_id> -`), **When** the
   command runs, **Then** the result is byte-identical to scenario 1 — `-`
   selects stdin as the Markdown source.
3. **Given** a Markdown body that exceeds the buildin per-request limit of
   100 children blocks at the top level, **When** the command runs, **Then**
   the body is split into successive `appendBlockChildren` batches of at
   most 100 blocks each, in source order, and the resulting page contains
   every block exactly once.
4. **Given** a Markdown document with no leading H1, **When** the command
   runs without `--title`, **Then** the command exits with the
   validation-error exit code already in use and writes a message naming
   the missing title; no `createPage` call is issued.
5. **Given** the parent ID does not exist, the credential is unauthorised,
   the buildin host is unreachable, or buildin returns a 4xx/5xx after the
   page is created but before all children are appended, **When** the
   command runs, **Then** the failure is surfaced with the same exit-code
   taxonomy already used by `get` and `search` (not-found, auth, transport,
   unexpected); see FR-012 for partial-failure handling.

---

### User Story 2 — MCP: expose page creation as an MCP tool (Priority: P1)

As an LLM connected to the buildout MCP server, I invoke the `create_page`
tool with a parent ID and a Markdown body, and I receive the new page's ID
in the tool result. I can then read the page back via the existing
`buildin://{page_id}` resource to confirm what I wrote.

**Why this priority**: The product invariant (Principle I) is parity between
CLI and MCP for every domain capability. LLM-driven write flows ("draft this
spec into a new page under Project X") only become possible once create_page
exists.

**Independent Test**: Start the MCP server against the same mocked buildin
client used in US1. From a test client, list tools and discover `create_page`
with an input schema matching FR-011. Invoke it with the same body used in
US1's fixture; assert the response contains exactly one `resource_link`
content item whose URI is `buildin://<new_page_id>` (the new page id
extracted from that URI equals the id the CLI prints in plain mode).
Then read `buildin://<new_page_id>` through the same MCP server and
assert the rendered Markdown round-trips per the compatibility matrix. The cheap-LLM
integration test from features 002/003 MUST be extended (or a sibling added)
to demonstrate an LLM chaining `create_page` → `buildin://{page_id}`
end-to-end.

**Acceptance Scenarios**:

1. **Given** the MCP server is running, **When** an MCP client lists tools,
   **Then** the listing advertises `create_page` with an input schema
   declaring `parent_id` (string, required), `markdown` (string, required),
   and the optional fields enumerated in FR-011. Its description identifies
   the tool as "create a new buildin page from a Markdown document".
2. **Given** a valid invocation, **When** the tool runs, **Then** the
   response carries exactly one MCP `resource_link` content item whose
   URI is `buildin://<new_page_id>` (FR-014). The response carries no
   text body; the new page id is recovered from the URI.
3. **Given** a buildin error (404 parent, 401/403 auth, transport, generic
   4xx/5xx), **When** the tool runs, **Then** the server returns an
   MCP-protocol error mapped to the same MCP error code already used by the
   read tools — not a successful tool result with an error blob inside.
4. **Given** a Markdown body with no leading H1 and no `title` argument,
   **When** the tool runs, **Then** the server returns an MCP-protocol
   validation error before any buildin call is made.

---

### User Story 3 — Round-trip: read → edit → create makes a faithful new page (Priority: P2)

As a developer or LLM, I read a page with `buildout get <page_id>`, edit the
resulting Markdown in any text tool, and write the edited Markdown into a
new page with `buildout create --parent <parent_id> edited.md`. The new page
preserves every supported block type and inline element from the edited
document with the fidelity guaranteed by the compatibility matrix.

**Why this priority**: It is the user-visible payoff of constitution
Principle III: the read and write surfaces are not just paired, they
compose. P2 because it depends on US1/US2 landing and is testable through
them; the slice does not require any new surface code beyond what US1 and
US2 introduce — it is a test/documentation deliverable.

**Independent Test**: Take an existing fixture page used by feature 002's
golden tests. Render it via `RenderAsync` to a Markdown string. Feed that
string through this feature's create-page operation against a fresh mocked
parent. Read the new page back. Assert the second rendered Markdown equals
the first per the compatibility matrix. Repeat the test for every supported
block type in isolation and in nested combinations.

**Acceptance Scenarios**:

1. **Given** any fixture page from feature 002's golden tests, **When** it
   is round-tripped (read → create → read), **Then** the second rendering
   equals the first under the compatibility-matrix equivalence relation,
   with no block-type loss outside what the matrix already documents as
   lossy.
2. **Given** a page that contains an inline page/database mention (rendered
   on the read side as `[Title](buildin://<id>)`), **When** the resulting
   Markdown is fed to create, **Then** the new page contains the same
   mention in the same position — the link is parsed back into a mention,
   not stored as a plain Markdown link to an unsupported scheme.
3. **Given** a page that contains an unsupported block (currently iframe
   and any other block not in FR-003), **When** the read output (which
   contains the unsupported-block placeholder) is fed to create, **Then**
   the placeholder survives a CommonMark round-trip without producing a
   real block in the new page — placeholders are preserved as Markdown
   comments / text, never silently materialised as buildin content.

---

### Edge Cases

- **Empty Markdown body**: A document that is empty or whitespace-only with
  no `# Title` line and no `--title` argument fails the same validation as
  the "no H1" case in US1.4. A document containing only a `# Title` line
  (no body) succeeds: a new page is created with that title and no
  children; `appendBlockChildren` is not called.
- **Markdown title with no body**: As above — title alone is enough to
  create a page; the body batch is skipped, not sent empty.
- **Leading H1 followed by a blank line before body**: That blank line is
  consumed along with the H1 as part of "the title", to mirror the read
  side's `# <title>\n\n<body>` output. Two consecutive blank lines after
  the H1 are also tolerated.
- **Multiple H1s in the document**: Only the *first* H1, *if it is the
  first non-blank line*, is consumed as the title. H1s elsewhere in the
  document are rendered as `heading_1` blocks in the body. If the first
  non-blank line is not an H1 (e.g. starts with a paragraph or H2), the
  title is *not* extracted — the user must pass `--title` explicitly.
- **Very large document (more than 100 top-level blocks)**: The body is
  split into successive `appendBlockChildren` batches of up to 100 blocks,
  in document order, and a single creation appears atomic from the user's
  perspective. Per-batch concurrency is out of scope for v1; batches are
  issued sequentially.
- **Nested children (lists with sub-bullets, code blocks inside list
  items)**: The Markdown→block converter recurses, building child arrays
  and using the buildin payload's nested `children` field where the API
  supports it; otherwise it falls back to follow-up
  `appendBlockChildren` calls against the parent block's ID once the
  parent block is created. Either approach is acceptable so long as the
  final structure matches the source document.
- **Partial failure mid-append**: If `createPage` succeeds but a
  subsequent `appendBlockChildren` batch fails, the page exists but is
  incomplete. The command MUST surface the failure with a non-zero exit
  code and a stderr / MCP-error message that *names the partial page's
  ID* so the user can decide to delete it or fix it. v1 does NOT attempt
  to auto-rollback the partially-created page; rollback is a future,
  additive change.
- **Authentication failure before any call**: Same handling as features
  002/003 — distinct exit code on the CLI, MCP-protocol error on MCP.
- **Markdown that fails to parse as CommonMark**: Validation error before
  any buildin call; exit code matches existing validation-error class.
- **Unicode / RTL / emoji content**: Passes through unchanged; the
  converter MUST NOT normalise, reorder, or mangle code points.
- **Markdown link to a `buildin://<id>` URI**: Parsed back into a buildin
  mention block of the appropriate kind (page mention if buildin's API
  reports that ID as a page; database mention if a database). The
  converter does not perform a lookup just to disambiguate — it uses the
  URI shape and lets buildin reject mismatches. Plain HTTP/HTTPS links
  remain plain links in the resulting block.
- **HTML embedded in the Markdown**: Not supported in v1; raw HTML in the
  source is treated as text inside the surrounding block (e.g. a paragraph
  containing literal `<div>` text). It does not produce a buildin `embed`
  block. Future feature.
- **Frontmatter (YAML / TOML)**: Not parsed in v1. If a document begins
  with a `---` fence, that fence is treated as a Markdown thematic break
  per CommonMark, and the body inside is rendered as paragraphs. Future
  feature.

## Requirements *(mandatory)*

### Functional Requirements

#### Core conversion (shared by both surfaces)

- **FR-001**: The shared core library MUST expose a single
  Markdown-to-page creation operation that takes a parent identifier, a
  Markdown string, and the optional parameters in FR-009/FR-011, and
  returns the new page's identifier (and any metadata the buildin
  `createPage` response carries that callers depend on). Both
  presentation surfaces MUST go through this operation; neither may call
  `POST /v1/pages` directly nor reimplement Markdown→block conversion
  locally.
- **FR-002**: The core operation MUST expose a single
  Markdown→blocks conversion entry point that is independently
  unit-testable (no buildin client involvement). The same conversion
  function MUST be reused by the round-trip tests required by
  constitution Principle III.
- **FR-003**: The converter MUST support, at minimum, the inverse of every
  block type already supported by the page-read converter (feature 002
  FR-002):
  - paragraph (with inline formatting: bold, italic, inline code, links)
  - heading levels 1, 2, and 3
  - bulleted list item
  - numbered list item
  - to-do list item (parsed from GFM `- [ ]` / `- [x]`)
  - fenced code block (language tag preserved when present)
  - quote
  - divider (thematic break)
  Nested children of these blocks MUST be recursed into.
- **FR-004**: Inline mentions MUST be parsed back from the forms feature
  002 FR-005b emits:
  - `[<title>](buildin://<id>)` — page or database mention; the converter
    selects page-mention by default and lets buildin's response classify
    the ID. Mismatches surface as buildin errors, not converter errors.
  - `@<display name>` — emitted by buildin as plain text in v1; this
    converter does NOT round-trip user mentions into mention blocks
    because the read side discards the user ID. Plain `@name` text is
    written as a plain text run, not a mention. This is documented in
    the compatibility matrix as one-way-lossy.
  - ISO date strings inside an otherwise-supported block: written as
    plain text. The matrix documents this as lossy (read mention →
    plain text on write). Future feature: round-trip date mentions via
    a distinct Markdown form.
- **FR-005**: The new page's title MUST come from one of:
  - the explicit `title` parameter when provided, OR
  - the first non-blank line of the Markdown body, *if* that line is an
    ATX heading at level 1 (`# Title`); in this case the H1 (and the
    blank line that follows it, if any) is consumed and NOT included as
    a body block.
  If neither rule yields a title, the operation MUST fail validation
  before any buildin call is made. The explicit parameter always wins;
  when both an explicit title and a leading H1 are present, the leading
  H1 is still consumed (so the body matches the read side's `<body>`
  after the title-line), but the explicit value is sent.
- **FR-006**: The unsupported-block placeholder format feature 002 FR-003
  emits MUST round-trip cleanly: a document containing such placeholders
  MUST NOT cause a creation failure, and the placeholder lines MUST NOT
  be materialised as real buildin blocks. They are preserved as text /
  comments in the resulting page exactly as authored.
- **FR-007**: Block types not enumerated in FR-003 (image, file,
  bookmark, embed, callout, equation, link_to_page, template,
  synced_block, column_list, column, table, table_row, child_page,
  child_database, toggle) are out of scope for v1. The converter MUST
  NOT silently invent blocks of those types; it MAY render them as
  paragraphs of fallback plain text if their Markdown surface is
  unambiguous, otherwise it MUST treat them as paragraphs of literal
  source text. The compatibility matrix documents each as "write:
  unsupported (paragraph fallback)" or "write: unsupported (no-op)"
  accordingly.
- **FR-008**: When the rendered block list exceeds 100 top-level blocks,
  the operation MUST split the list into sequential
  `appendBlockChildren` batches of at most 100 blocks each, preserving
  source order. The buildin API's 100-block per-request limit MUST never
  be exceeded under any input.

#### CLI surface

- **FR-009**: The CLI MUST expose a `create` command (final command name
  is a `/speckit-plan` decision; this spec fixes only that exactly one
  new top-level CLI verb is added) with:
  - one positional argument `<markdown_source>`: a filesystem path to a
    Markdown file, or the literal `-` to read Markdown from stdin.
    Required.
  - `--parent <id>` (string, required): the buildin page id or database id
    under which the new page is created. Workspace / space identifiers are
    deferred from v1 (see R5): the probe sequence has no `GET /v1/spaces/{id}`
    endpoint, so a workspace-shaped id is treated as parent-not-found.
  - `--title <text>` (string, optional): overrides the leading-H1 title
    rule (FR-005).
  - `--icon <value>` (string, optional): a single emoji, or a URL
    interpreted as an external icon. Out-of-scope for v1: uploading
    icon files.
  - `--cover <url>` (string, optional): an external image URL used as
    the page cover. Out-of-scope for v1: uploading cover files.
  - `--property <name>=<value>` (repeatable, optional): a property value
    to set on the new page. Only meaningful when the parent is a
    database (FR-010). v1 supports plain-text property kinds whose value
    is expressible as a single string: title, rich_text, number, select,
    multi_select (comma-separated values), checkbox (`true`/`false`),
    date (ISO 8601), url, email, phone_number. People, files, relation,
    rollup, and formula properties are out of scope for v1 and trigger
    a validation error.
  - `--print <id|json|none>` (optional, default `id`): controls what is
    written to stdout. `id` writes the new page id followed by a
    newline. `json` writes a single JSON object with the new page id
    and the buildin URL (if buildin returns one). `none` writes
    nothing.
- **FR-010**: When `--parent` resolves to a database id, the converter
  MUST set the title property from FR-005 and accept the property values
  given by `--property` flags; when `--parent` resolves to a page id,
  only the title is set and `--property` flags produce a validation error
  (no properties exist outside a database).
  The tool MUST auto-probe the parent kind from the supplied id (single
  GET — page-by-id first, falling back to database-by-id) before issuing
  `createPage`; no `--parent-kind` / `parent_kind` parameter is exposed
  on either surface. If the probe fails (the id is neither a readable
  page nor a readable database under the caller's credential), the
  operation MUST exit with the parent-not-found failure class before any
  write call is issued.
- **FR-011**: The MCP `create_page` tool's input schema MUST be a
  one-to-one mapping of FR-009's CLI surface, with these field names:
  - `parent_id` (string, required)
  - `markdown` (string, required) — the document body, including its
    leading H1 if any (the server applies the FR-005 rule)
  - `title` (string, optional)
  - `icon` (string, optional)
  - `cover_url` (string, optional)
  - `properties` (object, optional) — when the parent is a database, an
    object whose keys are property names and whose values are the same
    plain-text serialisations the CLI's `--property` flag accepts. An
    empty `{}` is equivalent to absence. Same v1 scope as FR-009.
  - No `print` field — the MCP tool's response shape is fixed by
    FR-014 (a `resource_link` to `buildin://<new_page_id>`).
- **FR-012**: CLI exit codes MUST match the taxonomy already used by
  `get` and `search`: validation error, page-not-found (for the parent),
  authentication/authorisation, transport, unexpected. "Successful
  creation" is exit 0. "Partial creation" (page created, some children
  failed) is the unexpected-error exit code, and the partial page's ID
  MUST appear in stderr so the user can recover.

#### MCP surface

- **FR-013**: The MCP server MUST advertise the `create_page` tool with
  the input schema in FR-011 and a description that names it as a
  Markdown-driven page creation. The tool MUST be discoverable in
  standard MCP tool listing.
- **FR-014**: A successful `create_page` invocation MUST return a single
  MCP `resource_link` content item whose URI is `buildin://<new_page_id>`
  and whose name/title carries the page's title (the one written, per
  FR-005). The response MUST NOT include a text content item containing
  the id; the new page id is recoverable from the URI. The MCP
  `resource_link` and the CLI `--print id` output are equivalent at the
  level of "carries the new page id", not byte-identical wire forms —
  this is the one MCP/CLI surface in the project that deliberately
  diverges on wire form (other read tools remain text-only and stay
  byte-identical to their CLI counterparts).
- **FR-015**: A failure (validation, parent-not-found, auth/transport,
  buildin-side error, partial creation) MUST be surfaced as an
  MCP-protocol error mapped to the existing error-class taxonomy. In
  the partial-creation case, the error message MUST contain the partial
  page's id.

#### Cross-cutting

- **FR-016**: All tests for this feature — Markdown→blocks unit tests,
  create-page integration tests, round-trip tests required by Principle
  III — MUST run against a mocked buildin client. No test may call a
  real buildin host.
- **FR-017**: The cheap-LLM MCP integration test from features 002/003
  MUST be extended (or a sibling test added) to demonstrate an LLM
  chaining `create_page` followed by `buildin://{new_page_id}` and
  verifying the resulting Markdown matches the input under the
  compatibility matrix.
- **FR-018**: Buildin tokens continue to be supplied per feature 002
  FR-015 (env var or user-scoped configuration). No new auth surface is
  introduced.
- **FR-019**: The operation is destructive in the constitution Principle
  VI sense only insofar as it *creates* user-visible buildin content
  under a parent the user named. It MUST NOT modify or replace any
  existing block in any existing page, and MUST NOT create more than
  one page per invocation. The command name (`create`) carries the
  intent explicitly; no additional `--confirm` flag is required by this
  spec.

### Key Entities

- **Markdown source**: a CommonMark string supplied as a file path, as
  `-` for stdin, or as the `markdown` MCP argument. Treated as an opaque
  document by everything upstream of the converter.
- **Markdown→blocks converter**: pure function from a Markdown string to
  an ordered tree of buildin block payloads (the input shape buildin's
  `appendBlockChildren` accepts). Inverse of feature 002's
  `PageMarkdownRenderer` and `BlockToMarkdownRegistry`. Lives in
  `Buildout.Core` alongside the existing converters.
- **Create-page parameters** (the answer to the user's "identify which
  params" question; full normative list in FR-009/FR-011):
  - `parent` — required; page id or database id (workspace identifiers
    deferred to v2; treated as parent-not-found in v1)
  - `markdown_source` — required; positional path or `-` (CLI) /
    `markdown` string (MCP)
  - `title` — optional override; default is leading-H1 consumption
  - `icon` — optional; emoji or URL
  - `cover` / `cover_url` — optional; image URL
  - `property` (CLI, repeatable) / `properties` (MCP, object) —
    optional; only meaningful for database parents; v1 covers
    plain-text property kinds only
  - `--print` (CLI only) — optional; controls stdout shape
- **Compatibility matrix**: extended from feature 002's per-block-type
  record to include a write-direction column for each block. Owned by
  the converter, exercised by tests, governs which round-trips are
  lossless versus documented-lossy.
- **New page id**: the buildin-assigned identifier returned by
  `createPage`. The single piece of state this operation produces. The
  CLI surfaces it as plain text on stdout (under `--print id`); the MCP
  tool surfaces it as the page id encoded in the URI of a
  `resource_link` content item pointing at `buildin://<new_page_id>`
  (FR-014). Both surfaces let the caller chain into a subsequent read
  via the existing `buildin://{page_id}` resource.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given a Markdown fixture covering every block type in
  FR-003 plus inline formatting and page/database mentions, the create
  operation produces a buildin page (against the mocked client) whose
  block list matches the converter's expected output element-for-element
  in source order. Verified by a fixture-driven test.
- **SC-002**: For every fixture page used by feature 002's golden tests,
  the round-trip read → create → read produces Markdown equal to the
  original under the compatibility-matrix equivalence relation.
  Verified by an automated round-trip suite.
- **SC-003**: A Markdown body containing more than 100 top-level blocks
  is created without any single `appendBlockChildren` request exceeding
  100 children; the resulting page contains every block exactly once,
  in source order. Verified by a test that records every request the
  mocked buildin client receives.
- **SC-004**: For the same inputs and the same buildin response fixture,
  the new page id printed on stdout by the CLI's plain mode (`--print id`)
  equals the page id encoded in the URI of the MCP `create_page` tool's
  `resource_link` content item. The two surfaces deliberately use
  different wire forms (text id vs MCP `resource_link`); this SC asserts
  semantic equivalence, not byte identity. Verified by an automated test
  that extracts and compares the ids.
- **SC-005**: Failure modes — invalid Markdown, missing title, unknown
  parent, auth failure, transport failure, partial creation — each
  surface distinctly through the CLI exit codes and MCP error messages
  defined by features 002/003. The partial-creation case names the
  partial page id in the error output. Verified by negative-path tests.
- **SC-006**: A user can read an existing page with `buildout get`, edit
  the Markdown locally, and create a new page from the edit in a single
  shell pipeline (`buildout get <id> | buildout create --parent <p> -`)
  without parsing JSON or any out-of-band configuration. Validates that
  the plain-mode read output is consumable as plain-mode create input
  end-to-end.
- **SC-007**: The full test suite for this feature, including the
  extended cheap-LLM MCP integration test, completes well under 30
  seconds on a developer laptop with no outbound network access. The
  cumulative offline feature-test budget remains within the
  constitution's spirit (test-first, fast).
- **SC-008**: The create operation cannot modify or delete any existing
  block in any existing page under any input combination. Verified by a
  contract test asserting that, across every test in this feature's
  suite, the mocked buildin client receives no `updateBlock`,
  `updatePage`, `deleteBlock`, `updateDatabase`, or `createDatabase`
  calls — only `createPage` and `appendBlockChildren`.

## Assumptions

- **Markdown→blocks is the natural inverse of feature 002's read
  converter.** The block set, the inline-formatting set, and the mention
  forms are the same; the compatibility matrix gains a write column
  rather than a parallel matrix. Block types outside that set are
  out-of-scope and fall through to paragraph text per FR-007 rather than
  inventing buildin types this feature does not yet test.
- **Title extraction follows the read side's emission rule.** Feature
  002 FR-005a emits `# <title>\n\n<body>`; this feature consumes that
  exact shape on the way back in. An explicit `--title` / `title`
  parameter overrides the rule but does not change the body slicing — a
  leading H1 is still consumed if present, so the round-trip stays
  symmetric.
- **Database parents are in scope at the parameter level but limited at
  the property level.** v1 accepts a database parent and supports the
  plain-text property kinds enumerated in FR-009. People / files /
  relation / rollup / formula property writes are deferred to a future
  feature. A page parent never accepts property values; workspace parents
  are deferred from v1 (see R5).
- **Parent-kind probing is part of the create operation, not a separate
  user-facing step.** Per the 2026-05-13 clarification, the tool always
  probes the supplied id (page-by-id first, database-by-id fallback)
  before `createPage`; the user-facing surface exposes no parent-kind
  parameter on either CLI or MCP. The probe's GET is counted as part of
  this feature's API-call budget; it is not a precondition the caller
  is expected to satisfy.
- **The buildin API's 100-block per-request limit is the only batching
  constraint v1 honours.** Larger bodies are split sequentially.
  Per-batch concurrency, streaming progress, retry-on-rate-limit, and
  incremental rollback are explicitly out of scope for this feature.
- **Partial-failure rollback is out of scope.** If the page is created
  but a body batch fails, the partial page remains in buildin and its id
  is surfaced to the user. v1 does not attempt to delete the partial
  page; an opt-in `--rollback-on-error` is a future, additive change.
- **Icon and cover are external-URL-only in v1.** Uploading icon /
  cover files to buildin's file-upload endpoint is a future feature; v1
  accepts only emoji icons and external URLs.
- **The cheap-LLM MCP integration harness, mock-HTTP mechanism,
  exit-code taxonomy, TTY-detection rules, and secrets handling are
  reused unchanged from features 002/003.** This feature adds no new
  cross-cutting infrastructure.
- **Final command name, MCP tool name, exact stdout JSON shape under
  `--print json`, and exact validation-error messages are
  `/speckit-plan` decisions.** This spec fixes only the contracts callers
  depend on.
