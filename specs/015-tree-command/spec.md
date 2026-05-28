# Feature Specification: Tree Command

**Feature Branch**: `015-tree-command`
**Created**: 2026-05-28
**Status**: Draft
**Input**: User description: "Tree command that returns a tree-map for a page in ASCII (markdown links, drawn like the Unix `tree` command) or JSON format. Available in both MCP and CLI. Format is selectable (or via content-type for MCP). Supports `depth` option (default 3, maximum 7). Only page and database links are rendered — no content blocks."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Get a Visual Overview of a Page Hierarchy (Priority: P1)

A workspace user wants a quick, scannable map of how a given page is structured — what sub-pages and embedded databases live underneath it, and how they nest. They invoke the tree command against a root page and receive an indented, `tree`-style rendering with each node shown as a clickable markdown link. They can paste this output directly into notes, documentation, or a chat message without further formatting.

**Why this priority**: This is the core value proposition — turning an opaque page hierarchy into a glanceable map. A human-readable ASCII tree is the most common use case for both CLI users (terminal output) and LLM-driven workflows (the agent can summarize or paste it). Without this, the feature has no MVP.

**Independent Test**: Can be fully tested by invoking the tree command (CLI or MCP) against a known page that contains nested sub-pages and databases, with no extra options, and verifying the output is a valid ASCII tree starting at the given page, with each line consisting of a markdown link `[Name](<URI>)`, drawn with `├──`, `└──`, and `│` connectors matching the Unix `tree` command convention.

**Acceptance Scenarios**:

1. **Given** a page "Foo" containing two child sub-pages "Bar" and "Baz", **When** the user invokes the tree command on "Foo" with no options, **Then** the output is:
   ```
   [Foo](<foo-uri>)
   ├── [Bar](<bar-uri>)
   └── [Baz](<baz-uri>)
   ```
2. **Given** a page with nested sub-pages three levels deep, **When** the user invokes the tree command with default depth (3), **Then** the output shows the full nesting using `│` to indicate continued vertical branches and `└──` for the last child at each level.
3. **Given** a page that contains a mix of sub-pages, embedded databases, and regular content blocks (paragraphs, lists, headings), **When** the user invokes the tree command, **Then** only sub-pages and embedded databases appear in the output; paragraphs, lists, headings, and other blocks are omitted.
4. **Given** a page with no child pages or databases, **When** the user invokes the tree command, **Then** the output contains only the root node as a single markdown link, with no connector characters.

---

### User Story 2 - Consume the Tree Programmatically (Priority: P2)

An LLM agent or a script needs the page hierarchy in a structured shape so it can iterate over children, filter by name, or feed the data into another tool. The user invokes the tree command with a JSON format selector and receives a recursive object containing the page name, URI, and an array of child nodes following the same shape.

**Why this priority**: ASCII is for humans; JSON unlocks automation. Without a machine-readable format, agents and scripts must parse the ASCII rendering, which is brittle. P2 because the ASCII form alone delivers the core overview value; JSON extends the audience.

**Independent Test**: Can be fully tested by invoking the tree command with the JSON format selector against a page with sub-pages, parsing the output as JSON, and verifying that the root object contains `name`, `uri`, and `children` fields and that `children` is a recursive array of nodes with the same shape.

**Acceptance Scenarios**:

1. **Given** the page "Foo" with sub-pages "Bar" and "Baz", **When** the user invokes the tree command with `--format json` (CLI) or the `format=json` parameter (MCP), **Then** the output is valid JSON of the shape:
   ```json
   {
     "name": "Foo",
     "uri": "<foo-uri>",
     "children": [
       { "name": "Bar", "uri": "<bar-uri>", "children": [] },
       { "name": "Baz", "uri": "<baz-uri>", "children": [] }
     ]
   }
   ```
2. **Given** a deeply nested page hierarchy, **When** the user invokes the tree command with JSON format, **Then** the `children` array nests recursively to reflect the actual hierarchy.
3. **Given** the CLI is invoked without a `--format` flag, **Then** the default format is ASCII.

---

### User Story 3 - Control Traversal Depth (Priority: P3)

A user dealing with a large workspace wants to avoid pulling thousands of nested nodes at once. They specify a `depth` option to limit how many levels deep the tree traversal goes, trading completeness for speed and readability. They can also request a deeper traversal (up to a hard maximum) when they need more detail.

**Why this priority**: Depth control is essential for usability with real workspaces, but the default depth of 3 is enough for most cases — so this story is P3 rather than blocking the MVP.

**Independent Test**: Can be fully tested by invoking the tree command with various `depth` values (1, 3, 7, 0, 8) on a page with at least 7 levels of nesting and verifying that the output respects the depth boundary, that the default behaves identically to `depth=3`, that `depth=7` returns the maximum, and that `depth=8` (or higher) is rejected with a clear error.

**Acceptance Scenarios**:

1. **Given** a page hierarchy nested 5 levels deep, **When** the user invokes the tree command with default options, **Then** the output shows exactly 3 levels of descendants below the root.
2. **Given** the same page, **When** the user invokes the tree command with `--depth 7` (CLI) or `depth=7` (MCP), **Then** the output shows up to 7 levels of descendants below the root.
3. **Given** the same page, **When** the user invokes the tree command with `--depth 1`, **Then** the output shows only the root and its immediate children.
4. **Given** any page, **When** the user invokes the tree command with `--depth 8` or higher, **Then** the command rejects the invocation with a clear error message identifying the maximum allowed depth (7).
5. **Given** any page, **When** the user invokes the tree command with `--depth 0` or a negative value, **Then** the command rejects the invocation with a clear error message identifying the minimum allowed depth (1).

---

### Edge Cases

- **Root page not found or inaccessible**: The command MUST fail fast with an error identifying the missing page; no partial tree is returned.
- **Descendant page read fails mid-traversal** (transient error, rate limit, permission denied): The traversal MUST continue; the failing node is rendered with name `(unavailable)` and treated as a leaf. The failure is logged but does not abort the command.
- **Truncation at depth boundary**: When the traversal stops because the depth limit was reached but more descendants exist, the output renders the deepest visible nodes normally. There is no special marker indicating that descendants were elided beyond the boundary. (The user knows the depth they requested.)
- **Cycles in the hierarchy**: The workspace data model does not permit cycles in the page/database tree, so the command assumes a directed acyclic structure. If a cycle is somehow encountered (e.g., a future API anomaly), the command MUST detect repeated nodes and abort with a clear error rather than loop infinitely.
- **Empty page names**: A page or database with an empty or whitespace-only name MUST still appear in the tree with its name rendered as `(untitled)` so the link target is preserved.
- **Markdown-significant characters in names**: Page names containing `]`, `[`, `<`, `>`, `\\`, or `)` MUST be rendered such that the markdown link in the ASCII format remains parseable (e.g., by escaping or using the angle-bracket URI form). JSON output preserves names verbatim.
- **Wide trees**: A page with hundreds of immediate children MUST render every child up to the depth limit; there is no per-level cap. The depth limit is the only traversal bound.
- **Tree command invoked on a database root**: The command MUST treat a database the same as a page — render the database as the root node and enumerate the records/pages it contains as children, subject to the same depth rules.
- **Mixed page and database children**: Both child pages and child databases at the same level MUST appear as siblings in the output, rendered identically (the rendering does not distinguish page from database visually).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a `tree` operation that, given a page or database UUID (the same identifier shape used by `get_page_markdown`, `update_page`, and other existing tools), returns a hierarchical map of that node and its descendant pages and databases. URLs, workspace paths, and other identifier forms are out of scope; callers must resolve them to a UUID through existing means (e.g., search).
- **FR-002**: The tree operation MUST be available both as a CLI command and as an MCP tool, with equivalent semantics and parameters.
- **FR-003**: The tree operation MUST support two output formats: an ASCII rendering and a JSON rendering. ASCII is the default when no format is specified.
- **FR-004**: In the ASCII format, each node MUST be rendered as a markdown link of the form `[Name](<URI>)`, with the URI wrapped in angle brackets to preserve any special characters. The URI MUST be the page or database's buildin.ai web URL (the same browser-resolvable URL the workspace presents to users), so the rendered link is directly clickable when pasted into any Markdown surface.
- **FR-005**: In the ASCII format, the hierarchy MUST be drawn using the Unix `tree`-style box-drawing characters: `├── ` for intermediate children, `└── ` for the last child at a given level, `│   ` to continue a vertical branch, and `    ` (four spaces) where no vertical branch continues.
- **FR-006**: In the JSON format, each node MUST be serialized as an object containing exactly three fields: `name` (string), `uri` (string — the buildin.ai web URL of the node, same form as FR-004), and `children` (array of nodes of the same shape). Leaf nodes MUST have `children: []`, not a missing field.
- **FR-007**: The tree operation MUST accept a `depth` parameter (CLI flag `--depth`, MCP parameter `depth`). Valid values are integers from 1 through 7 inclusive. The default is 3.
- **FR-008**: A `depth` value of 1 MUST return the root node and its direct children only. A `depth` value of N MUST return the root and up to N levels of descendants.
- **FR-009**: The tree operation MUST reject `depth` values less than 1 or greater than 7 with a clear error message naming the allowed range.
- **FR-010**: The tree operation MUST include only child pages and embedded/child databases. It MUST NOT include any other block types (paragraphs, lists, headings, callouts, code blocks, images, etc.).
- **FR-011**: The CLI MUST accept a `--format` flag with values `ascii` (default) and `json`. The MCP tool MUST accept a `format` parameter with the same values and the same default. Format selection is parameter-only on both surfaces; HTTP `Accept`-header / content-type negotiation is NOT honored (the LLM cannot influence HTTP headers through MCP tool calls, so a parameter is the only mechanism that is actually reachable by the primary consumer).
- **FR-012**: When the **root** target page or database does not exist or is not accessible, the tree operation MUST fail fast with a clear error identifying the requested identifier; it MUST NOT return a partial tree.
- **FR-012a**: When a **descendant** page or database read fails during traversal (transient API error, rate limit, permission gap), the operation MUST continue traversal of the remaining tree and render the failing node with the placeholder name `(unavailable)`. Its `uri` field MUST be set to the node's known URI if available, or an empty string otherwise; its `children` MUST be `[]`. Every such failure MUST be logged for diagnostics, including the failing node's identifier and the underlying error.
- **FR-013**: The tree operation MUST traverse the hierarchy by following the existing parent/child relationships exposed by the underlying page-and-database API; no new relationship model is introduced.
- **FR-014**: Sibling order in the output MUST match the order returned by the underlying API (the workspace's natural ordering); no client-side sorting is applied.
- **FR-015**: Names rendered in the ASCII format MUST escape or be rendered in a way that preserves the integrity of the markdown link syntax (so a name containing `]` does not break the link).
- **FR-016**: A node whose name is empty or whitespace-only MUST be rendered with the placeholder name `(untitled)` in both formats.

### Key Entities

- **TreeNode**: A node in the rendered hierarchy. Attributes: `name` (the page or database title; `(untitled)` if empty), `uri` (the buildin.ai web URL of the page or database — browser-resolvable), `children` (an ordered list of TreeNodes, possibly empty).
- **TreeRequest**: The input to the operation. Attributes: target UUID (page or database; same shape as other tools accept), `format` (ascii or json; default ascii), `depth` (integer 1–7; default 3).

## Clarifications

### Session 2026-05-28

- Q: How does the caller specify the root page or database? → A: UUID only — same identifier shape as every other tool in the project. URLs/paths must be resolved upstream.
- Q: What URI is rendered in the `[Name](<URI>)` link and the `"uri"` JSON field? → A: The buildin.ai web URL of the page/database, so the rendered links are directly clickable when pasted into any Markdown surface.
- Q: How should the MCP tool handle format selection — parameter only, content-type only, or both? → A: Parameter only. HTTP `Accept`-header negotiation is out of scope because the LLM cannot influence HTTP headers through MCP tool calls; the parameter is the only mechanism reachable by the primary consumer.
- Q: What happens when a sub-page read fails mid-traversal (transient API failure / rate limit / permission gap)? → A: Render a partial tree; the failing descendant is shown with name `(unavailable)` and treated as a leaf, the failure is logged, and the rest of the traversal continues. Only a failed read of the **root** target aborts the command.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user invoking the tree command on a known page with default options receives a correct, complete ASCII rendering of that page's hierarchy down to 3 levels in a single command invocation.
- **SC-002**: The JSON output for any tree is a valid JSON document that round-trips through a standard JSON parser without modification and exposes the same node set and ordering as the ASCII output for the same request.
- **SC-003**: For any page with up to 100 descendants within the requested depth, the tree command returns its result in under 3 seconds from a warm workspace.
- **SC-004**: A user can switch between ASCII and JSON formats by changing only a single flag/parameter; the same identifier and depth produce equivalent trees in both formats.
- **SC-005**: Invalid `depth` values are rejected with an error message that names the allowed range (1–7), so a user can correct the invocation on the first attempt without consulting documentation.
- **SC-006**: No content blocks (paragraphs, headings, lists, etc.) ever appear in the tree output, verified by running the command against pages with rich block content and inspecting the output.

## Assumptions

- The underlying page-and-database API already exposes the parent/child relationships needed to traverse from a given page to its sub-pages and embedded databases; no new ingestion or relationship model is required.
- The buildin.ai web URL for any given page or database UUID is available (or trivially derivable) from the existing API client; deriving it is not novel work for this feature.
- Traversal performance is acceptable using the existing read APIs; if a wide tree triggers many round-trips, optimization (batching, caching) can be addressed in a future iteration. The read cache feature ([012-read-cache](../012-read-cache/spec.md)) already mitigates repeated reads.
- The MCP tool returns text content; the `format` parameter is the sole mechanism for selecting output shape on both CLI and MCP. HTTP `Accept`-header negotiation is intentionally out of scope (see Clarifications).
- ASCII output is intended for direct paste into Markdown-capable surfaces (chat, docs, READMEs). The angle-bracket URI form is preferred because it tolerates a wider range of characters than the bare-URL form.
- This feature is purely read-only; it does not modify any page, database, or block.
- The maximum depth of 7 is a guardrail against runaway traversals; users who genuinely need more can issue multiple tree commands rooted at deeper nodes.
- Authentication and authorization are handled by the existing API client; the tree command surfaces only pages and databases the caller is already allowed to read.
