# Tasks: CI/CD Pipeline

**Input**: Design documents from `/specs/004-cicd-pipeline/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Round-trip tests are required for any new or modified block-type support (Principle III). Tests are written before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- .NET 10 solution at repository root: `src/`, `tests/`, `.github/`
- Paths are relative to repository root unless otherwise stated

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add NuGet dependencies for WireMock and Semantic Kernel; remove Anthropic SDK.

- [ ] T001 Add `WireMock.Server` NuGet package to `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj`
- [ ] T002 [P] Add `Microsoft.SemanticKernel` NuGet package to `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj`
- [ ] T003 [P] Add `Microsoft.SemanticKernel.Connectors.OpenAI` NuGet package to `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj`
- [ ] T004 Remove `Anthropic.SDK` NuGet package from `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj`
- [ ] T005 Verify `dotnet build` succeeds after dependency changes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: WireMock fixture and stub definitions shared by all user stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T006 Create `BuildinWireMockFixture` class in `tests/Buildout.IntegrationTests/Buildin/BuildinWireMockFixture.cs` — starts `WireMockServer`, exposes `BaseUrl` and `CreateClient()` returning a real `BotBuildinClient` wired to the mock server, implements `IDisposable`
- [ ] T007 Create `BuildinStubs` static class in `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs` — define manual stubs for four endpoints (`GET /v1/users/me`, `GET /v1/pages/{page_id}`, `GET /v1/blocks/{block_id}/children`, `POST /v1/pages/search`) per contracts/wiremock-stubs.md, each method accepts optional `responseBody` override
- [ ] T008 Create xUnit collection definition `BuildinWireMockCollection` in `tests/Buildout.IntegrationTests/Buildin/BuildinWireMockFixture.cs` with `[CollectionDefinition]` and `ICollectionFixture<BuildinWireMockFixture>`
- [ ] T009 Verify fixture starts/stops cleanly by running existing `SmokeTests` — `dotnet test tests/Buildout.IntegrationTests --filter SmokeTest`

**Checkpoint**: Foundation ready — WireMock fixture available for all user stories.

---

## Phase 3: User Story 1 — Automated Build & Test (Priority: P1) 🎯 MVP

**Goal**: GitHub Actions workflow builds the solution, runs unit and integration tests in parallel, fails on any test failure.

**Independent Test**: Push a commit to a PR branch and observe green/red status check in GitHub.

### Tests for User Story 1

- [ ] T010 [US1] Create WireMock contract tests in `tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs` — for each stubbed endpoint, call the client method through `BuildinWireMockFixture` and assert the response deserializes into the correct hand-written model without error (verifies stubs match `openapi.json` via Kiota-generated models)
- [ ] T011 [US1] Rewrite `tests/Buildout.IntegrationTests/Buildin/MockedHttpHarnessTests.cs` — remove `MockHttpHandler` inner class, add `[Collection("BuildinWireMock")]`, inject `BuildinWireMockFixture`, override stubs per test with specific responses, assert same deserialization results as before
- [ ] T012 [P] [US1] Rewrite `tests/Buildout.IntegrationTests/Cli/GetCommandTests.cs` — remove `Substitute.For<IBuildinClient>()`, add `[Collection("BuildinWireMock")]`, inject `BuildinWireMockFixture`, use `fixture.CreateClient()` in DI, override stubs per test for page data and block children
- [ ] T013 [US1] Verify all integration tests pass: `dotnet test tests/Buildout.IntegrationTests` — zero network calls to buildin.ai

### Implementation for User Story 1

- [ ] T014 [US1] Create GitHub Actions CI workflow in `.github/workflows/ci.yml` — four jobs (`build`, `test-unit`, `test-integration`, `publish`), `build` runs `dotnet build -c Release`, `test-unit` and `test-integration` run in parallel depending on `build`, `publish` depends on both test jobs
- [ ] T015 [US1] Add `test-integration` job environment variable `OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}` to `.github/workflows/ci.yml`
- [ ] T016 [US1] Add `publish` job to `.github/workflows/ci.yml` — `dotnet publish` for `Buildout.Mcp` and `Buildout.Cli` with `-p:PublishSingleFile=true`, upload via `actions/upload-artifact@v4`

**Checkpoint**: CI workflow runs on every push/PR. All integration tests use WireMock. No `MockHttpHandler` or `IBuildinClient` NSubstitute mocks remain in `MockedHttpHarnessTests` or `GetCommandTests`.

---

## Phase 4: User Story 2 — WireMock-Based Buildin Mock Server (Priority: P1)

**Goal**: WireMock stubs verified against `openapi.json`; all buildin-facing integration tests exercise real HTTP paths.

**Independent Test**: `dotnet test` passes with zero network calls to buildin.ai. Contract tests prove stubs match OpenAPI schemas.

### Tests for User Story 2

- [ ] T017 [US2] Add contract test for `GET /v1/users/me` stub in `tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs` — assert `GetMeAsync()` returns a `UserMe` with expected fields
- [ ] T018 [P] [US2] Add contract test for `GET /v1/pages/{page_id}` stub in `tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs` — assert `GetPageAsync()` returns a `Page` with expected fields
- [ ] T019 [P] [US2] Add contract test for `GET /v1/blocks/{block_id}/children` stub in `tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs` — assert `GetBlockChildrenAsync()` returns a `PaginatedList<Block>`
- [ ] T020 [P] [US2] Add contract test for `POST /v1/pages/search` stub in `tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs` — assert `SearchPagesAsync()` returns a `PageSearchResults` with expected shape

### Implementation for User Story 2

- [ ] T021 [US2] Refine `BuildinStubs` response bodies in `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs` if contract tests reveal mismatches with `openapi.json` schemas
- [ ] T022 [US2] Verify WireMock request journal logs no unmatched requests after full test suite: add an assertion in `BuildinWireMockFixture.Dispose()` or a dedicated test that `Server.LogEntries` is empty of 404s

**Checkpoint**: All four stubbed endpoints have passing contract tests. WireMock stubs are verified sources of truth matching `openapi.json`.

---

## Phase 5: User Story 3 — LLM Integration Tests via OpenRouter and Semantic Kernel (Priority: P2)

**Goal**: LLM integration tests use Semantic Kernel + OpenRouter free-tier model instead of Anthropic SDK. Tests skip without `OPENROUTER_API_KEY`.

**Independent Test**: Set `OPENROUTER_API_KEY`, run `PageReadingLlmTests` — Semantic Kernel drives MCP tools via OpenRouter. Unset key → tests skip.

### Tests for User Story 3

- [ ] T023 [US3] Write failing test `LlmCanAnswerQuestionsAboutRenderedPage` using Semantic Kernel in `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs` — configure `Kernel` with `AddOpenAIChatCompletion` pointing to `https://openrouter.ai/api/v1`, model `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free`, send a prompt about rendered Markdown, assert response contains expected content, skip if `OPENROUTER_API_KEY` absent
- [ ] T024 [US3] Write failing test `LlmCanFindAndReadPage` using Semantic Kernel in `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs` — register MCP tools as SK plugins with `FunctionChoiceBehavior.Auto()`, use `BuildinWireMockFixture` for buildin responses, assert LLM invokes `search` and `read_buildin_page` tools and returns grounded answer, skip if `OPENROUTER_API_KEY` absent

