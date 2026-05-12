# Feature Specification: CI/CD Pipeline

**Feature Branch**: `004-cicd-pipeline`
**Created**: 2026-05-07
**Status**: Draft
**Input**: User description: "CI/CD: build project, run tests (unit & integration in parallel), save MCP/CLI artifacts if tests passed. Integration tests use OpenRouter with nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free model. Setup mock server for buildin using WireMock with OpenAPI as source of truth."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Build & Test on Every Push (Priority: P1)

As a developer, when I push a commit or open a pull request, the solution
builds and all unit and integration tests run automatically. I can see
pass/fail results directly in the PR status checks.

**Why this priority**: Without a green build gate, broken code can merge. This
is the foundational gate everything else depends on.

**Independent Test**: Push a commit to a PR branch and observe the GitHub
Actions status check turn green (build + tests pass) or red (any failure).

**Acceptance Scenarios**:

1. **Given** a commit is pushed to any branch, **When** the CI workflow
   triggers, **Then** the solution builds successfully with `dotnet build`
   on the .NET 10 SDK with warnings-as-errors enabled.
2. **Given** the build succeeds, **When** unit tests run, **Then** all tests
   in `Buildout.UnitTests` pass.
3. **Given** the build succeeds, **When** integration tests run, **Then** all
   tests in `Buildout.IntegrationTests` pass against the WireMock-based
   buildin mock server (no real buildin.ai network calls).
4. **Given** both unit and integration test jobs complete, **When** any test
   fails, **Then** the overall workflow fails and the PR status check shows
   failure.

---

### User Story 2 - WireMock-Based Buildin Mock Server (Priority: P1)

As a developer, integration tests run against a WireMock.NET server that
faithfully mimics the buildin.ai API using manual stubs maintained to match
the repository's `openapi.json` as the source of truth. No real buildin.ai
calls occur during testing.

**Why this priority**: The constitution (Principle IV) mandates that
integration tests run against a mocked buildin.ai. WireMock provides
contract-accurate mocking aligned with the OpenAPI specification.

**Independent Test**: Run `dotnet test` locally and all integration tests
pass with no network calls to buildin.ai, using WireMock stubs maintained
in sync with `openapi.json`.

**Acceptance Scenarios**:

1. **Given** the integration test project, **When** tests initialize, **Then**
   a WireMock.NET server starts on a local port with manually defined stubs
   that match the endpoints and schemas in `openapi.json`.
2. **Given** a WireMock stub for the search-pages endpoint, **When** the test
   calls the buildin client's search method, **Then** WireMock returns the
   stubbed response matching the OpenAPI schema and the test passes.
3. **Given** a WireMock stub for the get-page and get-block-children
   endpoints, **When** the test calls the buildin client's page-fetching
   methods, **Then** WireMock returns stubbed responses matching the OpenAPI
   schemas and the tests pass.
4. **Given** the existing `MockHttpHandler` and NSubstitute `IBuildinClient`
   mocks in integration tests, **When** WireMock is introduced, **Then** those
   mocks are removed and all integration tests still pass.

---

### User Story 3 - LLM Integration Tests for MCP Tool Calling (Priority: P2)

As a developer, LLM-powered integration tests validate that an LLM can
discover MCP tools through the protocol, understand their schemas and
descriptions, call them with correct parameters, and produce grounded
answers from the results — using OpenRouter with a free-tier model
(`nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free`) via the Semantic
Kernel SDK.

**Why this priority**: MCP tool descriptions, parameter schemas, and output
formats are the contract between buildout and any LLM client. Validating that
an LLM can understand and correctly use these contracts through the MCP
protocol (not hand-authored wrappers) is essential for ensuring buildout's
MCP server is production-ready. This depends on P1 infrastructure (build +
mock server) being in place first.

**Independent Test**: Set `OPENROUTER_API_KEY` and run the LLM integration
tests. Each test connects an in-process MCP server to an LLM via Semantic
Kernel, asks a question requiring tool use, and asserts the LLM discovers
and calls the correct MCP tools with correct parameters and returns a
grounded answer.

