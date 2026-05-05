# Feature Specification: Initial Page Reading

**Feature Branch**: `002-basic-page-reading`
**Created**: 2026-05-05
**Status**: Draft
**Input**: User description: "Initial reading support. Both MCP and CLI should support basic reading buildin pages as plain markdown. MCP - add resource buildin://{page_id}. For CLI - `buildout get {page_id}`, check if terminal supports rich markup, if not - just output markdown without styling. _Basic reading_ - means support basic page blocks (headers, lists, code listing), excluding iframes and databases."

## User Scenarios & Testing *(mandatory)*

This feature delivers the first user-visible capability of buildout: turning a
buildin page into Markdown that an LLM (via MCP) or a human (via CLI) can read.
The two surfaces are independent slices over the same core conversion.

### User Story 1 — CLI: read a page as Markdown (Priority: P1)

As a developer with a buildin page ID, I run `buildout get <page_id>` in a
terminal and see the page's contents rendered as Markdown. If my terminal
supports styled rendering, I see headings, lists, and code blocks formatted for
readability; if I'm piping the output to a file or another tool, I get plain
CommonMark that another tool can consume unchanged.

**Why this priority**: This is the smallest end-to-end slice that exercises the
core block→Markdown converter, the buildin client, and a presentation surface.
It is independently shippable and demonstrable to a human in seconds.

**Independent Test**: With a recorded/mocked buildin response for a page that
contains a heading, a paragraph, a bulleted list, a numbered list, and a code
block, run `buildout get <page_id>` (a) into a real TTY and observe styled
output, (b) piped to a file and observe raw CommonMark equivalent in semantic
content. The piped output, parsed as CommonMark, MUST round-trip back to the
same block structure.

**Acceptance Scenarios**:

1. **Given** a buildin page containing a heading, paragraph, bulleted list,
   numbered list, and fenced code block, **When** the developer runs
   `buildout get <page_id>` with stdout connected to a terminal that advertises
   rich-text capability, **Then** the page contents appear with visible
   styling (heading emphasis, list bullets/numbers, code blocks visually
   distinct) and the process exits 0.
2. **Given** the same page, **When** the developer runs `buildout get <page_id>`
   with stdout redirected to a file or pipe (no TTY), **Then** the file
   contains plain CommonMark Markdown with no terminal escape codes, and the
   process exits 0.
3. **Given** a page ID that does not exist or the caller is not authorised to
   read, **When** the developer runs `buildout get <page_id>`, **Then** the
   command writes a human-readable error to stderr identifying the failure
   class (not found, unauthorised, transport, unexpected) and exits with a
   non-zero status.
4. **Given** a page that contains an iframe block or an embedded database
   block (excluded from this feature), **When** the developer runs
   `buildout get <page_id>`, **Then** every supported block in the page is
   rendered correctly and each unsupported block is replaced by a placeholder
   that names the block type, in its original position, so neither the LLM
   nor the human silently loses content.

---

### User Story 2 — MCP: expose a page as a `buildin://{page_id}` resource (Priority: P1)

As an LLM connected to the buildout MCP server, I list resources, discover the
page-resource template `buildin://{page_id}`, and read a specific page by
substituting its ID. The server returns the page's contents as a single
Markdown document I can reason over directly.

**Why this priority**: The product's reason for existing is LLM ergonomics
(constitution principle II). Reading a page is the foundational read
operation; nothing else (write, edit) is meaningful without it.

**Independent Test**: Start the MCP server against a mocked buildin client
returning a page with mixed supported and unsupported blocks. From a test
client, request the resource `buildin://<page_id>`. Assert the response is
a single Markdown text resource whose body matches the same converter output
as the CLI's piped form for the same page.

**Acceptance Scenarios**:

1. **Given** the MCP server is running and connected to a working buildin
   client, **When** an MCP client lists resources, **Then** the listing
   advertises a resource template with URI scheme `buildin://{page_id}` and a
   description identifying it as a buildin page rendered as Markdown.
2. **Given** a known page ID, **When** an MCP client reads
   `buildin://<page_id>`, **Then** the response contains exactly one text
   resource whose MIME type is a Markdown type and whose body is the page
   rendered as CommonMark.
3. **Given** a page ID that does not exist or is unauthorised, **When** an
   MCP client reads `buildin://<page_id>`, **Then** the server returns an
   MCP-level error (not a 200 with an error blob inside the body) whose
   message identifies the failure class.
4. **Given** the same page used in US1's acceptance scenario 1, **When** that
   page is requested both through the CLI (in plain mode) and through the MCP
   resource, **Then** the two Markdown bodies are byte-identical.

---

### Edge Cases

