# Phase 0 Research: Database Views (Read-Only)

Resolves the unknowns flagged in `plan.md` § Phase 0.

## R1 — Pagination ownership

**Decision**: The new core renderer (`DatabaseViewRenderer`) owns the
pagination loop. It calls `IBuildinClient.QueryDatabaseAsync` in a
loop, following `NextCursor` while `HasMore == true`, and accumulates
all rows into a single in-memory list before invoking any style
strategy.

**Rationale**:

- The existing `IBuildinClient.QueryDatabaseAsync` returns a single
  `QueryDatabaseResult` with `Results`, `HasMore`, and `NextCursor`
  (file `src/Buildout.Core/Buildin/Models/QueryDatabaseResult.cs`).
  The interface intentionally does not paginate; centralising that
  loop here matches the precedent set by `PageMarkdownRenderer` (which
  paginates `GetBlockChildrenAsync` for nested blocks) rather than
  pushing the loop into the client.
- Spec FR-002 / SC-004 require *every* row in the rendered output, so
  partial materialisation is never acceptable. Loading rows up-front
  before producing output also lets each style strategy compute
  metadata (group counts, max date) that requires the full set.
- Surfacing transport errors at the renderer level lets the adapters
  reuse the existing exit-code / MCP-error mapping.

**Alternatives considered**:

- **Push pagination into `IBuildinClient`** — rejected. Changes the
  client surface for a single caller, blurring Constitution V's
  abstraction line and forcing a parallel decision in any future
  client implementation.
- **Stream rows to the style strategies one page at a time** —
  rejected. Several styles (board groups, calendar dates) need the
  full row set to decide layout; streaming would force a two-pass
  design.

## R2 — Plain-output rendering primitive

**Decision**: Plain output is built with `StringBuilder` directly. No
Spectre.Console rendering helpers are used to produce the byte-
identical body. Spectre is used only by the CLI when
`TerminalCapabilities.IsStyledStdout` is true, and only to layer
styling on top of an already-rendered plain string (bold for the
metadata header, dim for divider lines).

**Rationale**:

- Spec FR-012 / SC-003 requires byte-identity between CLI plain mode
  and MCP body. Spectre rendering helpers (`Table`, `Panel`, `Rule`,
  `Markup`) embed implementation-defined characters and may emit
  styling sequences even when ANSI is disabled, which would couple
  the byte stream to the Spectre version.
- `GetCommand` already follows this split — it produces the markdown
  string in core, then wraps it in `MarkdownTerminalRenderer` only
  for TTY (`src/Buildout.Cli/Commands/GetCommand.cs:40–47`). This
  feature mirrors that pattern exactly.

**Alternatives considered**:

- **Use `Spectre.Console.Table` and dump its plain rendering for the
  MCP body** — rejected. Would require asserting on Spectre's
  internal column-width algorithm in tests; the algorithm is not
  contractually stable and changes would break byte-identity tests.
- **Bring in a markdown-table library** — rejected. Adds a dependency
  for ~30 lines of pipe-formatting; also wouldn't help with the four
  non-table styles.

## R3 — Command name shape

**Decision**: Register the command as a two-word Spectre.Console.Cli
command: `db view <database_id>`. In `Program.cs`:

```csharp
config.AddBranch<DbSettings>("db", db =>
{
    db.AddCommand<DbViewCommand>("view");
});
```

(Branch settings carry no options of their own; `DbSettings` exists
only to satisfy `AddBranch<T>`.)

**Rationale**:

- Existing commands are single-word verbs (`get`, `search`) acting on
  the implicit "page" noun. Adding a single-word `view` would be
  ambiguous (view of what?); a flat `db-view` reads as one word and
  hides the namespacing.
- A `db` branch leaves room for future commands without renaming —
  `db schema`, `db query`, etc. — which is desirable since the
  spec's "view styles" map to a small slice of database operations.
- `Spectre.Console.Cli`'s `AddBranch<T>` is the documented mechanism
  for nested commands.

**Alternatives considered**:

- **Flat `db-view`** — rejected. Hides hierarchy; makes future
  `db-schema` / `db-query` look like unrelated peers.
