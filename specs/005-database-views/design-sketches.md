# Design Sketches: Database View Renderings

Companion to `spec.md`. Concrete renderings the spec's view styles
should produce. Examples use fake data. These sketches are
**illustrative**, not normative — actual cell widths, divider chars,
and metadata-line wording are decided in `/speckit-plan`.

> **Important constraint.** The buildin OpenAPI contract (this repo's
> `openapi.json`) defines no server-side "view" object. There is no
> stored view configuration, no view list endpoint, no view type
> field on `Database`. Each rendering below is produced **client-side**
> from the database schema (`PropertySchema`) and the rows returned by
> `POST /v1/databases/{id}/query`.

## Common Header

Every rendered view starts with one metadata line so a downstream
reader knows what they are looking at:

```
# <database title> — <style> view
> grouped by: <property>   sorted: <property> asc   filtered: <summary>
```

The second line is omitted when no parameters apply.

---

## 1. Table

Default style. Markdown pipe-table. Title property first.

```
# Tasks — table view

| Title          | Status      | Due        | Owner |
|----------------|-------------|------------|-------|
| Design API     | Open        | 2026-05-15 | Alice |
| Fix bug #2     | In Review   | 2026-05-12 | Bob   |
| Deploy v2      | Done        | 2026-05-10 | Carol |
| Refactor sea…  | Blocked     | —          | Alice |
```

**Fallback (too wide).** When the schema would produce a table
exceeding the soft column-width budget, switch to a stacked layout:

```
# Tasks — table view (stacked, 6 columns)

─ Design API
    Status: Open    Due: 2026-05-15    Owner: Alice
    Priority: High  Effort: 8 pts      Tags: backend, api
─ Fix bug #2
    Status: In Review    Due: 2026-05-12    Owner: Bob
    Priority: Medium     Effort: 2 pts      Tags: bugfix
```

---

## 2. Board (Kanban)

Group rows by a select/multi-select/status property given via
`--group-by`. Up to three non-empty groups MAY be rendered
side-by-side; otherwise stacked sections.

**Side-by-side (≤ 3 groups):**

```
# Feature Pipeline — board view (grouped by Status)

┌─ To Do (3) ─────┐  ┌─ In Progress (2) ┐  ┌─ Done (2) ──────┐
│ • Setup DB      │  │ • Build API       │  │ • Deployed v1   │
│ • Design UI     │  │ • Review PR       │  │ • Closed #42    │
│ • Tests         │  │                   │  │                 │
└─────────────────┘  └───────────────────┘  └─────────────────┘
```

**Stacked (> 3 groups):**

```
# Feature Pipeline — board view (grouped by Status)

## To Do (3)
  • Setup DB
  • Design UI
  • Tests

## In Progress (2)
  • Build API
  • Review PR

## In Review (1)
  • Refactor search

## Blocked (1)
  • Audit dependencies

## Done (2)
  • Deployed v1
  • Closed #42
```

Rows whose group-by value is missing land under `(none)`.

---

## 3. Gallery

One card block per row. Cover image is a textual placeholder. Up to
three secondary properties under the title.

```
# Projects — gallery view

╭─ Buildout CLI ──────────────────────╮
│ [cover: image]                      │
│ Status:  In Development             │
│ Owner:   Alice                      │
│ Due:     2026-06-01                 │
╰─────────────────────────────────────╯

╭─ API Redesign ──────────────────────╮
│ [cover: none]                       │
│ Status:  Planning                   │
│ Owner:   Bob                        │
│ Due:     2026-07-15                 │
╰─────────────────────────────────────╯
```

---

## 4. List

One bulleted line per row. Title plus a short parenthesized property
summary.

```
# Issues — list view

• Design API           (Priority: High,   Status: Open)
• Fix bug #2           (Priority: Medium, Status: In Review)
• Deploy v2            (Priority: Low,    Status: Done)
• Refactor search      (Priority: High,   Status: Blocked)
```

This is also the documented fallback for any database with no visible
non-title properties under any style.

---

## 5. Calendar

Notion's month grid is not legible in a terminal. Render as date
headings with rows underneath, ascending. The date property comes
from `--date-property`.

```
# Milestones — calendar view (by Due)

## 2026-05-10 (Sun)
  • Deploy v2
  • Close sprint

## 2026-05-12 (Tue)
  • Fix bug #2
  • Security review

## 2026-05-15 (Fri)
  • Design API review

## (undated)
  • Refactor search
```

Rows missing the date property are placed under `(undated)` at the
end, never dropped.

---

## 6. Timeline

Same constraint as calendar. Render as ordered list grouped by start
date, showing the date range and duration.

```
# Q2 Roadmap — timeline view (by Phase)

## 2026-05
  2026-05-01 → 2026-05-08  (7d)   Design Phase
    └ Research competitors
    └ Wireframes
  2026-05-10 → 2026-05-24  (14d)  Build Phase
    └ API development
    └ Frontend work

## 2026-06
  2026-06-01 → 2026-06-14  (13d)  Testing & Launch
    └ QA
    └ Beta release
```

If the date property holds a single date, render the duration as
`(1d)` and use the same heading style.

---

## Common Rules

These apply to every style and are testable independently of the
specific style chosen.

- **Cell truncation.** Inline property values exceeding the per-cell
  budget are truncated with `…`. Untruncated raw payload is never
  echoed.
- **Width budget.** A documented soft column-width budget governs
  whether the table style switches to stacked layout. The exact
  number is decided in `plan.md`; the spec only requires the fallback
  to exist and to be deterministic.
- **Empty values.** Render as `—` (em dash). Multi-select with no
  selections renders as `—`, not `[]`.
- **Property types that cannot render inline.**
  - `files` → `[N files]`
  - `relation` → `[N related]`
  - `rollup` → resolved scalar if possible, else `[rollup]`
  - `formula` → resolved scalar if possible, else `[formula]`
  - `people` → comma-separated names truncated to the cell budget
- **Hidden by default.** `created_by` / `last_edited_by` / internal
  identifiers are hidden unless the schema has no other non-title
  property.
- **No color is required for correctness.** TTY styling MAY add bold
  for titles and dim for metadata; non-TTY output is the source of
  truth for tests.
- **Order.** Within any style, rows preserve the order returned by the
  database query endpoint — this feature does not introduce a
  client-side sort. Calendar/timeline group-by-date layouts are the
  only exception, and even there the in-group order follows query
  order.

## Mapping to OpenAPI Reality

| Sketch element        | Where it comes from                                  |
|-----------------------|-------------------------------------------------------|
| Database title        | `Database.title` (existing schema)                   |
| Column names          | Keys of `Database.properties` (existing schema)      |
| Column types          | `PropertySchema.type` per column                     |
| Row title             | The title-typed property of each `Page` in the query |
| Row property values   | `Page.properties[name]` typed as `PropertyValue`     |
| Pagination follow-through | `QueryDatabaseResponse.has_more` + `next_cursor` |
| Group-by source       | A user-named property in the schema (validated)      |
| Date source           | A user-named date-typed property (validated)         |

There is **no** OpenAPI element corresponding to "the view itself" —
every styling decision above is a client-side choice owned by this
feature.
