# Feature Specification: Markdown Page Update via Patch Operations

**Feature Branch**: `008-markdown-page-update`
**Created**: 2026-05-16
**Status**: Draft
**Input**: User description: "Update tool for Buildin.ai pages using markdown-native patching instead of full-page rewrite. The agent edits a markdown projection (with stable anchors) and the server reconciles the patched markdown back into the canonical block graph while preserving block identity."

## Clarifications

### Session 2026-05-16

- Q: How should "section" semantics work for the block-replacing patch operation, given that heading + content are siblings in buildin's tree? → A: Provide both operations. `replace_block` replaces the anchored block plus its tree descendants and accepts any anchor type; `replace_section` accepts a heading anchor only and replaces the heading plus all following sibling blocks up to the next heading of equal-or-higher level (or end of parent). A `replace_section` against a non-heading anchor fails with a new patch-error class `patch.section_anchor_not_heading`.
- Q: How is the revision token derived? → A: Content hash of the anchored-Markdown body — specifically CRC32 of the UTF-8 bytes of the anchored Markdown, rendered as a lowercase 8-character hex string (e.g., `1a2b3c4d`). CRC32 was chosen over SHA-256 to keep the token short, since the LLM caller carries the revision on every `update_page` call and a 64-char hash burns context for no security benefit (the server recomputes from current state and compares byte-for-byte; the token is never trusted client-side). Collisions across a single page's edit history are astronomically rare and would surface as a successful update against a stale snapshot — protected against by the deterministic-render contract in FR-002.
- Q: How does this feature integrate with spec 007's observability infrastructure? → A: Light-touch cross-reference. `get_page_markdown` and `update_page` calls flow through spec 007's existing per-call instrumentation (each call gets a span; each failure produces an error-tagged log entry) without this spec inventing new metric or span names. The new `patch.*` error classes MUST appear as a recognised error-class dimension on whatever spec 007 already emits. Exact metric/log attribute names are a `/speckit-plan` decision aligned with spec 007's conventions.
- Q: Should the edit-mode read be an MCP resource or a tool, and how does the CLI surface it? → A: MCP stays as a tool (`get_page_markdown`) — the structured triple `{ markdown, revision, unknown_block_ids }` is awkward to model as a resource body, and tools natively carry structured payloads with atomic per-call snapshot semantics. The spec 002 `buildin://{page_id}` resource is preserved untouched (humans / unanchored read). CLI does NOT add a new `fetch` command; it extends the existing `buildout get <page_id>` command with an `--editing` flag that switches the same command into edit-mode read (anchored Markdown + revision + `unknown_block_ids`). Without the flag, `get` is byte-identical to spec 002's behavior.
- Q: How is the page root referenced in anchored Markdown, and what does a root-anchored op target? → A: Emit a distinct sentinel `<!-- buildin:root -->` as the very first line of the anchored-Markdown body (immediately before the `# Title` H1 from spec 002 / 006). The root sentinel is a separate anchor form from block anchors — cleaner IR separation between "the page" and "its blocks". Patch operations whose `anchor` field equals the literal string `root` resolve to this sentinel. A `replace_block` with `anchor: "root"` replaces the page's child blocks only; the new Markdown's leading H1 (if any) becomes the new page title per spec 006 FR-005's title-consumption rule. Icon, cover, and other page properties are NOT editable through any operation in this feature (they remain out of scope, deferred to a future spec). The root anchor is implicitly a non-heading, so `replace_section` against it fails with `patch.section_anchor_not_heading` (unchanged from prior session's clarification).
- Q: How should the reconciler handle a patch whose effect is a tree-level reorder of existing blocks (the same anchor appears at a different position in the patched Markdown)? → A: Reject it. The reconciler classifies any anchor present in both the pre- and post-patch ASTs but at a different tree position as a failure with a new error class `patch.reorder_not_supported`. Rationale: even in the buildin UI, users don't "move" blocks — they cut and re-insert, which is delete + create. The patch contract mirrors that mental model: callers express moves as an explicit pair of ops (e.g., a `search_replace` that drops the anchor + an `append_section` / `insert_after_block` that introduces a fresh block). This is also the only honest contract given that the Notion-family API has no native move primitive, so any "preserve IDs across reorder" guarantee would have been unimplementable. Buildin block IDs, comments, backlinks, and other collaboration metadata cannot survive a move under this contract; agents who care must be explicit.
- Q: What is `replace_section`'s boundary scope when the anchored heading is inside a container block (column, toggle, callout, list item with children, etc.)? → A: Same-parent only. The forward scan that determines the section's extent walks the heading's *immediate-sibling list* only — it stops at the end of the parent's children, or at the first sibling heading of equal-or-higher level, whichever comes first. Container boundaries are hard limits: a `replace_section` anchored at a heading inside a column never reaches outside that column, even if a peer heading exists later at the page-root level. This keeps the operation's blast radius proportional to the parent and prevents accidents like rewriting a callout's contents because it contains a peer-level heading.
- Q: What happens when `append_section` is anchored at a block type that cannot have children (a paragraph, code block, divider, image, etc.) — and how do agents express "insert a sibling after this non-heading block"? → A: Two changes to the operation set. (1) `append_section` against a non-container block fails with a new error class `patch.anchor_not_container`; the reconciler MUST consult a per-block-type "can have children" predicate (sourced from spec 002's compatibility matrix — page root, toggle headings, list / to-do items, quote, callout, column, toggle, table row are containers; paragraph, code, divider, image, and other leaves are not) and reject before any write is issued. (2) `insert_after_heading` is generalised and renamed to `insert_after_block`: it now accepts any block anchor (heading or otherwise) and inserts the new Markdown as a sibling at the same depth, immediately after the anchored block. Together these give two unambiguous primitives — "child of this container" (`append_section`) and "sibling after this block" (`insert_after_block`) — without any silent fallback behaviour. The op count stays at five.

## User Scenarios & Testing *(mandatory)*

This feature delivers the first user-visible *edit* capability of buildout.
Spec 002 produced the read path (blocks → Markdown) and spec 006 produced
the create path (Markdown → new page); this feature produces the missing
third corner — Markdown → *existing* page — without violating
constitution Principle VI (non-destructive editing).

The conceptual shift is that the LLM does not edit raw buildin blocks. It
edits an *enhanced-Markdown projection* of the page that carries stable
per-block anchors as HTML comments, submits one or more *semantic patch
operations* against that projection, and the core library reconciles the
patched Markdown back into the canonical block graph at AST level —
preserving block IDs (and the comments, backlinks, timestamps, and other
collaboration metadata attached to them) for every block that survives
the edit.

A full-page `replace_content` is deliberately *not* offered: such an
operation cannot honour Principle VI because every block in the page
would be destroyed and recreated. Bulk page rewrites remain the job of
spec 006's `create` (against a new parent).

### User Story 1 — Fetch an editable Markdown projection of a page (Priority: P1)

As a developer or LLM that intends to edit a buildin page, I run
`buildout get <page_id> --editing` (or invoke the MCP
`get_page_markdown` tool) and receive (a) a Markdown rendering of the
page that carries a stable hidden anchor for every block in the page,
(b) an opaque revision token identifying the snapshot I just read,
and (c) a list of any block IDs the renderer could not safely
represent in Markdown (unsupported block types). The anchored
Markdown is the input my next call to `update` will reference.
Without `--editing`, `buildout get` continues to emit the
unanchored Markdown spec 002 defined (the human-facing read).

**Why this priority**: An update operation that cannot first emit
anchored Markdown is unimplementable end-to-end. This is the smallest
slice that lets an agent observe what it is about to patch.

**Independent Test**: With a mocked buildin client, fetch a fixture page
that contains every supported block type from spec 002's compatibility
matrix. Assert: (a) every supported block in the page emits a
`<!-- buildin:block:<id> -->` line on its own immediately above the
block's Markdown, (b) the body Markdown stripped of those anchor
comments equals the spec 002 read output for the same page (the read
side's wire form is not regressed for callers that do not care about
anchors), (c) the response includes a non-empty `revision` string, (d)
`unknown_block_ids` lists every block type the page contains that the
spec 002 converter declares unsupported, and is empty when the page
contains only supported blocks.

**Acceptance Scenarios**:

1. **Given** a fixture page containing a paragraph, an H2, a bulleted
   list with nested sub-bullets, and a fenced code block, **When** the
   developer runs `buildout get <page_id> --editing`, **Then** the
   output is anchored Markdown with one anchor comment per supported
   block (top level and nested), the revision token is present, and
   `unknown_block_ids` is empty. Running the same command without
   `--editing` MUST produce the spec 002 unanchored Markdown
   byte-for-byte — the flag is the only switch between modes.
2. **Given** the same page, **When** the MCP `get_page_markdown` tool is
   invoked with `{ "page_id": "<id>" }`, **Then** the response contains
   the same anchored Markdown, the same revision token, and the same
   `unknown_block_ids` list as the CLI run — modulo wire-form differences
   already established for read tools.
3. **Given** a page containing an iframe block (currently unsupported
   per spec 002 / 006 FR-003), **When** the page is fetched, **Then**
   the iframe block's ID appears in `unknown_block_ids` and a
   *protected placeholder* anchor (FR-004) is emitted in place of the
   unsupported block — never an editable approximation of its content.
4. **Given** the same fetch invoked twice with no intervening edits,
   **When** the second result is compared to the first, **Then** the
   anchored Markdown is byte-identical and the revision token is
   identical (deterministic serialization).

---

### User Story 2 — Patch a page with semantic operations (Priority: P1)

As a developer or LLM that has just fetched anchored Markdown, I submit
one or more *patch operations* against that snapshot:

- `replace_block` — replace the anchored block (and its tree
  descendants) with new Markdown. Accepts any anchor type.
- `replace_section` — replace a heading-rooted section: the heading
  identified by the anchor plus all sibling blocks following it up
  to the next heading of equal-or-higher level (or end of parent).
  Accepts a heading anchor only.
- `search_replace` — replace a unique substring of the page Markdown
  with new Markdown,
- `append_section` — append new Markdown as children under an anchored
  *container* block (or at the end of the page when no anchor is
  given); leaf-anchored calls fail with `patch.anchor_not_container`,
- `insert_after_block` — insert new Markdown immediately after a
  referenced block, at the same depth, regardless of block type.

The core library applies the operations in order against the anchored
Markdown, parses the result back into a buildin AST, diffs that AST
against the canonical block tree it cached at fetch time, and issues
the minimal set of buildin write calls (`appendBlockChildren`,
`updateBlock`, `deleteBlock`) that reproduces the patched AST while
*preserving the buildin block ID of every block whose anchor was not
changed*. The call returns a *reconciliation summary*:
`{ preserved_blocks, new_blocks, deleted_blocks, ambiguous_matches,
new_revision }`.

**Why this priority**: This is the feature. Everything else exists to
make this operation safe and observable.

**Independent Test**: Build a fixture matrix (one fixture per
operation type × at least one happy path and one error path each).
For each fixture: fetch the page through US1 against the mocked client,
apply the operation through this story's CLI/MCP surface, and assert:
(a) the recorded sequence of buildin write calls is the minimal set
required (zero `updateBlock` calls for any block whose Markdown was
unchanged after reconciliation; zero `deleteBlock` calls for any block
whose anchor survives), (b) the reconciliation summary's counts match
the recorded write calls, (c) re-fetching the page (US1 again) yields
the expected post-edit anchored Markdown, (d) anchors for surviving
blocks retain the *same buildin block ID* before and after the edit.

**Acceptance Scenarios**:

1. **Given** a page with a heading "## API" followed by a paragraph,
   and **When** the user submits one `replace_block` operation
   anchored at the paragraph's block ID with new paragraph Markdown,
   **Then** exactly one `updateBlock` call is issued against that
   paragraph's block ID, no other blocks are touched, the heading
   block's ID is unchanged, and the new revision is returned.
1a. **Given** a page laid out as `## API\n<p1>\n<p2>\n## Notes\n<p3>`,
   **When** the user submits one `replace_section` operation
   anchored at the `## API` heading with new Markdown containing a
   single replacement paragraph, **Then** the heading block's ID is
   preserved, `<p1>` and `<p2>` are deleted (and the new paragraph
   is appended in their place), `## Notes` and `<p3>` are not
   touched, and the new revision is returned.
1b. **Given** the same page, **When** the user submits a
   `replace_section` operation anchored at the `<p1>` paragraph's
   block ID, **Then** the call fails with
   `patch.section_anchor_not_heading`, the error names the offending
   anchor, and no write call is issued. A `replace_block` against
   the same anchor would have succeeded.
2. **Given** a page whose body contains the unique substring
   "old text", **When** the user submits a `search_replace` of
   "old text" → "new text", **Then** the unique containing block is
   identified by AST diff and a single `updateBlock` issued against
   it; surrounding blocks are untouched.
3. **Given** a `search_replace` whose `old_str` matches more than once
   in the page, **When** the operation runs, **Then** the call fails
   before any write is issued and the failure carries the error class
   `patch.ambiguous_match` with the ambiguous string echoed back.
4. **Given** a `search_replace` whose `old_str` matches zero times in
   the page, **When** the operation runs, **Then** the call fails with
   `patch.no_match` and no write is issued.
5. **Given** an `append_section` anchored to a bulleted list block,
   **When** the operation supplies new bullets in Markdown, **Then**
   the new bullets are created as children of that list block (one
   `appendBlockChildren` call against the list block's ID) and the
   existing children of the list keep their block IDs.
6. **Given** an `append_section` with no anchor and new top-level
   Markdown, **When** the operation runs, **Then** the new blocks
   are appended at the end of the page (one `appendBlockChildren`
   against the page) and every existing block keeps its ID.
7. **Given** an `insert_after_block` referencing the H2 "## API"'s
   anchor, **When** the operation supplies a new paragraph, **Then**
   the new paragraph block is inserted in document order immediately
   after the H2 (and before whatever block previously followed the
   H2) and no existing block's ID changes. The same operation
   anchored at a *paragraph's* block id behaves symmetrically —
   the new content lands immediately after that paragraph as a
   sibling — because `insert_after_block` accepts any anchor type.
7a. **Given** an `append_section` anchored at a *paragraph* block
   id, **When** the operation runs, **Then** it fails with
   `patch.anchor_not_container` (paragraphs are leaf blocks under
   buildin's data model and cannot accept children); the failure
   names the offending anchor and suggests `insert_after_block`
   as the sibling-insert counterpart. No write call is issued.
8. **Given** an operation references an anchor that is not present in
   the snapshot, **When** the operation runs, **Then** the call fails
   with `patch.unknown_anchor` (naming the missing anchor) before any
   write is issued.

---

### User Story 3 — Optimistic concurrency: stale revisions are rejected (Priority: P1)

As a developer or LLM that may share a page with humans or other
agents, I include the revision token I read at fetch time in every
update call. If the page changed server-side between my fetch and my
update, my call fails before any write is issued, and I am expected
to re-fetch and re-derive my patch against the new snapshot.

**Why this priority**: Without this, a slow agent silently overwrites
a fast human. Optimistic concurrency is the cheapest safe-by-default
posture and the only one that composes with future collaborative
extensions.

**Independent Test**: Two-step test against the mocked client. Fetch a
page (US1) → recording revision `r0`. Simulate a server-side edit by
mutating the mock's stored page so its derived revision is now `r1`.
Submit a patch against `r0`. Assert: the call fails with
`patch.stale_revision`, the response carries the current revision
(`r1`) so the caller can re-fetch, and no buildin write call was
issued.

**Acceptance Scenarios**:

1. **Given** the page is unchanged between fetch and update, **When**
   the update call carries the matching revision, **Then** the patch
   applies and the response carries a new revision distinct from the
   one supplied.
2. **Given** the page has changed server-side between fetch and
   update, **When** the update call carries the stale revision,
   **Then** the patch fails with `patch.stale_revision`, the current
   revision appears in the error payload, and no write call is
   issued.
3. **Given** the update call omits the revision field entirely,
   **When** the call runs, **Then** it fails with a
   validation-class error and no write call is issued — the revision
   is not optional.

---

### User Story 4 — Dry-run preview before commit (Priority: P2)

As a developer or LLM about to apply a patch I am not 100% sure of, I
set `dry_run: true` on the update call. The core library performs the
full operation — parse, apply ops, AST diff, classify each block as
preserved / created / deleted — but issues no buildin write calls.
The response is the same reconciliation summary the committing call
would have returned (`preserved_blocks`, `new_blocks`,
`deleted_blocks`, `ambiguous_matches`) plus the rendered post-edit
anchored Markdown. I inspect the summary, then re-issue the same call
with `dry_run: false` to commit.

**Why this priority**: A safety mechanism for the cases the four
error classes in US2 do not cover — e.g., a `replace_section` whose
new Markdown is much shorter than the old subtree and would cascade
into many deletions. P2 because US2 is usable without it for small,
local edits.

**Independent Test**: For each operation type in US2, run the
operation with `dry_run: true` against the mocked client. Assert: the
mocked client records zero write calls, the response carries the
same `{ preserved_blocks, new_blocks, deleted_blocks }` counts that
the same call would have produced without `dry_run`, and the
post-edit anchored Markdown rendered in the response is what a
subsequent fetch would have returned had the patch committed.

**Acceptance Scenarios**:

1. **Given** any valid patch, **When** the call carries
   `dry_run: true`, **Then** zero buildin write calls are recorded
   and the response contains the reconciliation summary plus the
   post-edit anchored Markdown.
2. **Given** the same patch is then re-issued with `dry_run: false`
   against the same revision, **When** the call runs, **Then** the
   recorded buildin write calls exactly produce the AST the dry-run
   previewed, and the new revision differs from the one supplied.
3. **Given** an error-class patch (ambiguous match, unknown anchor,
   etc.), **When** the call carries `dry_run: true`, **Then** the
   same error is surfaced as it would be in a committing call — dry
   run does not suppress validation failures.

---

### User Story 5 — Large-delete safety guard (Priority: P2)

As a developer or LLM that is otherwise free to author small patches,
I am protected against a runaway patch that would delete a large
fraction of the page in a single call. The core library counts the
blocks the reconciliation would delete; if the count exceeds a
configured threshold, the call fails with `patch.large_delete` and
the caller must either rewrite the patch to be more local *or*
explicitly set `allow_large_delete: true` to acknowledge the blast
radius.

**Why this priority**: A common LLM failure mode is to "rewrite" a
section by emitting a `replace_section` whose new Markdown happens
to be empty, or by emitting a `search_replace` whose `new_str` drops
many bullets. The guard catches both. P2 because it is a backstop,
not the main path.

**Independent Test**: With a 20-block fixture page, submit a
`replace_block` against the page-root anchor whose new Markdown
contains a single paragraph. The reconciliation would delete 19
blocks. Assert the call fails with `patch.large_delete` without
writing. Re-issue with `allow_large_delete: true` and assert the
call now succeeds and the recorded `deleteBlock` calls match the
previewed count.

**Acceptance Scenarios**:

1. **Given** a patch whose reconciliation would delete a number of
   blocks at or below the threshold, **When** the call runs without
   `allow_large_delete`, **Then** the patch commits normally.
2. **Given** a patch whose reconciliation would delete more blocks
   than the threshold, **When** the call runs without
   `allow_large_delete`, **Then** the call fails with
   `patch.large_delete`, the would-be-deleted count appears in the
   error payload, and no write call is issued.
3. **Given** the same patch with `allow_large_delete: true`, **When**
   the call runs, **Then** the patch commits and the recorded
   `deleteBlock` calls equal the previewed count.
4. **Given** an `allow_large_delete: true` patch under `dry_run`,
   **When** the call runs, **Then** the reconciliation summary is
   returned (no writes) and the previewed delete count matches what
   a committing call would have deleted.

---

### Edge Cases

- **Patch references the page-root anchor**: `replace_block` with
  `anchor: "root"` (resolving to the `<!-- buildin:root -->`
  sentinel from FR-003) is treated as a structural request to
  replace the page's children, and optionally the title via a
  leading `# Heading` in the new Markdown; it is subject to the
  large-delete guard like any other patch. It is *not* a
  back-door full-page rewrite — the page itself is never deleted,
  icon and cover are never touched, and the patch is rejected if
  the parsed result contains zero children (use spec 006's
  `create` if a fresh page is what you want). The page-root
  anchor is not a heading, so `replace_section` against it fails
  with `patch.section_anchor_not_heading`. `append_section` with
  no `anchor` field continues to append at the end of the page
  (children list), independently of the root sentinel.
- **Patch touches an unsupported-block placeholder**: A placeholder
  emitted for an unknown block carries a protected anchor (FR-004).
  Any operation whose effect would alter, delete, or duplicate that
  anchor fails with `patch.unsupported_block_touched`; the unknown
  block is preserved verbatim. Operations may freely add or remove
  blocks *around* the placeholder.
- **`search_replace` whose `old_str` spans multiple blocks**: Allowed
  only when the AST diff yields an unambiguous structural mapping —
  i.e., the spanned blocks form a contiguous run and the new text
  parses back into the same number of blocks of the same types in
  the same order. Otherwise the operation is rejected with
  `patch.ambiguous_match` and a hint that the caller should use
  `replace_block` (single-block subtree) or `replace_section`
  (heading-rooted section) instead.
- **Anchor present in the patched Markdown that was *not* present in
  the snapshot**: Treated as a forgery — the patch is rejected with
  `patch.unknown_anchor`. Callers cannot fabricate buildin block IDs;
  new blocks emerge from new anchor-less Markdown and get IDs
  assigned server-side.
- **Anchor present in the snapshot that is *absent* from the patched
  Markdown**: That block is scheduled for deletion. Subject to the
  large-delete guard. The mere act of regenerating the page's
  Markdown without re-emitting every anchor is *not* a safe pattern;
  callers MUST start from the snapshot the fetch returned.
- **Operations issued in one call that would conflict**: Operations
  are applied in order to the in-memory anchored Markdown. If
  operation `n` references an anchor that operation `n-1` removed,
  the call fails with `patch.unknown_anchor` at operation `n` and no
  writes are issued. Partial-success is not a state this feature
  exposes.
- **Empty `operations` array**: The call fails with a
  validation-class error before any write is issued. A no-op update
  is expressible only as `dry_run: true` against a real operation,
  not as a committing call with no operations.
- **Operation that would split or merge a list across its block
  boundary**: Supported insofar as the reconciler can match
  individual list-item blocks by anchor; surviving items keep their
  IDs, new items are created, removed items are deleted, subject to
  the large-delete guard. The list-parent block ID is preserved if
  the list still exists after the patch.
- **Patch whose anchored Markdown fails to parse as CommonMark
  after the operations are applied**: Validation-class error before
  any write is issued. The error names the operation index whose
  output broke parsing.
- **Authentication failure on the buildin write call**: Same
  handling as features 002 / 003 / 006 — distinct exit code on CLI,
  MCP-protocol error on MCP.
- **Buildin returns a 4xx on a single child write mid-reconciliation
  (e.g., a malformed block payload)**: The patch is partially
  applied. The error response carries the partial revision, the
  list of operations that committed, the operation that failed, and
  the buildin error. No automatic rollback in v1 (matches spec 006's
  partial-create posture).
- **Concurrent successful updates by the same caller**: The revision
  token from the previous update is the input revision for the next
  update. Each call is independently checked; there is no batching
  or transactional grouping across calls.

## Requirements *(mandatory)*

### Functional Requirements

#### Anchored Markdown projection (core)

- **FR-001**: The shared core library MUST expose a single
  fetch-for-edit operation that takes a buildin page id and returns
  `{ markdown, revision, unknown_block_ids }`. Both presentation
  surfaces MUST go through this operation; neither may render
  anchored Markdown locally, nor compute the revision token locally,
  nor consult the buildin client directly.
- **FR-002**: The anchored Markdown MUST be deterministic: the same
  page state MUST produce byte-identical Markdown and the same
  revision token across repeated reads, independent of host time and
  process. The Markdown body stripped of anchor comments MUST equal
  the spec 002 read output for the same page (anchors do not
  perturb the unanchored projection).
- **FR-003**: Anchored Markdown uses three anchor forms, all
  emitted as standalone HTML comments that survive a CommonMark
  roundtrip through any conforming parser (no fenced or escaped
  forms):
  - **Root sentinel** — `<!-- buildin:root -->`. Emitted exactly
    once, as the very first line of the anchored-Markdown body,
    immediately before the `# Title` H1 inherited from spec 002 /
    006. Represents the page itself (not any block); the
    `anchor` field on a patch operation resolves to this
    sentinel when its value is the literal string `root`.
  - **Block anchor** — `<!-- buildin:block:<block_id> -->`.
    Emitted on its own line immediately preceding the Markdown
    of each *supported* buildin block, at the same nesting
    indentation as the block's Markdown emission, with no
    surrounding blank line beyond what CommonMark would already
    require.
  - **Opaque anchor** — `<!-- buildin:opaque:<block_id> -->`, see
    FR-004.
  Block IDs are buildin's native block-id form (UUID); the
  literal string `root` is reserved and cannot collide with any
  block id.
- **FR-004**: Each *unsupported* buildin block in the page MUST be
  emitted as a *protected placeholder*: an opaque anchor comment of
  the form `<!-- buildin:opaque:<block_id> -->` followed by the
  unsupported-block placeholder paragraph spec 002 FR-003 already
  emits. The block id MUST also appear in the `unknown_block_ids`
  list returned by FR-001. The reconciler MUST refuse any patch
  whose effect would alter, duplicate, or remove a protected
  placeholder anchor, surfacing `patch.unsupported_block_touched`.
- **FR-005**: The revision token MUST be the CRC32 of the UTF-8
  bytes of the anchored-Markdown body returned by FR-001,
  rendered as a lowercase 8-character hexadecimal string (e.g.,
  `1a2b3c4d`). The token MUST change whenever any block in the
  page changes in a way that would change the fetched anchored
  Markdown, and MUST NOT change otherwise — including across
  buildin-side metadata changes that leave the Markdown invariant
  (a new comment attached to a block, a permission edit, a
  `last_edited_time` bump on an unrelated block). Callers MUST
  treat the token as opaque (compare by byte equality only) even
  though the algorithm is fixed; the algorithm is fixed for
  determinism and wire-size, not as a contract callers may
  parse. CRC32 is chosen for token-size economy (LLM callers
  carry the revision on every `update_page` call); the token is
  never trusted client-side — the server recomputes from current
  state and compares byte-for-byte.

#### Patch operations (core)

- **FR-006**: The shared core library MUST expose a single update
  operation that takes
  `{ page_id, revision, operations, dry_run?, allow_large_delete? }`
  and returns the reconciliation summary defined in FR-012. Both
  presentation surfaces MUST go through this operation; neither may
  apply operations or perform AST diffing locally, nor call the
  buildin write endpoints directly.
- **FR-007**: The operation MUST support, at minimum, the following
  operation types:
  - **`replace_block`** — fields: `anchor` (string, required;
    either a block id present in the snapshot — any block type —
    or the literal string `root` to target the page-root sentinel
    from FR-003), `markdown` (string, required, the new Markdown
    for that block and its tree descendants). Effect when the
    anchor is a block id: the anchored block's Markdown subtree
    is replaced by the new Markdown in the in-memory snapshot.
    Effect when the anchor is `root`: the page's child blocks
    are replaced by the parsed children of the new Markdown; if
    the new Markdown begins with a leading `# Heading`, that H1
    is consumed as the new page title per spec 006 FR-005's
    title-consumption rule (and is NOT added as a heading block).
    The root form remains subject to the large-delete guard
    (FR-013) and the placeholder-protection rule (FR-004). This
    is the unconditional single-block / single-root replacement;
    for heading-rooted section replacements, callers use
    `replace_section`.
  - **`replace_section`** — fields: `anchor` (string, required, a
    block id that points at a **heading** block in the snapshot),
    `markdown` (string, required). Effect: replace the heading
    plus all sibling blocks following it (in source order) up to,
    but not including, the next heading of equal-or-higher level
    in the **same parent**, or the end of the parent's children
    list if no such sibling heading exists. The scan operates on
    the heading's immediate-sibling list only — it does NOT
    descend into container blocks (columns, toggles, callouts,
    list items with children, etc.) and does NOT cross out of
    the heading's parent into ancestors. Container boundaries
    are hard limits on the operation's blast radius. The new
    Markdown is parsed and inserted in that range, in place of
    the deleted siblings; the new Markdown's first block need
    not be a heading. Anchored at a non-heading block, the
    operation fails with the new error class
    `patch.section_anchor_not_heading`.
  - **`search_replace`** — fields: `old_str` (string, required),
    `new_str` (string, required). Effect: replace the first
    occurrence of `old_str` with `new_str` in the in-memory
    snapshot. Fails with `patch.ambiguous_match` when `old_str`
    appears more than once; fails with `patch.no_match` when it
    appears zero times. Anchors inside `old_str` MUST be matched
    literally; callers may include anchors in `old_str` to
    disambiguate.
  - **`append_section`** — fields: `anchor` (string, optional;
    either a block id present in the snapshot or the literal
    string `root` for the page-root sentinel), `markdown`
    (string, required). Effect: append the new Markdown as
    children of the anchored block when `anchor` is given (or
    of the page itself when `anchor` is `root` or absent — both
    are equivalent for appends). The anchored block MUST be a
    container type per buildin's data model — page root, toggle
    heading, bulleted / numbered / to-do list item, quote,
    callout, column, toggle, table row, etc.; if it is a leaf
    type (paragraph, code, divider, image, plain non-toggle
    heading, and any other block whose buildin data model
    forbids children) the operation fails with the new error
    class `patch.anchor_not_container`. The reconciler MUST
    consult a per-block-type "can have children" predicate
    sourced from spec 002's compatibility matrix and decide
    before any buildin write call is issued. Callers that want
    to add content after a leaf block use `insert_after_block`
    instead.
  - **`insert_after_block`** — fields: `anchor` (string,
    required, a block id present in the snapshot — any block
    type, heading or otherwise; the literal string `root` is
    NOT accepted because the page-root sentinel has no sibling
    list), `markdown` (string, required). Effect: insert the
    new Markdown in document order immediately after the
    anchored block, at the same depth (i.e., as the next
    sibling in the anchored block's parent's children list).
    This is the "sibling insert" counterpart to `append_section`'s
    "child insert"; together the two ops cover every position an
    LLM might want to add new content. The previous operation
    name `insert_after_heading` is renamed to `insert_after_block`
    in this clarification session — the heading-only constraint
    has been dropped, and any block type may be the anchor.
- **FR-008**: Operations MUST be applied in the order they appear in
  `operations`. Each operation operates on the in-memory anchored
  Markdown produced by the previous operation (or the original
  snapshot for operation 0). If any operation fails, no buildin
  write call is issued and the failure names the failing
  operation's index.
- **FR-009**: A full-page replacement operation (i.e., a single
  operation whose effect is "discard all current children and use
  this Markdown body instead") MUST NOT be exposed as a dedicated
  operation type. The closest legal expression is a `replace_block`
  with `anchor: "root"` (the page-root sentinel from FR-003); the
  page-root is not a heading, so `replace_section` is not
  available against it (rejected with
  `patch.section_anchor_not_heading`). The root form remains
  subject to the large-delete guard (FR-013) and the
  placeholder-protection rule (FR-004), and edits only the page's
  child-block list plus (optionally) the title from a leading
  `# Heading`; icon, cover, and other page properties are out of
  scope of every operation in this feature. This is a Principle
  VI requirement.

#### Reconciliation (core)

- **FR-010**: After all operations are applied, the resulting
  Markdown MUST be parsed back into a buildin AST using the same
  conversion entry point spec 006 FR-002 already defines, with one
  addition: each block that carries a `buildin:block:<id>` anchor
  in the patched Markdown carries that id forward into the AST.
  Blocks emitted by the parser that do not carry an anchor are
  flagged as "new" and will receive buildin-assigned ids on commit.
- **FR-011**: The reconciler MUST diff the patched AST against the
  canonical block tree cached at fetch time and emit the *minimal*
  sequence of buildin write calls that reproduces the patched AST:
  - blocks whose anchor id is unchanged, whose parent in the tree
    is unchanged, whose sibling position is unchanged, and whose
    payload is byte-identical to the cached payload — *no write
    call issued* for that block;
  - blocks whose anchor id, parent, and position are unchanged
    but whose payload differs — one `updateBlock` call against
    that block id;
  - blocks present in the patched AST without an anchor —
    `appendBlockChildren` against the appropriate parent, at the
    correct position; the buildin-assigned id is recorded;
  - blocks present in the canonical tree whose anchor id is absent
    from the patched AST — one `deleteBlock` call against that id,
    subject to FR-013;
  - blocks whose anchor id is present in both the canonical tree
    and the patched AST but at a **different parent or a
    different sibling position** — REJECTED. Reorders of existing
    blocks are not supported by this feature: the reconciler MUST
    fail the call with `patch.reorder_not_supported`, name the
    offending anchor and its old / new positions in the error
    payload, and issue no buildin write call. Callers that want
    to relocate a block MUST express it as a pair of operations
    (delete via `search_replace` or via re-issuing
    `replace_section` without the anchor, then introduce a fresh
    block at the new location via `append_section` or
    `insert_after_block`); the new block receives a fresh
    buildin id and loses the comments / backlinks / timestamps
    attached to the old one. This mirrors the buildin UI's
    cut-and-paste model and is the only honest contract given
    that the Notion-family API exposes no native move primitive.
  The reconciler MUST preserve block IDs (and, transitively, the
  comments, backlinks, timestamps, and other collaboration
  metadata buildin attaches to them) for every block whose anchor
  AND parent AND sibling position are unchanged.
- **FR-012**: The operation MUST return a reconciliation summary
  `{ preserved_blocks, new_blocks, deleted_blocks, updated_blocks,
  ambiguous_matches, new_revision, post_edit_markdown? }` where the
  counts are integers reflecting what the reconciler did (or, under
  dry-run, would have done). `new_revision` is the revision token
  that would be returned by an immediate re-fetch. `post_edit_markdown`
  is included only when `dry_run: true`.

#### Safety mechanisms (core)

- **FR-013**: The reconciler MUST count blocks the patch would
  delete. If the count exceeds a configured threshold (default: a
  small absolute number such that single-section edits are never
  blocked, exact value a `/speckit-plan` decision), the call MUST
  fail with `patch.large_delete` before any write call is issued,
  unless the caller has set `allow_large_delete: true`. The error
  payload MUST carry the would-be-deleted count. The threshold
  applies per call, not cumulatively across calls.
- **FR-014**: When `dry_run: true`, the reconciler MUST perform
  parse, apply, diff, classify, and large-delete-check exactly as
  it would for a committing call, but MUST NOT issue any buildin
  write call. The response MUST include the same reconciliation
  summary the committing call would have returned, plus the
  rendered post-edit anchored Markdown (FR-012's
  `post_edit_markdown`). Validation-class failures MUST surface
  identically under dry-run and commit modes.
- **FR-015**: The revision check MUST run before any operation is
  applied. If the supplied revision does not equal the page's
  current revision, the call MUST fail with `patch.stale_revision`,
  the response MUST carry the current revision, and no buildin
  write call is issued — even under `dry_run: true` (a dry-run
  against a stale revision is itself an error worth surfacing).

#### CLI surface

- **FR-016**: The CLI MUST extend the existing `buildout get`
  command from spec 002 with an `--editing` flag (no new top-level
  CLI verb is added). Behavior:
  - Without `--editing`: identical to spec 002 (unanchored
    Markdown body to stdout; no revision; no `unknown_block_ids`).
    This invariant MUST hold byte-for-byte — `--editing` is the
    only switch between human and edit-mode reads.
  - With `--editing`: writes the anchored Markdown body to stdout
    and emits the revision and `unknown_block_ids` on stderr
    in a fixed, parseable header (one `revision: <hex>` line and
    zero-or-more `unknown_block_id: <id>` lines).
  - `--print <markdown|json>` (optional, default `markdown`,
    only meaningful together with `--editing`): when `json`,
    the entire `{ markdown, revision, unknown_block_ids }`
    triple is written to stdout as a single JSON object and the
    stderr header is suppressed. Without `--editing`,
    `--print json` is rejected as a validation error (the human-
    read mode has no structured output to serialise).
  Other read flags spec 002 already exposes (TTY-detection, etc.)
  apply unchanged in both modes.
- **FR-017**: The CLI MUST expose an `update` command with:
  - `--page <id>` (string, required).
  - `--revision <token>` (string, required).
  - `--ops <path|->` (positional or flagged, required): a
    filesystem path to a JSON document whose root is the
    `operations` array, or `-` to read that JSON from stdin.
  - `--dry-run` (flag, optional, default false): sets
    `dry_run: true`.
  - `--allow-large-delete` (flag, optional, default false): sets
    `allow_large_delete: true`. Adding this flag is the only way
    to commit a patch that the guard would otherwise block; the
    flag's presence is the explicit acknowledgement required by
    Principle VI.
  - `--print <summary|json>` (optional, default `summary`):
    `summary` writes the reconciliation counts and `new_revision`
    as a short human-readable block to stdout; `json` writes the
    full JSON reconciliation summary.
- **FR-018**: CLI exit codes MUST match the taxonomy already used
  by `get`, `search`, and `create`: validation, page-not-found
  (for `--page` resolving to a missing page), authentication /
  authorisation, transport, and unexpected. The patch-specific
  failures (`patch.stale_revision`, `patch.ambiguous_match`,
  `patch.no_match`, `patch.unknown_anchor`,
  `patch.section_anchor_not_heading`,
  `patch.anchor_not_container`,
  `patch.reorder_not_supported`,
  `patch.unsupported_block_touched`, `patch.large_delete`) MUST
  share a single new exit-code class, `patch-rejected`, distinct
  from validation. Partial-success (`patch.partial`, FR-024) uses
  the unexpected-error exit code, matching spec 006 FR-012.

#### MCP surface

- **FR-019**: The MCP server MUST advertise a `get_page_markdown`
  tool with input schema `{ page_id: string }` (required) and
  output `{ markdown: string, revision: string,
  unknown_block_ids: string[] }`. The tool description MUST
  identify it as the edit-mode fetch and MUST cross-reference the
  `update_page` tool.
- **FR-020**: The MCP server MUST advertise an `update_page` tool
  with input schema:
  - `page_id` (string, required)
  - `revision` (string, required)
  - `operations` (array, required, min length 1) — each element is
    a `replace_block` | `replace_section` | `search_replace` |
    `append_section` | `insert_after_block` payload per FR-007.
  - `dry_run` (boolean, optional, default false)
  - `allow_large_delete` (boolean, optional, default false)
  The tool description MUST mark `update_page` as a destructive
  operation per Principle VI, MUST name the failure modes
  enumerated in FR-018, and MUST point callers at `dry_run` as
  the cheap-preview mechanism.
- **FR-021**: A successful `update_page` invocation MUST return a
  single MCP content payload carrying the JSON reconciliation
  summary (FR-012). A successful `get_page_markdown` invocation
  MUST return the JSON triple from FR-019. The structural shape of
  the two surfaces' success payloads is intentionally symmetric so
  that an LLM client can chain fetch → patch → fetch round-trips
  without ad-hoc decoding.
- **FR-022**: A failure (validation, stale revision, ambiguous
  match, no match, unknown anchor, unsupported-block touch,
  large-delete-without-flag, parent / page not found,
  auth / transport, buildin-side error, partial application) MUST
  be surfaced as an MCP-protocol error mapped to the error-class
  taxonomy in FR-018. The error payload MUST carry the
  patch-specific class name (FR-018's `patch.*` names) so callers
  can recover programmatically. Partial-application errors MUST
  carry the partial revision.

#### Cross-cutting

- **FR-023**: All tests for this feature — anchor-emission unit
  tests, AST-diff unit tests, per-operation integration tests,
  revision-check tests, large-delete-guard tests, dry-run tests,
  end-to-end round-trips — MUST run against a mocked buildin
  client. No test may call a real buildin host (constitution
  Principle IV).
- **FR-024**: If a buildin write call fails mid-reconciliation
  after one or more sibling write calls have succeeded, the
  operation MUST surface `patch.partial` carrying:
  (a) the partial revision token reflecting the post-write state,
  (b) the index of the operation that was being committed when the
  failure occurred, (c) the buildin-side error class. v1 does NOT
  auto-rollback the partial state, matching spec 006 FR-012's
  posture for partial creation.
- **FR-025**: The cheap-LLM MCP integration test from features
  002 / 003 / 006 MUST be extended (or a sibling test added) to
  demonstrate an LLM chaining `get_page_markdown` →
  (compute a `replace_section`) → `update_page` →
  `get_page_markdown` and verifying both that the targeted block's
  Markdown changed *and* that every other block's anchor id is
  preserved. The test asserts Principle III round-trip *across*
  the edit, not just before and after.
- **FR-026**: This operation is destructive in the constitution
  Principle VI sense. The CLI command name (`update`), the MCP
  tool name (`update_page`), and the MCP tool description MUST
  carry that intent explicitly. The `--allow-large-delete` /
  `allow_large_delete` opt-in is the explicit-flag mechanism
  required by Principle VI for operations that destroy a large
  fraction of user content; the revision check and the per-
  operation `patch.*` failure classes are the targeting
  mechanisms required by Principle VI for the rest.
- **FR-027**: Buildin tokens continue to be supplied per spec 002
  FR-015. No new auth surface is introduced.
- **FR-028**: This feature's calls and error classes MUST flow
  through spec 007's existing observability instrumentation
  without parallel-inventing signal pipelines. Every
  `get_page_markdown` / `update_page` invocation MUST be wrapped
  by the same per-call span/log scope spec 007 already applies to
  other CLI commands and MCP tools, and every patch-rejected
  outcome (FR-018's `patch.*` classes plus `patch.partial`) MUST
  surface its class name through whatever error-classification
  dimension spec 007 has standardised (log attribute, metric
  label, or span attribute — exact name a `/speckit-plan`
  decision aligned with spec 007's conventions). This spec does
  NOT introduce new metric names, new span names, or a new
  exporter; it only obligates the feature's surfaces to be
  observable in the same shape as existing surfaces.

### Key Entities

- **Anchored Markdown**: a CommonMark document that interleaves
  spec 002's read-side Markdown emission with hidden HTML-comment
  anchors in three forms (FR-003): a single
  `<!-- buildin:root -->` sentinel at the very top of the body
  representing the page itself; one `<!-- buildin:block:<id> -->`
  per supported block; one `<!-- buildin:opaque:<id> -->` per
  unsupported block (paired with the existing placeholder
  paragraph). The Markdown stripped of anchors equals spec 002's
  read output.
- **Revision token**: an opaque string returned with the anchored
  Markdown and required on every update call. Used for optimistic
  concurrency. Treated as a cookie by callers.
- **Patch operation**: a single discriminated-union value of type
  `replace_block`, `replace_section`, `search_replace`,
  `append_section`, or `insert_after_block`, with the fields
  enumerated in FR-007.
- **Patch operation list**: the ordered sequence of operations
  applied in a single `update_page` call. Applied in order against
  the in-memory anchored Markdown; either all succeed or none
  commit (FR-008).
- **Reconciliation summary**: the response object
  `{ preserved_blocks, new_blocks, deleted_blocks, updated_blocks,
  ambiguous_matches, new_revision, post_edit_markdown? }` defined
  by FR-012. The single observable result of a successful update
  (or a successful dry-run).
- **Compatibility matrix (edit column)**: extended from spec 002's
  per-block-type record (and spec 006's write column) to include
  an *edit* column for each block, recording whether the
  reconciler preserves the block id under each operation type.
  Owned by the converter, exercised by tests.
- **Large-delete threshold**: a configured integer above which a
  patch's reconciled delete count requires explicit
  `allow_large_delete: true`. The exact value and configuration
  surface are a `/speckit-plan` decision; this spec fixes only the
  contract.
- **Patch-error class**: one of `patch.stale_revision`,
  `patch.ambiguous_match`, `patch.no_match`,
  `patch.unknown_anchor`, `patch.section_anchor_not_heading`,
  `patch.anchor_not_container`,
  `patch.reorder_not_supported`,
  `patch.unsupported_block_touched`,
  `patch.large_delete`, `patch.partial`. Surfaced via the
  `patch-rejected` CLI exit class and the MCP-protocol error
  payload.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every page fixture from spec 002's golden tests,
  the fetch operation produces anchored Markdown whose anchor-
  stripped form equals the spec 002 read output for that fixture
  byte-for-byte. Verified by a parameterised test that runs over
  every spec 002 fixture.
- **SC-002**: For every fixture page from SC-001, two successive
  fetches against the same mocked state produce byte-identical
  anchored Markdown and byte-identical revision tokens. Verified
  by a determinism test.
- **SC-002a**: The revision token is exactly 8 lowercase
  hexadecimal characters and equals the CRC32 of the UTF-8 bytes
  of the anchored-Markdown body returned by the same fetch.
  Buildin-side metadata changes that do not alter the anchored
  Markdown (e.g., a comment added to a block, a permission edit)
  do not change the token; any change that alters the anchored
  Markdown does. Verified by a paired test that mutates the
  mocked buildin's metadata-only state and asserts token
  stability, then mutates a block payload and asserts token
  change.
- **SC-003**: For each patch operation type in FR-007, there exists
  at least one fixture demonstrating the happy path and at least
  one fixture per declared failure class
  (`patch.ambiguous_match`, `patch.no_match`,
  `patch.unknown_anchor`, `patch.section_anchor_not_heading`,
  `patch.anchor_not_container`,
  `patch.reorder_not_supported`,
  `patch.unsupported_block_touched`). `replace_section`
  specifically MUST have a happy-path fixture (heading anchor
  spanning multiple sibling blocks up to the next equal-or-higher
  heading) and a `patch.section_anchor_not_heading` fixture
  (non-heading anchor). The `patch.reorder_not_supported` fixture
  MUST exercise the path where an anchor present in the snapshot
  is re-emitted by the agent at a different sibling position
  (e.g., a `search_replace` that swaps two anchor-bearing
  paragraphs), and MUST also assert that the well-known
  workaround — delete via `search_replace` paired with insert
  via `append_section` or `insert_after_block` — succeeds.
  Verified by parameterised tests recording every buildin write
  call.
- **SC-004**: For every fixture in SC-003's happy-path set, the
  number of buildin write calls recorded by the mocked client
  equals the reconciliation summary's
  `(new_blocks + updated_blocks + deleted_blocks)`, and the
  preserved-blocks count equals `(total_blocks_in_page -
  new_blocks - deleted_blocks - updated_blocks)`. The reconciler
  never issues a write call against a block whose anchor and
  payload are unchanged. Verified by a contract test asserting
  the recorded-calls / summary equivalence.
- **SC-005**: Every patch operation whose effect on the AST is a
  pure payload mutation on a single block produces exactly one
  buildin `updateBlock` call against that block's id and zero
  other write calls. No `deleteBlock` + `appendBlockChildren`
  cycle is permitted when an in-place mutation suffices.
  Verified by a targeted test per operation type.
- **SC-006**: Submitting any patch against a stale revision fails
  with `patch.stale_revision` and records zero buildin write
  calls, including under `dry_run: true`. The current revision
  appears in the error payload. Verified by a stale-revision
  fixture.
- **SC-007**: For the same inputs, the CLI's
  `get --editing --print json` and the MCP `get_page_markdown`
  tool return the same `{ markdown, revision, unknown_block_ids }`
  triple up to wire encoding; the CLI's `update --print json` and
  the MCP `update_page` tool return the same reconciliation
  summary up to wire encoding. Additionally, the CLI's
  `get` without `--editing` continues to be byte-identical to the
  spec 002 `buildin://{page_id}` resource body (spec 002 SC-003 is
  preserved). Verified by parity tests.
- **SC-008**: A patch whose reconciliation would delete more than
  the configured threshold of blocks is rejected with
  `patch.large_delete` and writes nothing; the same patch with
  `allow_large_delete: true` commits and writes exactly the
  previewed delete count. Verified by a large-delete fixture.
- **SC-009**: A dry-run of any operation in SC-003's happy-path
  set records zero buildin write calls and returns the same
  reconciliation summary the committing call would have produced.
  Subsequently re-issuing the same call without `dry_run`
  produces buildin write calls whose count and target ids match
  the dry-run's summary. Verified by a paired dry-run / commit
  test.
- **SC-010**: The cheap-LLM MCP integration test (FR-025)
  demonstrates an LLM chaining `get_page_markdown` → `update_page`
  → `get_page_markdown`, with the targeted block's Markdown
  changing across the edit and *every other* block's anchor id
  preserved bit-for-bit. Verified offline against the cheap-LLM
  harness.
- **SC-011**: The full test suite for this feature completes well
  under 60 seconds on a developer laptop with no outbound
  network access. The cumulative offline feature-test budget
  remains within the constitution's spirit (test-first, fast).
- **SC-012**: The update operation cannot create, modify, or
  delete any page other than the one named by `page_id`. Verified
  by a contract test asserting that, across every test in this
  feature's suite, the mocked buildin client receives write calls
  only against block IDs belonging to the targeted page (and
  `createBlock` calls only under that page's tree), and no
  `createPage` / `updatePage` / `createDatabase` /
  `updateDatabase` calls are issued by this feature's code path.

## Assumptions

- **The read converter is the source of truth for Markdown
  emission.** Anchored Markdown is the existing spec 002 emission
  with anchor lines woven in; the anchor format is purely
  additive and does not perturb the underlying CommonMark. The
  same per-block-type compatibility matrix extends with an *edit*
  column rather than spawning a parallel matrix.
- **The block id is the only identity that survives an edit.**
  Comments, backlinks, timestamps, and any other collaboration
  metadata buildin attaches to a block are preserved iff the
  block id is preserved. Preserving block ids across reconciliation
  is therefore the load-bearing correctness property of this
  feature.
- **Operations apply against the snapshot, not the live page.**
  The patched Markdown is derived from the anchored Markdown the
  caller fetched. The caller cannot fabricate anchors; only
  buildin assigns ids. The revision token guarantees the snapshot
  the patch was derived from is still current at commit time.
- **`replace_content` (full-page rewrite) is intentionally absent.**
  Such an operation would destroy every block id and violate
  Principle VI; callers who genuinely want a fresh page are
  expected to call spec 006's `create` against a parent.
- **Database edits are out of scope.** This feature edits
  *document pages*. Editing the rows of a database, the schema of
  a database, or a database view is a future feature and shares
  no FRs with this spec.
- **Cross-page link rewriting is out of scope.** A `search_replace`
  that updates a link's text or URL operates locally on the
  targeted page; no other page is consulted or modified.
- **Bulk multi-page updates are out of scope.** Each `update_page`
  call operates on exactly one page. Bulk surfaces, if they ever
  exist, will compose `update_page` over a list, not extend its
  contract.
- **Real-time collaborative merge / CRDT integration is out of
  scope.** The concurrency posture is optimistic via the revision
  token (FR-005, FR-015). If buildin grows native CRDT support
  later, this contract can be subsumed by a CRDT-aware variant
  without breaking the FR-007 operation shapes.
- **Partial-failure rollback is out of scope.** If a buildin
  write fails mid-reconciliation, `patch.partial` (FR-024) is
  surfaced and the caller decides. An opt-in
  `--rollback-on-error` is a future, additive change matching
  spec 006's posture.
- **The cheap-LLM MCP integration harness, the mock-buildin
  mechanism, the exit-code taxonomy, the TTY-detection rules, and
  the secrets handling are reused unchanged from features 002 /
  003 / 006.** This feature adds one new exit-code class
  (`patch-rejected`) and one new family of error names
  (`patch.*`) — nothing else cross-cutting.
- **MCP tool names, the `--editing` flag name on `get`, the
  large-delete threshold value, the exact wire form of `--print
  summary`, the exact attribute names through which spec 007's
  observability surfaces the new `patch.*` error classes, and the
  precise text of validation-error messages are `/speckit-plan`
  decisions.** This spec fixes only the contracts callers depend
  on. (The revision-derivation algorithm and the
  `replace_block` / `replace_section` split, originally deferred,
  are now pinned in Clarifications above.)