- **Empty page**: a page with zero blocks renders as an empty Markdown document
  (zero bytes or a single trailing newline) and exits 0 / returns success. It
  is not an error.
- **Unsupported block types**: any block this feature does not yet handle —
  including but not limited to iframe, embedded database, and any other block
  not in the supported list — is replaced by a single-line placeholder of the
  form `<!-- unsupported block: <block_type> -->` (or equivalent that survives
  CommonMark parsing) at its original position. Position fidelity matters so
  callers can tell where content was elided.
- **Nested children**: list items and other supported blocks may contain
  children. Nesting MUST be preserved in the Markdown output (indented list
  items, nested fenced code, etc.) for every supported block type. Children
  of unsupported blocks are not recursed into; the placeholder stands in for
  the entire subtree.
- **Authentication failure**: the buildin token is invalid, expired, or
  missing. The CLI exits non-zero with a message that distinguishes auth
  failure from a "page not found"; the MCP server returns an MCP error.
- **Transport failure**: the buildin host is unreachable or returns a 5xx /
  malformed response. Both surfaces surface this as a transport-class error
  distinct from a buildin-side error (see FR-009).
- **Very large pages**: a page exceeding what the buildin API returns in a
  single response (paginated children) MUST be fully fetched before
  rendering; partial output is not acceptable. If pagination is not yet
  implemented in the underlying client, the spec MUST document the page-size
  ceiling and fail loudly above it rather than silently truncating.
- **Unicode / right-to-left / emoji**: page content in any Unicode script
  passes through unchanged; the converter MUST NOT mangle, normalise, or
  reorder code points.
- **Non-TTY but rich-mode forced**: a future flag (`--rich`/`--no-rich`) is
  out of scope here, but the auto-detection MUST be correct for the common
  cases — interactive shell → styled, pipe/redirect → plain — and MUST never
  emit terminal escape codes when stdout is not a TTY.

## Requirements *(mandatory)*

### Functional Requirements

#### Core conversion (shared by both surfaces)

- **FR-001**: The shared core library MUST expose a single page-to-Markdown
  rendering operation that takes a buildin page ID, fetches the page (and all
  its descendant blocks needed to render its body), and returns a CommonMark
  string. Both presentation surfaces MUST go through this operation; neither
  may reimplement block→Markdown conversion locally.
- **FR-002**: The core converter MUST support, at minimum, the following
  buildin block types as first-class output:
  - paragraph (plain text, with inline formatting where present: bold,
    italic, inline code, links)
  - heading levels 1, 2, and 3
  - bulleted list item
  - numbered list item
  - to-do list item (rendered as GFM task list `- [ ]` / `- [x]`)
  - fenced code block (with language tag preserved when buildin provides one)
  - quote
  - divider
  Children of these blocks MUST be recursed into.
- **FR-003**: Block types not enumerated in FR-002 — including iframe,
  embedded database, and any other type not yet supported — MUST be rendered
  as a single placeholder line that names the block type and survives a
  CommonMark round-trip without parser errors. The placeholder format MUST
  be consistent across both surfaces.
- **FR-004**: Output MUST be deterministic CommonMark (or GFM where required
  for to-do lists). Two renderings of the same page (with the same fetched
  block payload) MUST produce byte-identical output.
- **FR-005**: Buildin-internal identifiers (block UUIDs, internal metadata)
  MUST NOT appear in the rendered Markdown unless explicitly required by the
  output of a supported block type.

#### CLI surface

- **FR-006**: The CLI MUST expose a `get <page_id>` command that takes a
  buildin page ID as a positional argument and writes the rendered Markdown
  to stdout.
- **FR-007**: The CLI MUST detect at runtime whether stdout is connected to
  a terminal that supports styled rendering. When it is, the CLI MUST render
  the Markdown with terminal styling (headings emphasised, lists bulleted/
  numbered, code blocks visually distinct). When it is not (pipe, redirect,
  non-styled terminal, `NO_COLOR` set, etc.), the CLI MUST emit raw
  CommonMark with zero terminal escape codes.
- **FR-008**: When invoked without a TTY, the byte stream the CLI writes to
  stdout MUST equal the body of the corresponding MCP resource read for the
  same page.
- **FR-009**: The CLI MUST distinguish, in its exit code and stderr output,
  among (a) page not found, (b) authentication / authorisation failure, (c)
  transport failure, and (d) unexpected error. Each category MUST use a
  distinct, documented exit code.

#### MCP surface

- **FR-010**: The MCP server MUST advertise a resource template with URI
  scheme `buildin://{page_id}` whose description identifies it as a buildin
  page rendered as Markdown.
