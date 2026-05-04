# Feature Specification: Scaffold + Buildin API Client

**Feature Branch**: `001-scaffold-api-client`
**Created**: 2026-05-04
**Status**: Draft
**Input**: User description: "Scaffold - initial project layout and buildin api client generation using repo's openapi.json"

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the developers building and maintaining buildout. The
feature delivers no end-user behaviour on its own; it delivers the substrate that
every later end-user feature (read / write / edit) builds on.

### User Story 1 — Buildable solution skeleton (Priority: P1)

As a developer joining the project, I clone the repository, run a single build
command, and end up with the constitution-mandated project layout — five projects
that all compile and whose test suites all pass — without any manual configuration
beyond installing the .NET SDK.

**Why this priority**: Nothing else in the project can land until the solution
builds. This is the minimum viable foundation.

**Independent Test**: Clone a fresh checkout, run the build and test commands, and
observe a green build with green tests in every project.

**Acceptance Scenarios**:

1. **Given** a fresh clone of the repository on a machine with the supported .NET
   SDK installed, **When** the developer runs the project's documented build command
   from the repository root, **Then** the build succeeds and produces output for all
   five projects.
2. **Given** a successful build, **When** the developer runs the project's documented
   test command, **Then** every test project executes and reports zero failures.
3. **Given** the layout, **When** the developer inspects the solution, **Then** they
   see exactly five projects matching the constitution layout (`Buildout.Core`,
   `Buildout.Mcp`, `Buildout.Cli`, and the two test projects under `tests/`), with
   no extras.

---

### User Story 2 — Typed Buildin API client (Priority: P2)

As a developer writing a downstream feature that talks to buildin.ai, I call typed
methods on a client interface in `Buildout.Core` (one method per buildin operation
defined in `openapi.json`), passing strongly-typed request objects and receiving
strongly-typed response objects, without ever writing raw HTTP, JSON serialisation,
or string-based path interpolation.

**Why this priority**: Every downstream feature depends on calling buildin. Without a
typed client, every feature reinvents transport, error handling, and DTOs — and they
diverge. With a typed client, features focus on domain logic.

**Independent Test**: Spin up an in-process mock HTTP server that returns a canned
response for one buildin operation; instantiate the client pointing at it; call the
typed method; assert the response object's typed fields are populated as expected.

**Acceptance Scenarios**:

1. **Given** the generated client, **When** a developer wants to call any operation
   defined in `openapi.json`, **Then** an IDE-discoverable method exists on the
   client interface with typed parameters and return value matching the OpenAPI
   schema.
2. **Given** an operation that returns an error response per the OpenAPI document,
   **When** the underlying HTTP call yields that error, **Then** the client surfaces
   the buildin error in a typed shape that lets the caller distinguish it from a
   transport failure (network, timeout, etc.).
3. **Given** a future need to support an alternate buildin authentication mode (User
   API + OAuth), **When** that implementation is added, **Then** it slots in behind
   the same client interface without requiring changes in any presentation project
   or downstream feature code.

---

### User Story 3 — Reproducible client regeneration (Priority: P3)

As a maintainer, when buildin publishes an updated `openapi.json`, I overwrite the
file at the repository root, run a single regeneration command, and the diff is
mechanical — confined to the generated portion of `Buildout.Core` and reviewable as
a pure output of the generator. Hand-written code is untouched.

**Why this priority**: API change cadence is unknown but not zero. A painful
regeneration story will lead to drift between `openapi.json` and the typed client.

**Independent Test**: With an unchanged `openapi.json`, run the regeneration command
twice in a row; the working tree is clean after each run. Then make a trivial
addition to `openapi.json`, regenerate, and confirm the diff touches only the
generated subtree.

**Acceptance Scenarios**:

1. **Given** an unchanged `openapi.json`, **When** the regeneration command is run,
   **Then** version control shows zero changes.
2. **Given** a modified `openapi.json` with a new operation added, **When** the
   regeneration command is run, **Then** the new operation appears as a typed method
   on the client interface and all changed lines are inside the generated
   subdirectory.
3. **Given** the regeneration command, **When** a contributor runs it on macOS,
   Linux, or Windows, **Then** the output is byte-identical (modulo line endings).

---

### Edge Cases

- `openapi.json` contains an operation, schema, or polymorphic type that the chosen
  generator cannot represent idiomatically. Handling for these cases must be
  encoded outside the generated tree (e.g. partial classes, post-generation
  patches, or hand-written extensions in `Buildout.Core`) so regeneration never
  loses the customisation.
- The buildin OpenAPI document lists multiple servers with identical URLs (the
  current `openapi.json` has both "生产环境" and "测试环境" set to
  `https://api.buildin.ai`). The client MUST take the base URL from configuration,
  not from the OpenAPI `servers` block, so deployments can override it.
- The buildin Bot API returns errors in shapes that may not be fully described by
  `openapi.json`. The client MUST still surface unexpected error payloads in a
  diagnosable way (raw status + body available for logging), not silently swallow
  them.
- A future buildin operation requires a request or response schema not yet
  representable by the generator. The feature MUST NOT block downstream work in
  that case; the unsupported operation may be hand-written in `Buildout.Core`
  alongside the generated surface, with a documented note in the regeneration
  story.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST contain exactly the five projects mandated by the
  project constitution: `Buildout.Core`, `Buildout.Mcp`, `Buildout.Cli`,
  `Buildout.UnitTests`, `Buildout.IntegrationTests`. The first three live under
  `src/`; the test projects under `tests/`.
