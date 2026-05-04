<!--
SYNC IMPACT REPORT
Version change: 1.0.0 → 1.0.1 (PATCH — fills deferred TODO without changing
semantics, per versioning policy)

Modified principles: N/A

Added sections: N/A

Removed sections: N/A

Modified content:
  - Technology & Implementation Standards: TODO(CLI_FRAMEWORK) resolved —
    Spectre.Console.Cli is now the mandated CLI framework for Buildout.Cli.

Templates requiring updates:
  - ✅ .specify/templates/plan-template.md  no change needed (no CLI-framework
       reference)
  - ✅ .specify/templates/spec-template.md  no change needed
  - ✅ .specify/templates/tasks-template.md no change needed (set to MANDATORY
       in v1.0.0; still aligned)

Follow-up TODOs: none.
-->

# Buildout Constitution

Buildout is a tools-family for buildin.ai (a Notion-like SaaS). It exposes
LLM-friendly read, write, and edit operations against buildin pages through an MCP
server (`stdio` + `http`) and a CLI tool, sharing a single core library.

## Core Principles

### I. Core/Presentation Separation (NON-NEGOTIABLE)

All buildin.ai API calls, block ↔ Markdown conversion, and domain logic MUST live in
the shared core library (`Buildout.Core`). Presentation projects (`Buildout.Mcp`,
`Buildout.Cli`) MUST NOT call buildin.ai directly, parse blocks, or duplicate
conversion logic — they translate transport-specific concerns (MCP tool descriptors,
CLI commands and flags) into core-library calls and nothing else.

**Rationale**: MCP and CLI are interchangeable surfaces over the same domain. Any
duplication lets one drift from the other and forces multi-surface fixes for every
bug. Future surfaces (admin dashboard, alternate transports) reuse the core
unchanged.

### II. LLM-Friendly Output Fidelity

Markdown rendered from buildin blocks MUST be deterministic, syntactically valid
CommonMark (or GFM where an extension is required), and free of buildin-internal
noise (opaque IDs, internal metadata) unless the caller explicitly requests it.
Conversion MUST preserve semantic structure — headings, lists, code blocks, links,
mentions, embeds — such that an LLM consuming the output reaches the same
understanding it would from a hand-authored document. Lossy conversions MUST be
enumerated in a per-block-type compatibility matrix and exercised by tests.

**Rationale**: The product's reason for existing is LLM ergonomics. "Mostly works"
Markdown is a defect, not a quirk.

### III. Bidirectional Round-Trip Testing

Every supported buildin block type MUST have round-trip tests in
`tests/Buildout.UnitTests`: blocks → Markdown → blocks, and the symmetric
Markdown → blocks → Markdown for input formats accepted by the writing tool.
Round-trips MUST either (a) produce structurally equivalent blocks/Markdown, or (b)
document the loss explicitly in the compatibility matrix referenced by Principle II.
New block-type support MUST NOT merge without such a test.

**Rationale**: Edit-in-place workflows depend on faithful round-tripping. Silent
lossiness corrupts user pages without observable failure.

### IV. Test-First Discipline (NON-NEGOTIABLE)

Implementation MUST follow Red → Green → Refactor: the failing test exists before
the code that satisfies it. Every behavioral change ships with:

- Unit tests in `tests/Buildout.UnitTests` for converters and pure logic.
- Integration tests in `tests/Buildout.IntegrationTests` for any change that crosses
  an external boundary — the buildin client, the MCP transport, the CLI dispatcher.

Integration tests MUST run against a mocked buildin.ai. No test may depend on real
buildin.ai availability, real API tokens, or live network access to buildin.ai.
Tests MUST NOT be skipped, disabled, or deleted to make a build pass.

**Rationale**: Conversion code is the highest-risk surface. LLM-driven MCP
integration tests catch protocol regressions that humans will not notice during
manual review.

### V. Buildin API Abstraction

All buildin.ai HTTP calls MUST go through a single typed client interface in
`Buildout.Core`. The current Bot API implementation MUST be one implementation of
that interface; switching to (or running concurrently with) the User API + OAuth
MUST be possible by adding a new implementation without source changes outside
`Buildout.Core`. No presentation project, converter, or tool implementation may take
a direct dependency on a specific authentication mode, endpoint URL, or API-surface
variant.

**Rationale**: The User API migration is anticipated. Coupling presentation or
conversion code to Bot-specific calls would force a rewrite later.

### VI. Non-Destructive Editing