- **FR-011**: Reading `buildin://{page_id}` MUST return a single text
  resource containing the rendered Markdown, with a Markdown MIME type
  (`text/markdown` or the most appropriate equivalent supported by the MCP
  SDK in use).
- **FR-012**: When the underlying core operation fails, the MCP server MUST
  surface the failure as an MCP-protocol error with a message that
  identifies the failure class (matching FR-009 categories), not as a
  successful read containing an error description.

#### Cross-cutting

- **FR-013**: All tests for this feature — converter unit tests and MCP/CLI
  integration tests — MUST run against a mocked buildin client; no test may
  call a real buildin host.
- **FR-014**: Round-trip tests MUST exist for each supported block type per
  constitution principle III: blocks → Markdown for the read direction.
  (Markdown → blocks is out of scope here; it lands with the writing
  feature.) Lossy conversions, if any, MUST be enumerated in a
  per-block-type compatibility matrix and exercised by tests.
- **FR-015**: The buildin token MUST be supplied via configuration (env var
  or user-scoped configuration) — not as a CLI flag value committed to
  history, and never embedded in source or tests.

### Key Entities

- **Page**: a buildin page identified by a UUID-style ID, containing an
  ordered tree of blocks.
- **Block**: a buildin content node with a type (paragraph, heading_1, etc.),
  optional inline formatting, and optional children.
- **Rendered page**: a single CommonMark/GFM string produced by the core
  converter from a Page.
- **MCP page resource**: an MCP resource template `buildin://{page_id}`
  resolving, on read, to a Rendered page.
- **CLI get command**: the `buildout get <page_id>` command which writes a
  Rendered page to stdout, optionally styled for the terminal.
- **Compatibility matrix**: the per-block-type record of supported /
  placeholder / lossy behaviour, owned by the converter and exercised by
  tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given a buildin page containing only the block types
  enumerated in FR-002, the rendered Markdown reproduces every visible
  element of the page — every heading, list item, code line, quote, divider
  — with no silent loss, validated by a test fixture covering all supported
  types in nested combinations.
- **SC-002**: An LLM reading the MCP resource for a page can answer at least
  one factual question per supported block type from the rendered output —
  i.e. the conversion is information-preserving for supported blocks.
  Demonstrated via the integration test suite using a cheap testing LLM.
- **SC-003**: The CLI command and the MCP resource produce byte-identical
  Markdown for the same page when the CLI is run in non-TTY mode. Verified
  by a test that fetches the same fixture page through both surfaces and
  compares the output.
- **SC-004**: Every block type in FR-002 has at least one passing
  conversion test, and every block type known to be excluded (iframe,
  embedded database) has at least one test asserting placeholder behaviour.
- **SC-005**: A new contributor can read any page their token grants them
  access to, with a single command, within 30 seconds of a green build —
  no further configuration needed beyond setting the buildin token.
- **SC-006**: The full test suite for this feature, including the cheap-LLM
  MCP integration test, completes in under 30 seconds on a developer laptop
  with no outbound network access to buildin.
- **SC-007**: Failure modes (page not found, auth failure, transport
  failure) are surfaced distinctly in both CLI exit codes and MCP error
  messages, validated by negative-path tests.

## Assumptions

- The Bot-API typed client from feature 001 is the buildin transport for
  this feature. No new auth modes are introduced here; the User-API path
  remains future work behind the same client interface.
- Page IDs are passed in whatever form the buildin Bot API accepts (UUID
  with or without dashes). Normalising / validating the ID format is part
  of the buildin client's responsibility, not this feature's.
- Buildin pages may have arbitrarily many child blocks, but the test
  fixtures used here represent realistic short-to-medium pages; whole-tree
  pagination handling is implemented if and only if the underlying client
  already supports it. If the client does not yet paginate, this feature
  documents the page-size ceiling rather than silently truncating.
- Terminal-capability detection uses standard signals (stdout is a TTY,
  `NO_COLOR` env var, `TERM`, the underlying rendering library's own
  detection) rather than a custom probe. The CLI framework
  (`Spectre.Console.Cli`) and its broader `Spectre.Console` ecosystem are
  expected to provide this detection out of the box.
- The MCP resource returns the page body only — no buildin metadata
  (created/last-edited timestamps, author, parent chain) — in this
  feature. Including metadata is a future, additive change.
- Inline formatting (bold, italic, inline code, links) inside a supported
  block is in scope as part of "supporting" that block; it is not a
  separate block type.
- Specific tooling decisions — the exact MIME type returned, the exact
  placeholder syntax, the styled renderer used for the rich CLI mode, the
  precise mock-HTTP mechanism — are deferred to `/speckit-plan`.
