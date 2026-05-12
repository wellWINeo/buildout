# Implementation Plan: CI/CD Pipeline

**Branch**: `004-cicd-pipeline` | **Date**: 2026-05-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-cicd-pipeline/spec.md`

## Summary

Introduce a GitHub Actions CI/CD pipeline that builds the .NET 10
solution, runs unit and integration tests in parallel, and publishes
framework-dependent single-file artifacts for `Buildout.Mcp` and
`Buildout.Cli` when all tests pass.

Three supporting changes ship in the same feature:

1. **WireMock.NET** replaces the hand-rolled `MockHttpHandler` and all
   NSubstitute `IBuildinClient` mocks in `Buildout.IntegrationTests`.
   Stubs are manually defined in C# fluent API, maintained to match
   `openapi.json` endpoints, and verified by contract tests that
   deserialize stub responses through the Kiota-generated models.

2. **Semantic Kernel** replaces the `Anthropic.SDK` dependency in the
   integration test project. LLM tests call OpenRouter via SK's
   `AddOpenAIChatCompletion` with a custom `HttpClient` pointing at
   `https://openrouter.ai/api/v1`, using model
   `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free`. MCP tools are
   discovered via `McpClient.ListToolsAsync()` and dynamically registered
   as SK plugins using MCP-native schemas and descriptions — validating
   that the MCP server's tool metadata is LLM-comprehensible.

3. **GitHub Actions workflow** (`.github/workflows/ci.yml`) defines four
   jobs — `build`, `test-unit`, `test-integration`, `publish` — with
   `test-unit` and `test-integration` running in parallel after build,
   and `publish` gated on both passing.

## Technical Context

**Language/Version**: C# / .NET 10. Already in place from features
001–003.

**Primary Dependencies (additions in this feature)**:

- `WireMock.Server` — self-hosted HTTP mock server for integration tests.
- `Microsoft.SemanticKernel` — LLM orchestration SDK.
- `Microsoft.SemanticKernel.Connectors.OpenAI` — OpenAI-compatible
  connector for SK (used to call OpenRouter).

**Dependencies removed**:

- `Anthropic.SDK` — replaced by Semantic Kernel.

**Storage**: N/A — no persistence in this feature.

**Testing**: xUnit + NSubstitute (already in place). WireMock adds
HTTP-level mocking. Semantic Kernel adds LLM-driven test orchestration.
`SearchToolTests` (which mocks `ISearchService`, not `IBuildinClient`)
is not modified.

**Target Platform**: GitHub Actions (`ubuntu-latest` runner), .NET 10.

**Project Type**: CI/CD configuration + test infrastructure changes for
the existing five-project .NET solution.

**Performance Goals (from spec SCs)**:

- Full CI pipeline completes within 10 minutes (SC-001).
- Unit and integration test jobs run in parallel (SC-002).

**Constraints**:

- No outbound HTTPS to `api.buildin.ai` from any test (Constitution
  Principle IV; FR-014).
- No test depends on real buildin tokens or live buildin network access.
- LLM tests skip without `OPENROUTER_API_KEY`; they never fail from
  missing secrets (FR-008).
- LLM tests only run on `main` pushes and PR branches (FR-009).
- `SearchToolTests` is not modified (see research R3).

**Scale/Scope**:

- 1 new GitHub Actions workflow file.
- 1 new WireMock fixture class + 1 stub definitions class.
- 3 modified integration test files (MockedHttpHarnessTests,
  GetCommandTests, PageReadingLlmTests).
- 1 new MCP-to-SK bridge helper (McpSkBridge.cs).
- 1 new contract test file (WireMock stub ↔ OpenAPI schema validation).
- 2 modified `.csproj` files (add WireMock + SK, remove Anthropic).
- ~1 new contract file in `contracts/`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Compliance | Notes |
|---|---|---|---|
| I | Core/Presentation Separation (NON-NEGOTIABLE) | ✅ PASS | No changes to `Buildout.Core` source code. WireMock fixture and Semantic Kernel client are test-only infrastructure in `Buildout.IntegrationTests`. CI workflow does not touch core or presentation code. |
| II | LLM-Friendly Output Fidelity | ➖ N/A | No new output rendering in this feature. Existing rendering is exercised by existing tests that now run through WireMock instead of interface mocks. |
| III | Bidirectional Round-Trip Testing | ➖ N/A | No new block types or conversion logic. |
| IV | Test-First Discipline (NON-NEGOTIABLE) | ✅ PASS | WireMock enforces HTTP-level mocking stronger than NSubstitute interface mocks — tests exercise real HTTP deserialization paths. Contract tests verify stubs match `openapi.json`. LLM tests via Semantic Kernel exercise real tool-call protocol. No test is skipped or deleted to make the build pass. The LLM test skip is for missing secrets only (constitution allows: "Tests MUST use fixtures or mocks"). |
| V | Buildin API Abstraction | ✅ PASS | WireMock tests construct a real `BotBuildinClient` (the Bot API implementation) wired to the mock server. The `IBuildinClient` interface remains the only seam. A future `UserApiBuildinClient` would be testable with the same fixture by swapping the client implementation. |
| VI | Non-Destructive Editing | ➖ N/A | No edit operations in this feature. |