- **FR-002**: A clean checkout MUST build successfully via a single documented
  command, with no manual configuration steps beyond installing the supported .NET
  SDK.
- **FR-003**: Every project MUST contain at least one passing test (a smoke test is
  acceptable for projects that have no other behaviour yet) so test runners
  discover and execute against each project.
- **FR-004**: A typed buildin API client interface MUST exist in `Buildout.Core`
  and expose one method per operation declared in `openapi.json`. Method names and
  parameter shapes MUST be derivable from the OpenAPI document without hand
  authoring.
- **FR-005**: A Bot-API implementation of the client interface MUST be generated
  from `openapi.json`. The generated code MUST live in a clearly identifiable
  subdirectory or namespace within `Buildout.Core` and MUST be marked (file header,
  attribute, or directory README) as machine-generated.
- **FR-006**: Regeneration MUST be invokable as a single documented command and
  MUST be deterministic — two consecutive runs against an unchanged `openapi.json`
  produce zero diff.
- **FR-007**: When `openapi.json` changes and is regenerated, all changed lines in
  the resulting commit MUST be inside the generated subdirectory or in the
  `openapi.json` file itself; hand-written code MUST be untouched.
- **FR-008**: All tests that exercise the buildin client MUST run against an
  in-process or in-memory mock; no test in any suite may make a network call to
  `api.buildin.ai` or any real buildin host.
- **FR-009**: The buildin client interface MUST be designed such that an alternate
  implementation (notably User API + OAuth) can be added later without changing
  the interface signatures or any caller. The presentation projects
  (`Buildout.Mcp`, `Buildout.Cli`) MUST NOT depend on the Bot-API implementation
  type, only on the interface.
- **FR-010**: The Bot token used by the client MUST be supplied at construction
  time (via configuration, environment variable, or explicit parameter). No token,
  test fixture, or other secret may appear in committed source.
- **FR-011**: The client MUST distinguish three error categories when a call
  fails: (a) transport / connectivity errors, (b) buildin API errors documented in
  `openapi.json` (typed where possible), and (c) unexpected error payloads. Each
  category MUST be inspectable by the caller.
- **FR-012**: The presentation projects (`Buildout.Mcp`, `Buildout.Cli`) MUST
  exist as buildable shells. They are not required to expose any user-facing tool
  or command in this feature; their role here is to prove the layout compiles and
  that subsequent features have a place to add tools / commands.

### Key Entities

- **Buildin API operation**: an HTTP endpoint declared in `openapi.json` (e.g.
  `GET /v1/pages/{page_id}`), with a request shape and a response shape.
- **OpenAPI document**: `openapi.json` at the repository root — version-controlled,
  authoritative for what the Bot API currently supports.
- **Generated client**: typed source code mirroring each operation, living in a
  marked subdirectory of `Buildout.Core`.
- **Client interface**: the abstract surface implemented by the generated Bot-API
  client and any future alternate implementations (e.g. User API + OAuth).
- **Bot token**: bearer credential supplied at runtime from configuration; never
  in source.
- **Project layout**: the constitution-mandated arrangement of five .NET projects
  (`Buildout.Core`, `Buildout.Mcp`, `Buildout.Cli` under `src/`;
  `Buildout.UnitTests`, `Buildout.IntegrationTests` under `tests/`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new contributor with the supported .NET SDK already installed
  reaches a green build and green tests within 5 minutes of `git clone`, with no
  manual configuration.
- **SC-002**: Every operation declared in `openapi.json` (currently 10 paths
  covering pages, blocks, databases, search, and current-user) is callable as a
  typed method on the client interface — 100% coverage.
- **SC-003**: The full test suite (unit + integration) completes in under 60
  seconds on a developer laptop with no network access.
- **SC-004**: Regenerating the client from an unchanged `openapi.json` produces
  zero version-control changes.
- **SC-005**: When `openapi.json` changes, regeneration confines the resulting
  diff to the generated subdirectory plus `openapi.json` itself — zero touched
  lines elsewhere.
- **SC-006**: The full build + test pipeline runs to completion in an environment
  with no outbound access to `api.buildin.ai`, demonstrating no test depends on
  the live buildin service.
- **SC-007**: Adding a hypothetical second implementation of the client interface
  (e.g. a stub for the planned User API path) requires zero changes to the
  interface itself or to any presentation project.

## Assumptions

- The provided `openapi.json` (OpenAPI 3.1.0, "Buildin API" v1.0.0) is canonical
  and authoritative for what the Bot API currently supports. If buildin publishes
  a corrected document later, regeneration is the path forward (covered by US3).
- Bot tokens are delivered to the client through .NET configuration sources or
  environment variables, never embedded in source or test fixtures.
- The buildin base URL is configurable; tests and production read it from
  configuration rather than hard-coding `https://api.buildin.ai`.
- Mocked HTTP testing uses an in-process mechanism (e.g. a custom
  `HttpMessageHandler`); the specific mocking approach is a planning-phase
  decision, not a spec decision.
- Generated client code is committed to version control (not generated on every
  build). This keeps PR diffs reviewable and avoids requiring the generator
  toolchain in environments that only consume the library.
- CI configuration (GitHub Actions, build pipelines) is out of scope for this
  feature and will be addressed separately.
- Specific tooling decisions — the OpenAPI client generator, the test framework,
  the mock HTTP library, and the regeneration command's exact form — are deferred
  to `/speckit-plan`. The spec constrains outcomes, not tools.
- This feature does not deliver any user-facing MCP tool or CLI command; those
  arrive in subsequent features (e.g. "read page as markdown"). The presentation
  projects exist as buildable shells only.