**Acceptance Scenarios**:

1. **Given** `OPENROUTER_API_KEY` is set in environment, **When** the LLM
   integration test runs, **Then** Semantic Kernel sends requests to OpenRouter
   using model `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free` via its
   OpenAI-compatible endpoint.
2. **Given** an in-process MCP server exposing `search`, `database_view`
   tools and `buildin://{pageId}` resource template, **When** the test
   registers MCP tools as SK plugins using schemas discovered via
   `McpClient.ListToolsAsync()` (not hand-authored descriptions), **Then**
   the LLM receives tool descriptions and parameter schemas that exactly match
   the MCP server's `[McpServerTool]` metadata.
3. **Given** the LLM is asked "Which page describes Q3 revenue, and what was
   the total?", **When** the `search` tool and `buildin://` resource are
   available, **Then** the LLM calls `search` with a relevant query, reads
   the matching page via the resource, and returns an answer grounded in the
   page content (e.g., contains "4.2").
4. **Given** the LLM is asked a question about a database's contents,
   **When** the `database_view` tool is available, **Then** the LLM calls
   `database_view` with a correct `database_id` parameter and optionally a
   `style` parameter, and returns an answer grounded in the rendered output.
5. **Given** an MCP tool returns an error (e.g., search with empty query
   returns `InvalidParams`, or a non-existent resource returns
   `ResourceNotFound`), **When** the LLM receives the error, **Then** it
   recovers gracefully and informs the user about the error rather than
   producing a hallucinated answer.
6. **Given** `OPENROUTER_API_KEY` is not set, **When** the LLM integration
   test runs, **Then** the test is skipped (not failed).

---

### User Story 4 - Publish MCP and CLI Artifacts (Priority: P3)

As a developer, after all tests pass, the CI pipeline publishes
framework-dependent single-file artifacts for `Buildout.Mcp` and
`Buildout.Cli` so they can be downloaded and used without building locally.

**Why this priority**: Artifact publishing provides value but depends
entirely on P1 and P2 being green. It is a delivery convenience, not a gate.

**Independent Test**: Push a passing commit, download the published artifacts
from the GitHub Actions run, and execute `Buildout.Cli` or `Buildout.Mcp`
on a machine with the .NET 10 runtime installed.

**Acceptance Scenarios**:

1. **Given** all unit and integration tests pass, **When** the publish job
   runs, **Then** `dotnet publish` produces framework-dependent single-file
   outputs for `Buildout.Mcp` and `Buildout.Cli`.
2. **Given** the published outputs, **When** the workflow completes, **Then**
   both artifacts are uploaded via `actions/upload-artifact` and downloadable
   from the workflow run page.
3. **Given** any test failure, **When** the workflow evaluates the publish
   gate, **Then** the publish job does not run.

---

### Edge Cases

- What happens when the WireMock server fails to start or bind a port?
- What happens when OpenRouter returns a rate-limit or server error?
- What happens when `openapi.json` is updated but WireMock stubs are not
  updated to match — do tests catch the drift?
- What happens when a PR is opened from a fork — secrets
  (`OPENROUTER_API_KEY`) are unavailable in fork PRs; LLM tests must skip
  gracefully.
- What happens when `dotnet publish` succeeds but the artifact exceeds
  GitHub's upload size limits?
- What happens when the LLM calls an MCP tool with incorrect parameters —
  does the test framework detect this and fail the test?
- What happens when the MCP tool descriptions are ambiguous and the LLM
  calls the wrong tool — how does the test distinguish "wrong tool" from
  "correct tool, different phrasing"?
- What happens when the free-tier model is too small to reliably perform
  multi-tool orchestration — should the test have retry/timeout logic?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The CI workflow MUST trigger on push to `main` and on
  `pull_request` events.
- **FR-002**: The workflow MUST run `dotnet build` as the first stage and
  fail fast on compilation errors.