### Implementation for User Story 3

- [ ] T025 [US3] Implement Semantic Kernel configuration helper in `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs` — create `Kernel` with `AddOpenAIChatCompletion` + custom `HttpClient { BaseAddress = "https://openrouter.ai/api/v1" }`, read `OPENROUTER_API_KEY` from environment, skip if absent
- [ ] T026 [US3] Implement MCP tool plugin wrapper for Semantic Kernel in `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs` — wrap `search` and `read_buildin_page` as `[KernelFunction]` methods or `KernelPluginFactory.CreateFromFunctions` delegates that call the MCP server through `McpClient`
- [ ] T027 [US3] Remove all `Anthropic.SDK` using directives and `AnthropicClient` usage from `tests/Buildout.IntegrationTests/Llm/PageReadingLlmTests.cs`
- [ ] T028 [US3] Add `[Collection("BuildinWireMock")]` to `PageReadingLlmTests`, inject `BuildinWireMockFixture`, replace `Substitute.For<IBuildinClient>()` with `fixture.CreateClient()`, override stubs for search and page data

**Checkpoint**: LLM tests run via Semantic Kernel + OpenRouter. Anthropic SDK fully removed. Tests skip without API key.

---

## Phase 6: User Story 4 — Publish MCP and CLI Artifacts (Priority: P3)

**Goal**: CI publishes framework-dependent single-file artifacts when all tests pass.

**Independent Test**: Download artifacts from a successful CI run; executables run on .NET 10 runtime.