| Standard | Compliance | Notes |
|---|---|---|
| .NET 10 target framework | ✅ | All projects unchanged. |
| Nullable + warnings-as-errors | ✅ | All new code respects `Directory.Build.props`. |
| `ModelContextProtocol` SDK | ✅ | Not modified. MCP tool/resource handlers unchanged. |
| `Spectre.Console.Cli` | ✅ | Not modified. |
| Solution layout (5 projects) | ✅ | No new projects. |
| Bot-API as one impl of `IBuildinClient`; User API path open | ✅ | WireMock tests use the real `BotBuildinClient`; interface abstraction is preserved. |
| Secrets from env/config; no committed tokens | ✅ | `OPENROUTER_API_KEY` read from environment / GitHub secret. No tokens in source. |

| Out-of-scope item | Respected? |
|---|---|
| Admin dashboard | ✅ Not added. |
| Managed/enterprise deployment | ✅ Not added. |
| Multi-tenant hosting | ✅ Not added. |
| Changes to `Buildout.Core` source | ✅ No production code changes. |
| Changes to `SearchToolTests` | ✅ Not modified (see research R3). |

**Gate result (pre-Phase 0)**: PASS — no unjustified violations.

**Re-check after Phase 1 design**: PASS — no new violations introduced.

- WireMock fixture is test-only code, not production code.
- Semantic Kernel is a test-only dependency, not added to production
  projects.
- CI workflow is infrastructure, not application code.
- Contract tests verify WireMock stubs against `openapi.json` without
  touching `Buildout.Core` source.

`Complexity Tracking` table remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/004-cicd-pipeline/
├── plan.md                      # This file (/speckit-plan output)
├── research.md                  # Phase 0 output
├── data-model.md                # Phase 1 output
├── quickstart.md                # Phase 1 output
├── contracts/                   # Phase 1 output
│   └── wiremock-stubs.md
├── checklists/
│   └── requirements.md          # spec quality checklist (already created)
└── tasks.md                     # Phase 2 (/speckit-tasks output — not in this command)
```

### Source Code (repository root)

```text
.github/
  workflows/
    ci.yml                                      # NEW: GitHub Actions CI/CD workflow

tests/
  Buildout.IntegrationTests/
    Buildout/
      BuildinWireMockFixture.cs                 # NEW: WireMock server fixture (ICollectionFixture)
      BuildinStubs.cs                           # NEW: manual stub definitions per endpoint
      WireMockContractTests.cs                  # NEW: verify stubs match openapi.json via Kiota models
      MockedHttpHarnessTests.cs                 # REWRITTEN: uses WireMock instead of MockHttpHandler
    Cli/
      GetCommandTests.cs                        # MODIFIED: uses WireMock + real BotBuildinClient
    Llm/
      McpSkBridge.cs                              # NEW: MCP-to-SK plugin bridge (discovers tools via McpClient.ListToolsAsync())
      PageReadingLlmTests.cs                    # MODIFIED: uses WireMock + Semantic Kernel with MCP-native tool discovery instead of hand-authored wrappers
    Mcp/
      SearchToolTests.cs                        # UNCHANGED (mocks ISearchService, not IBuildinClient)
    SmokeTests.cs                               # UNCHANGED
    Buildout.IntegrationTests.csproj             # MODIFIED: add WireMock.Server + SK packages, remove Anthropic.SDK
```

**Structure Decision**: No new production projects or directories. All
changes are in the test infrastructure layer (`Buildout.IntegrationTests`)
and a new `.github/workflows/` directory for CI. The WireMock fixture
and stubs live alongside the existing `Buildin/` test subdirectory.
`SearchToolTests.cs` is intentionally not modified (see research R3).

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

*No violations.*