- **FR-003**: Unit tests and integration tests MUST run as parallel GitHub
  Actions jobs after a successful build.
- **FR-004**: Integration tests MUST use WireMock.NET as the buildin.ai mock
  server, with manually defined stubs maintained to match the endpoints and
  response schemas in `openapi.json`.
- **FR-005**: The existing `MockHttpHandler` class and NSubstitute-based
  `IBuildinClient` mocks in `Buildout.IntegrationTests` MUST be replaced by
  WireMock-based fixtures.
- **FR-006**: LLM integration tests MUST use the Semantic Kernel SDK with
  `AddOpenAIChatCompletion` configured to call OpenRouter at
  `https://openrouter.ai/api/v1` using model
  `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free`.
- **FR-007**: MCP tools in LLM tests MUST be registered as Semantic Kernel
  plugins using the native `McpClientTool.AsKernelFunction()` extension
  method (available in `Microsoft.SemanticKernel.Core` 1.44.0+) with
  `FunctionChoiceBehavior.Auto()` to enable automatic tool calling. Tool
  names, descriptions, and parameter schemas are sourced from the MCP
  protocol via `McpClient.ListToolsAsync()` — not hand-authored in the
  test code. This validates that the MCP server's `[McpServerTool]`
  metadata is sufficient for an LLM to discover and correctly use the
  tools.
- **FR-008**: LLM integration tests MUST be skipped (not failed) when
  `OPENROUTER_API_KEY` is not set in the environment.
- **FR-009**: LLM integration tests MUST run on push to `main` and on
  branches with an open pull request. They MUST NOT run on push to branches
  without an open PR.
- **FR-010**: The Anthropic SDK dependency (`Anthropic.SDK` NuGet package) in
  `Buildout.IntegrationTests` MUST be removed and replaced with the Semantic
  Kernel OpenAI connector.
- **FR-015**: LLM integration tests MUST cover all three MCP surfaces: the
  `search` tool, the `database_view` tool, and the `buildin://{pageId}`
  resource template.
- **FR-016**: LLM integration tests MUST NOT contain tests that only validate
  LLM reading comprehension of pre-rendered Markdown without exercising MCP
  tool calling. Pure Markdown comprehension is already covered by unit tests
  for the rendering pipeline.
- **FR-017**: At least one LLM integration test MUST exercise multi-tool
  orchestration — the LLM chaining two or more MCP tool/resource calls to
  answer a single question.
- **FR-018**: At least one LLM integration test MUST exercise an MCP error
  path — the LLM receiving a tool error and producing a sensible response
  rather than a hallucinated answer.
- **FR-011**: When all tests pass, the workflow MUST publish
  framework-dependent single-file artifacts for `Buildout.Mcp` and
  `Buildout.Cli`.
- **FR-012**: The publish job MUST NOT execute if any test job fails.
- **FR-013**: Published artifacts MUST be uploaded via
  `actions/upload-artifact` and available for download from the workflow run.
- **FR-014**: No test MAY depend on real buildin.ai availability, real API
  tokens, or live network access to buildin.ai (per constitution Principle IV).

### Key Entities

- **CI Workflow**: GitHub Actions YAML defining build, test, and publish
  stages with job dependencies and artifact upload.
- **WireMock Fixture**: A test fixture that starts a WireMock.NET server,
  registers manual stubs matching the `openapi.json` endpoint schemas, and
  provides the server's base URL to integration tests via an `IBuildinClient`
  wired to the mock server.
- **WireMock Stubs**: Manually maintained request-response mappings per
  buildin.ai endpoint (search-pages, get-page, get-block-children, get-me),
  each verified against the corresponding OpenAPI schema. These are the
  contract tests that catch drift between stubs and `openapi.json`.
- **Semantic Kernel LLM Client**: Semantic Kernel configured with
  `AddOpenAIChatCompletion` using a custom `HttpClient` pointing to
  `https://openrouter.ai/api/v1`, enabling tool-call-driven integration
  tests against the free-tier OpenRouter model.