### Implementation for User Story 4

- [ ] T029 [US4] Verify `publish` job in `.github/workflows/ci.yml` produces correct artifacts — ensure `dotnet publish` uses `-c Release -p:PublishSingleFile=true` for both `Buildout.Mcp` and `Buildout.Cli`
- [ ] T030 [US4] Verify `actions/upload-artifact@v4` steps produce two downloadable artifacts: `buildout-mcp` and `buildout-cli`
- [ ] T031 [US4] Verify `publish` job has `needs: [test-unit, test-integration]` and does not run when either test job fails (test by intentionally breaking a test and pushing)

**Checkpoint**: Artifacts downloadable from successful CI runs. Publish skipped on test failure.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup and verification.

- [ ] T032 Remove `MockHttpHandler` inner class from `tests/Buildout.IntegrationTests/Buildin/MockedHttpHarnessTests.cs` if any remnants remain
- [ ] T033 Verify no `using Anthropic.SDK` references remain anywhere in the codebase: `rg "Anthropic" tests/`
- [ ] T034 Verify no `Substitute.For<IBuildinClient>()` remains in `tests/Buildout.IntegrationTests/` (excluding `SearchToolTests` which mocks `ISearchService`)
- [ ] T035 Run full test suite: `dotnet test` — all green
- [ ] T036 Run quickstart.md validation — verify each command in the definition-of-done section

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories.
- **User Stories (Phase 3–6)**: All depend on Phase 2 completion.
  - US1 (Phase 3) and US2 (Phase 4) are both P1 and can proceed in parallel.
  - US3 (Phase 5) depends on US1 being mostly complete (WireMock conversion of `PageReadingLlmTests`).
  - US4 (Phase 6) is purely CI workflow verification; can start once US1's CI workflow is created.
- **Polish (Phase 7)**: Depends on all user stories being complete.

### User Story Dependencies

- **US1 (P1) — Build & Test**: Can start after Phase 2. Foundation for CI workflow.
- **US2 (P1) — WireMock Mock Server**: Can start after Phase 2. Contract tests are independent of US1.
- **US3 (P2) — LLM via OpenRouter**: Depends on US1 (needs WireMock-converted `PageReadingLlmTests`).
- **US4 (P3) — Publish Artifacts**: Depends on US1 (needs CI workflow in place).

### Within Each User Story

- Tests written and FAIL before implementation.
- Stub definitions before tests that use them.
- Fixture before tests that depend on it.
- CI workflow after tests are green.

### Parallel Opportunities

- T002, T003, T004 can run in parallel (different packages in same file, but sequential is fine too).
- T010, T011, T012 can run in parallel (different test files).
- T017, T018, T019, T020 can run in parallel (independent contract tests).
- T023, T024 can run in parallel (independent LLM tests).
- US1 and US2 phases can run in parallel with separate agents.

---

## Parallel Example: User Story 1

```
# Phase 2 complete. Launch US1 test rewrites in parallel:
Task T010: "Contract tests in WireMockContractTests.cs"
Task T011: "Rewrite MockedHttpHarnessTests.cs with WireMock"
Task T012: "Rewrite GetCommandTests.cs with WireMock"

# Then CI workflow (T014-T016) once tests pass.
```

## Parallel Example: User Story 2

```
# Phase 2 complete. Launch all contract tests in parallel:
Task T017: "Contract test GET /v1/users/me"
Task T018: "Contract test GET /v1/pages/{page_id}"
Task T019: "Contract test GET /v1/blocks/{block_id}/children"
Task T020: "Contract test POST /v1/pages/search"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (add/remove NuGet packages)
2. Complete Phase 2: Foundational (WireMock fixture + stubs)
3. Complete Phase 3: US1 (rewrite integration tests + CI workflow)
4. **STOP and VALIDATE**: `dotnet test` passes, CI workflow runs on push
5. Ship if ready — CI pipeline is functional

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → CI runs, tests green (MVP!)
3. Add US2 → Contract tests verify stubs match `openapi.json`
4. Add US3 → LLM tests via Semantic Kernel + OpenRouter
5. Add US4 → Artifact publishing verified
6. Polish → Final cleanup

---

## Notes

- `[P]` tasks = different files, no dependencies
- `[Story]` label maps task to specific user story for traceability
- `SearchToolTests.cs` is intentionally NOT modified (mocks `ISearchService`, not `IBuildinClient`)
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