- **`view <database_id>`** — rejected. Risks colliding with future
  page-view, block-view operations.

## R4 — Per-property-type formatting

**Decision**: A single `IPropertyValueFormatter` dispatches on the
`PropertyValue` subclass and returns a single-line string. Mapping:

| PropertyValue subclass        | Inline rendering                                        |
|-------------------------------|---------------------------------------------------------|
| `TitlePropertyValue`          | concatenated rich-text plain (no formatting marks)      |
| `RichTextPropertyValue`       | concatenated rich-text plain                            |
| `NumberPropertyValue`         | `value.ToString(CultureInfo.InvariantCulture)`; `—` if null |
| `SelectPropertyValue`         | option name; `—` if null                                |
| `MultiSelectPropertyValue`    | comma-joined names; `—` if empty                        |
| `DatePropertyValue`           | `start` (and `start → end` if end set), ISO-8601 date   |
| `CheckboxPropertyValue`       | `[x]` / `[ ]`                                           |
| `UrlPropertyValue`            | the URL; `—` if null                                    |
| `PeoplePropertyValue`         | comma-joined names, truncated at the cell budget        |
| `FilesPropertyValue`          | `[N files]` (count only)                                |
| `RelationPropertyValue`       | `[N related]` (count only)                              |
| `RollupPropertyValue`         | inner scalar via the formatter recursively if present, else `[rollup]` |
| `FormulaPropertyValue`        | inner scalar via the formatter recursively if present, else `[formula]` |

`PropertyValue.cs` defines exactly these 13 sealed subclasses
(verified against the file). `CreatedTimePropertySchema` exists in
`PropertySchema.cs` but no corresponding `PropertyValue` subclass is
present in the model — created-time information surfaces on `Page`
itself rather than as a value-typed column, so the formatter has no
row for it. If a future buildin schema revision introduces a
`CreatedTimePropertyValue` (or similar timestamp value subclass),
this table is the single point that needs updating.

**Rationale**: Keeps a single point of change for property formatting,
reused by every style. Tests cover each subclass independently.

**Alternatives considered**:

- **Format inline within each style strategy** — rejected. Six × 14
  combinations duplicate the same logic.

## R5 — Cell budget, table-width threshold, board cap

**Decision**:

- **Per-cell budget**: 24 characters. Values longer than 24 are
  truncated with a trailing `…` (1 character), giving 23 visible
  characters of content + `…`.
- **Table-stacked threshold**: a table with more than 6 columns OR
  whose summed cell budget would exceed 80 columns (including
  separators) renders in stacked layout instead of a pipe-table.
- **Side-by-side board cap**: 3 non-empty groups. With more groups,
  render stacked.

**Rationale**: 80-column terminal width is the spec's hard target
(FR-004). 24 chars / 6 columns ≈ 144 — already wider than 80, so
the column count is the dominant gate; the per-cell budget keeps
narrow tables readable. The 3-group cap matches the design sketches
in `design-sketches.md`.

**Alternatives considered**:

- **Make the budget configurable via a flag** — deferred. Adds a
  knob without a use case; can be added later without breaking
  byte-identity for the default.

## R6 — Valid `--group-by` property types

**Decision**: Allowed property types for `--group-by`:

- `select` — group key is the option name; missing → `(none)`
- `multi-select` — group key is the comma-joined option names *as a
  single string* (i.e., a row with two selections appears in *one*
  group whose label is the combined name). Splitting one row into
  multiple groups is rejected: it would inflate the apparent row
  count and contradict the spec's "every row exactly once" rule.
- `checkbox` — two groups: `Checked` / `Unchecked`

Other property types passed to `--group-by` are a validation error
with a message listing the valid alternatives derived from the
schema.

Buildin exposes "status" properties as ordinary `select` properties
in the schema (no separate `StatusPropertySchema` exists in the
model), so status columns are valid `--group-by` targets via the
`select` rule above without special-casing.

**Rationale**: Prevents row inflation; matches buildin's UI semantics
where multi-select boards effectively pick one canonical group key.

