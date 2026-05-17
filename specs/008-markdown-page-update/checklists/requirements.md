# Specification Quality Checklist: Markdown Page Update via Patch Operations

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-05-16
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.
- Reviewer notes on the validation pass:
  - Content quality: the spec deliberately names existing surfaces (CLI verbs
    `get`, `search`, `create`; MCP tools `get_page_markdown`, `update_page`)
    and anchor wire form (`<!-- buildin:block:<id> -->`). These are *contract*
    references, not implementation details — the FRs that fix them
    (FR-003, FR-016, FR-017, FR-019, FR-020) are the public surface every
    caller depends on. They are acceptable in this spec for the same reason
    spec 006 names `--parent` / `--print` / `create_page`.
  - Requirement completeness: every FR has at least one matching SC and at
    least one acceptance scenario in a US (or an Edge Case row). The
    patch-error class names are enumerated exhaustively in FR-018 and
    cross-referenced from the failure paths in US2/US3/US5.
  - Feature readiness: the five user stories form a viable MVP at P1
    (US1+US2+US3 — fetch, patch, optimistic concurrency); P2 stories
    (US4 dry-run, US5 large-delete guard) are independently testable safety
    backstops.

## Deferred to `/speckit-plan`

The spec leaves the following decisions to the plan phase, in line with
spec 006's posture:

- Final MCP tool names and the exact `--editing` flag name on `get`.
- Exact value of the large-delete threshold and how it is configured.
- Wire form of `--print summary` (the human-readable variant).
- Exact attribute names through which spec 007's observability
  surfaces the new `patch.*` error classes (metric label, log
  attribute, span attribute — naming aligned with spec 007's
  conventions).
- Precise text of validation-error messages and MCP error descriptions.

## Resolved during `/speckit-clarify` (Session 2026-05-16)

**Round 1**

- `replace_section` semantics → split into `replace_block` (any
  anchor, single-block subtree) and `replace_section` (heading
  anchor only, heading-rooted section). New error class
  `patch.section_anchor_not_heading`.
- Revision-derivation strategy → CRC32 of UTF-8 anchored-Markdown
  body, rendered as 8-char lowercase hex.
- Observability integration → light-touch cross-reference: feature
  flows through spec 007's existing per-call instrumentation; new
  `patch.*` classes appear as an error-class dimension on spec
  007's existing signals; no new metric/span names invented here.
- Edit-mode read surface → MCP keeps the new `get_page_markdown`
  tool (structured triple, atomic snapshot); CLI extends existing
  `buildout get` with an `--editing` flag rather than adding a
  new `fetch` command. The spec 002 `buildin://{page_id}`
  resource and its byte-identical-CLI invariant are preserved.

**Round 2**

- Page-root reference → emit `<!-- buildin:root -->` as the very
  first line of the anchored-Markdown body; patch operations use
  the literal anchor value `root` to target it. `replace_block`
  with `anchor: "root"` replaces the page's children only; the
  new Markdown's leading H1 (if any) becomes the new title per
  spec 006 FR-005. Icon, cover, and other page properties are
  not editable in this feature.
- Reordered blocks → reject. Any anchor present in both pre- and
  post-patch ASTs at a different tree position fails with the new
  error class `patch.reorder_not_supported`. Mirrors the buildin
  UI's cut-and-paste mental model and is the only honest contract
  given the Notion-family API exposes no native move primitive.
- `replace_section` boundary scope → same-parent only. The
  forward scan walks the heading's immediate-sibling list only;
  container boundaries (columns, toggles, callouts, list items
  with children) are hard limits. Blast radius is bounded by the
  parent's children count.
- `append_section` on non-container + sibling-insert ergonomics →
  `append_section` against a leaf block (paragraph, code, divider,
  image, plain non-toggle heading, etc.) fails with the new error
  class `patch.anchor_not_container`. `insert_after_heading` is
  renamed and generalised to `insert_after_block`: any block
  anchor, inserts the new Markdown as a sibling at the same
  depth. The op count stays at five.
