# MCP QA Test Skill

Use this skill to manually QA-test the Buildout MCP server by making real tool
calls and verifying responses. Works against a live workspace — the MCP server
must already be configured in Claude Code (server name: `buildin`).

## Prerequisites

- The `buildin` MCP server is registered in Claude Code settings and running.
- The bot token has at least read access to some pages. Write access needed for
  the create/update/delete/restore suite.
- All tools are callable as `mcp__buildin__<tool_name>`.

---

## How to Run This Skill

Work through each section below in order. For each test:

1. Call the MCP tool with the specified inputs.
2. Check the response against the **Expected** column.
3. Record **PASS** or **FAIL** with a short note.
4. Print a summary table at the end.

Stop the run immediately if authentication fails (all subsequent tests would
also fail).

---

## Phase 0 — Discovery

Before any other test, find real workspace content to use.

**Step 0.1 — List accessible pages**

Call `search` with an empty query to enumerate what the bot can see.

```
tool: search
query: ""
```

Expected: returns a tab-separated list with at least one `page` or `database`
entry. Save the first page_id as `$PAGE_ID` and the first database_id (if any)
as `$DB_ID`. If nothing is returned the workspace is empty — skip all read tests
and proceed only with the create/delete/restore suite.

**Step 0.2 — Note a non-existent ID**

Use `00000000-0000-0000-0000-000000000000` as `$MISSING_ID` throughout.

---

## Phase 1 — Search Tool (`search`)

| # | Input | Expected |
|---|-------|----------|
| 1.1 | `query=""` (repeat from 0.1) | Non-error response; `has_more` field or empty body acceptable |
| 1.2 | `query="<known keyword from 0.1 title>"` | At least one result matching that title |
| 1.3 | `query="xyzzy_nonexistent_12345"` | Empty body (no error) |
| 1.4 | `query=""` with `page_id=$PAGE_ID` | Results scoped to descendants of that page (may be empty) |
| 1.5 | `query=""` with `page_id=$MISSING_ID` | `ResourceNotFound` MCP error |
| 1.6 | `query=""` (no page_id) | Empty string body is acceptable; no exception |

---

## Phase 2 — Get Page Markdown (`get_page_markdown`)

| # | Input | Expected |
|---|-------|----------|
| 2.1 | `page_id=$PAGE_ID` | JSON with `Markdown` (string), `Revision` (non-empty string), `UnknownBlockIds` (array) |
| 2.2 | Inspect `Markdown` from 2.1 | Starts with `<!-- buildin:root -->` or a `# Title` heading; contains `<!-- buildin:block:` anchors |
| 2.3 | `page_id=$MISSING_ID` | `InvalidParams` MCP error; message mentions the ID |

Save `$REVISION` from test 2.1 for the update suite.

---

## Phase 3 — Tree Tool (`tree`)

| # | Input | Expected |
|---|-------|----------|
| 3.1 | `page_id=$PAGE_ID`, default format+depth | ASCII tree: root line + zero or more `├──`/`└──` lines with markdown links |
| 3.2 | `page_id=$PAGE_ID`, `format="json"` | Valid JSON with `name`, `uri`, `children` fields at root |
| 3.3 | `page_id=$PAGE_ID`, `format="json"`, `depth=1` | JSON children array has no grandchildren |
| 3.4 | `page_id=$PAGE_ID`, `format="ascii"`, `depth=7` | Succeeds (no validation error) |
| 3.5 | `page_id=$PAGE_ID`, `format="ascii"`, `depth=0` | `InvalidParams`; message mentions valid range |
| 3.6 | `page_id=$PAGE_ID`, `format="ascii"`, `depth=8` | `InvalidParams` |
| 3.7 | `page_id=$PAGE_ID`, `format="xml"` | `InvalidParams`; message contains `xml` |
| 3.8 | `page_id=$MISSING_ID`, `format="ascii"` | `InvalidParams`; message mentions the ID |

---

## Phase 4 — Database View (`database_view`)

Skip this phase if `$DB_ID` was not found in Phase 0.

| # | Input | Expected |
|---|-------|----------|
| 4.1 | `database_id=$DB_ID` (default table style) | Non-empty string with tabular content |
| 4.2 | `database_id=$DB_ID`, `style="list"` | Non-empty string in list format |
| 4.3 | `database_id=$DB_ID`, `style="board"` (no group_by) | Either error mentioning `group_by` is required, or renders with a default grouping |
| 4.4 | `database_id=$MISSING_ID` | `ResourceNotFound` MCP error |