## R7 — Date typing for calendar / timeline

**Decision**:

- **Calendar** accepts `--date-property` pointing to a property of
  type `date`, `created_time`, or `last_edited_time`. Group by the
  `start` date (or the timestamp itself for `created_time` /
  `last_edited_time`); rows whose date property is null or absent
  go under `(undated)`.
- **Timeline** accepts the same property types; if the property is a
  `date` with both `start` and `end`, render as `start → end (Nd)`;
  if only `start`, render as `start (1d)`. Single-timestamp
  property types (`created_time`) treated as `start` only.

**Rationale**: Matches buildin's date-property model (`start` +
optional `end`) and the design-sketches output. Does not introduce
a new "date range" entity.

**Alternatives considered**:

- **Restrict timeline to properties that have an `end`** — rejected.
  Excludes useful single-date timelines and would give a confusing
  validation error for the common case.

## R8 — Inline expansion of `child_database` blocks

**Decision**:

- **Style**: fixed at `Table`. The page-read pipeline does NOT accept
  `--style`, `--group-by`, or `--date-property` overrides for
  embedded expansion in this version.
- **Heading**: inline expansions are introduced by a `## <database
  title>` line (level 2) followed by a blank line and then the
  table. The standalone view's `# <title> — table view` header is
  *not* used inline — it would visually compete with the host
  page's title and shout "tool metadata" inside reading flow.
- **Recursion depth**: 1 level. Rows of an embedded database are
  pages, but those pages are not recursively read. This keeps the
  fan-out bounded (≤ N database fetches per page, where N is the
  count of `child_database` blocks; the embedded database's own
  rows are not separately read as pages).
- **Failure behaviour**: per-block isolation. A 404 / 401 / 403 /
  transport error / malformed-block payload on a single
  `child_database` block is replaced by a single line —
  `[child database: not accessible]`,
  `[child database: not found]`, `[child database: access denied]`,
  `[child database: transport error]`,
  `[child database: malformed]` — and the surrounding page
  continues to render. The page-read return code / MCP error code
  is unaffected by these per-block failures, by design.
- **Renderer reuse**: the `ChildDatabaseConverter` takes a
  constructor dependency on `IDatabaseViewRenderer` and calls a
  dedicated `RenderInlineAsync(databaseId, ct)` method on the
  renderer (see `contracts/core-renderer.md` and `data-model.md`).
  That method always renders the table style with no group-by /
  date-property parameters, and emits a `## <database title>`
  sub-section heading instead of the standalone view's
  `# <title> — table view` metadata header. Keeping inline as a
  separate method (rather than a flag on `RenderAsync` or a
  post-process step on its output) avoids string surgery on the
  rendered body and keeps the standalone path's golden tests
  decoupled from the inline path's.
- **Bypass option**: none in v1. There is no flag on `get` or on
  the page resource to suppress inline expansion. Adding one is a
  follow-up if motivated; in the meantime, callers who want the
  raw page contents without expansion can use the buildin client
  directly (i.e., it's a power-user concern, not a default-mode
  ergonomics concern).

**Rationale**: Picking a single fixed style avoids an explosion of
page-read flags, keeps the contract small, and gives a predictable
output for LLM consumers. Per-block error isolation matches user
expectation (one broken database link should not break the page) and
keeps page-read's existing error semantics intact for the common
case. Recursion-depth-of-one is the smallest commitment that
delivers the user's stated value without an open-ended fan-out
risk.

**Alternatives considered**:

- **Allow `--style` on `get`** — rejected. Adds a fan-out of options
  to a command that is, today, parameterless beyond a page id;
  embedded views are an ergonomics layer, not a configuration
  surface.
- **Recursive expansion (depth > 1)** — rejected for v1. The fan-out
  is unpredictable and a row in an embedded database is itself a
  page that may embed yet another database. A depth flag is the
  natural extension if we ever want to go further; the current
  shape doesn't preclude adding one.
- **Fail the page render on any `child_database` failure** —
  rejected. Makes one stale link in a long page block the whole
  read; user expectation is exactly the opposite.