Block-edit operations MUST target specific block IDs and MUST NOT rewrite, reorder,
or remove unrelated blocks. Any operation that destroys or replaces existing user
content MUST surface its destructive nature explicitly — in the MCP tool's
`description` field and in the CLI command name or a required flag. Bulk page
rewrites MUST be a separate, distinctly named operation from edits.

**Rationale**: LLMs invoking tools are non-deterministic. An "edit" tool that
silently rewrites a page can destroy user work; the tool contract must make
destructive operations impossible to invoke by accident.

## Technology & Implementation Standards

**Target framework**: .NET 10. Projects use SDK-style `.csproj`. Nullable reference
types and warnings-as-errors MUST be enabled solution-wide.

**MCP**: Implemented via the official `ModelContextProtocol` SDK. Both `stdio` and
`http` transports MUST be supported by `Buildout.Mcp` and selectable at process
startup.

**CLI framework**: `Spectre.Console.Cli`. `Buildout.Cli` MUST use this framework
for command definition, parsing, and dispatch, and SHOULD use the wider
`Spectre.Console` library for rendering structured output (tables, trees,
markdown, progress) and interactive prompts. Replacing the framework requires a
constitution amendment.

**Solution layout** (load-bearing — changes require a constitution amendment):

```text
src/
  Buildout.Core/        # Shared domain: buildin client, converters, models
  Buildout.Mcp/         # MCP presentation layer (stdio + http)
  Buildout.Cli/         # CLI presentation layer (+ skills)
tests/
  Buildout.UnitTests/         # Converter and pure-logic tests
  Buildout.IntegrationTests/  # Mocked-buildin + cheap-LLM MCP tests
```

**Authentication**: Currently Bot API tokens. User API + OAuth MUST be addable
behind the buildin client interface (see Principle V) without source changes outside
`Buildout.Core`.

**Secrets**: Buildin tokens, OAuth secrets, and LLM keys MUST be read from
environment variables or user-scoped configuration. Tests MUST use fixtures or
mocks. No secrets in source, in test fixtures, or in committed configuration.

**Out of scope** (current version): admin dashboard, managed or enterprise
deployment, multi-tenant hosting. Adding any of these requires a constitution
amendment (MINOR or MAJOR depending on architectural impact).

## Development Workflow & Quality Gates

**Spec-Kit driven**: Features flow `/speckit-specify` → `/speckit-plan` →
`/speckit-tasks` → `/speckit-implement`. Implementation MUST NOT begin before the
plan passes the Constitution Check defined in the plan template.

**Branching**: All work happens on feature branches. `main` MUST NOT receive direct
commits except for the initial bootstrap.

**Merge gates**: A change MUST NOT merge to `main` if any of the following fail:

- `Buildout.UnitTests` suite green.
- `Buildout.IntegrationTests` suite green.
- All round-trip tests for affected block types green (Principle III).
- Core/presentation separation respected — presentation projects compile against
  the public surface of `Buildout.Core` only (Principle I).

**Reviews**: Every PR review MUST include an explicit constitution-compliance
check. Violations MUST be either fixed before merge or documented in the plan's
"Complexity Tracking" table with justification.

**MCP tool changes**: Any addition or signature change to an MCP tool MUST include
both unit tests and an integration test that exercises the tool through a cheap
testing LLM, validating the MCP-level contract (description, schema, error shape) —
not only the underlying core-library behavior.

## Governance

This constitution supersedes ad-hoc practices and informal conventions. Where this
document conflicts with other in-repo guidance, this document wins, except where
the user's explicit instructions (CLAUDE.md, AGENTS.md, or direct requests)
override.

**Amendments** require, in a single PR:

1. An update to `.specify/memory/constitution.md` containing the change.
2. A version bump per the policy below.
3. A Sync Impact Report at the top of the file describing affected templates and
   any follow-ups.
4. Propagation of the amendment to dependent templates in `.specify/templates/`.

**Versioning policy**:

- **MAJOR**: Removing a principle, redefining a principle such that existing code
  violates it, or restructuring governance.
- **MINOR**: Adding a principle, adding a load-bearing section, or materially
  expanding existing guidance.
- **PATCH**: Clarifications, wording, typo fixes, or filling in a deferred TODO
  (e.g. picking the CLI framework) without changing semantics.

**Compliance review**: PR reviewers MUST verify constitution compliance as part of
review. Plans MUST run the Constitution Check gate before Phase 0 research and
re-check after Phase 1 design.

**Version**: 1.0.1 | **Ratified**: 2026-05-04 | **Last Amended**: 2026-05-04