---

## Phase 5 — Page Resource (MCP Resource)

| # | Action | Expected |
|---|--------|----------|
| 5.1 | Read resource `buildin://$PAGE_ID` | Text content with `text/markdown` MIME type; body matches the `Markdown` field from test 2.1 |
| 5.2 | Read resource `buildin://$MISSING_ID` | MCP resource error (not-found code) |

---

## Phase 6 — Create / Update / Delete / Restore (Write Suite)

This phase creates a scratch page and exercises all mutating tools against it.
Clean up at the end regardless of intermediate failures.

**Step 6.1 — Create page**

```
tool: create_page
parent_id: $PAGE_ID
markdown: "# MCP QA Scratch\n\nThis page was created by the MCP QA test suite and will be deleted."
title: "MCP QA Scratch"
```

Expected: result contains a `ResourceLinkBlock` with URI `buildin://<uuid>`.
Save that UUID as `$SCRATCH_ID`.

**Step 6.2 — Read the created page**

```
tool: get_page_markdown
page_id: $SCRATCH_ID
```

Expected: `Markdown` contains "MCP QA Scratch"; `Revision` is non-empty. Save
as `$SCRATCH_REV`.

**Step 6.3 — Update the page**

```
tool: update_page
page_id: $SCRATCH_ID
revision: $SCRATCH_REV
operations: [{"op":"search_replace","old_str":"MCP QA Scratch","new_str":"MCP QA Scratch (updated)"}]
```

Expected: JSON summary with `UpdatedBlocks >= 1` and a new `NewRevision` value.
Save as `$SCRATCH_REV2`.

**Step 6.4 — Dry-run update**

```
tool: update_page
page_id: $SCRATCH_ID
revision: $SCRATCH_REV2
operations: [{"op":"search_replace","old_str":"(updated)","new_str":"(dry run)"}]
dry_run: true
```

Expected: JSON summary returned; no permanent change (verify by re-reading and
checking the text still contains "(updated)" not "(dry run)").

**Step 6.5 — Stale revision rejection**

```
tool: update_page
page_id: $SCRATCH_ID
revision: $SCRATCH_REV   (the old one, not $SCRATCH_REV2)
operations: [{"op":"search_replace","old_str":"updated","new_str":"stale"}]
```

Expected: MCP error; message contains `stale_revision`.

**Step 6.6 — Delete page**

```
tool: delete_page
page_id: $SCRATCH_ID
```

Expected: `ResourceLinkBlock` with `buildin://$SCRATCH_ID`; success text.

**Step 6.7 — Verify deleted page is hidden**

```
tool: search
query: "MCP QA Scratch"
```

Expected: `$SCRATCH_ID` does not appear in results.

**Step 6.8 — Restore page**

```
tool: restore_page
page_id: $SCRATCH_ID
```

Expected: `ResourceLinkBlock` with `buildin://$SCRATCH_ID`; success text.

**Step 6.9 — Verify page is back**

```
tool: get_page_markdown
page_id: $SCRATCH_ID
```

Expected: succeeds; `Markdown` still contains "MCP QA Scratch (updated)".

**Step 6.10 — Cleanup: delete scratch page permanently (archive)**

```
tool: delete_page
page_id: $SCRATCH_ID
```

Expected: success.

---

## Phase 7 — Error Handling Edge Cases

| # | Tool | Input | Expected |
|---|------|-------|----------|
| 7.1 | `update_page` | `operations: "not json"` | MCP error (InvalidParams or InternalError) |
| 7.2 | `update_page` | `operations: [{"op":"search_replace","old_str":"XYZZY_NOTEXIST","new_str":"x"}]` against `$PAGE_ID` | MCP error; message contains `no_match` |
| 7.3 | `create_page` | `parent_id: $MISSING_ID`, `markdown: "# Test"` | MCP error (not success) |

---

## Result Summary Template

After completing all phases, print a table:

```
Phase | Test | Result | Notes
------|------|--------|------
0     | 0.1  | PASS   |
0     | 0.2  | N/A    |
1     | 1.1  | PASS   |
...
```

Include counts: `PASS: X  FAIL: Y  SKIP: Z`

Any FAIL entries should have a note with the actual vs expected behavior.