- **MCP-to-SK Plugin Bridge**: A test helper (`McpSkBridge`) that calls
  `McpClient.ListToolsAsync()` to discover available MCP tools, converts
  each `McpClientTool` to a Semantic Kernel `KernelFunction` using the
  built-in `AsKernelFunction()` extension, and wraps MCP resources (e.g.,
  `buildin://{pageId}`) as callable SK functions. The LLM sees exactly
  the tool metadata the MCP server exposes — no hand-authored wrappers.
- **MCP Resource-to-Function Wrapper**: Since Semantic Kernel has no native
  concept of MCP resources, a thin wrapper exposes the `buildin://{pageId}`
  resource template as an SK function (e.g., `read_buildin_page(pageId)`)
  with the description sourced from the MCP resource template metadata.
- **Published Artifacts**: Framework-dependent single-file executables for
  `Buildout.Mcp` and `Buildout.Cli`, produced by `dotnet publish`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every push to `main` or any PR branch produces a green or red
  status check within 10 minutes.
- **SC-002**: Unit and integration test jobs run in parallel, completing no
  slower than the longest individual job.
- **SC-003**: All integration tests pass with zero network calls to
  buildin.ai (verifiable by WireMock request journal).
- **SC-004**: LLM integration tests successfully discover MCP tools via
  `McpClient.ListToolsAsync()`, convert them to Semantic Kernel functions
  using the native `AsKernelFunction()` extension, and the LLM correctly
  calls `search`, `database_view`, and `buildin://` resources to return
  grounded answers on every CI run where the API key is available.
- **SC-005**: Published artifacts are downloadable from every successful
  workflow run and executable on any machine with the .NET 10 runtime.
- **SC-006**: Removing the `Anthropic.SDK` package from
  `Buildout.IntegrationTests.csproj` does not break any remaining test.
- **SC-007**: WireMock stubs produce responses matching the schemas defined
  in `openapi.json` for each stubbed endpoint, verified by contract tests.

## Assumptions

- GitHub Actions is the CI/CD platform (repo hosted on GitHub).
- `OPENROUTER_API_KEY` is stored as a GitHub Actions repository secret.
- WireMock.NET does not natively support OpenAPI 3.1 stub generation; stubs
  are maintained manually but verified against the spec via contract tests.
- The `openapi.json` in the repository root is the authoritative source of
  truth for buildin.ai API shapes; WireMock stubs must be updated when it
  changes.
- Framework-dependent single-file artifacts require the .NET 10 runtime on
  the consumer's machine.
- The .NET 10 SDK is available via GitHub Actions'
  `actions/setup-dotnet`.
- Fork PRs will not have access to secrets; LLM tests will skip in that
  scenario (acceptable per FR-008).
- The existing `MockHttpHandler` in `MockedHttpHarnessTests.cs` and the
  NSubstitute `IBuildinClient` mocks in `PageReadingLlmTests.cs` will be
  fully replaced, not supplemented.
- Semantic Kernel's `AddOpenAIChatCompletion` supports custom endpoints via
  a custom `HttpClient` with `BaseAddress` set to the OpenRouter API URL.
- The OpenRouter free-tier model
  (`nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free`) supports
  OpenAI-compatible tool/function calling.
- The MCP SDK's `McpClientTool` extends `Microsoft.Extensions.AI.AIFunction`
  and can be converted to a Semantic Kernel `KernelFunction` via the built-in
  `AsKernelFunction()` extension method (available since SK 1.44.0). No
  official SK-MCP connector package is required.
- MCP tool descriptions and parameter descriptions (from `[Description]`
  attributes on `[McpServerTool]` methods) are surfaced through the MCP
  protocol and are sufficient for an LLM to understand how to call each tool.
- MCP resources (via `[McpServerResource]` templates) are not convertible via
  `AsKernelFunction()` since they are not tools. A thin wrapper exposes the
  `buildin://{pageId}` resource template as an SK function calling
  `McpClient.ReadResourceAsync()`.
