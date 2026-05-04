---

description: "Task list for Scaffold + Buildin API Client"
---

# Tasks: Scaffold + Buildin API Client

**Input**: Design documents from `/specs/001-scaffold-api-client/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source projects**: `src/Buildout.Core/`, `src/Buildout.Mcp/`, `src/Buildout.Cli/`
- **Test projects**: `tests/Buildout.UnitTests/`, `tests/Buildout.IntegrationTests/`
- **Solution**: `buildout.slnx` at repo root
- **OpenAPI spec**: `openapi.json` at repo root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, tooling, and Kiota generation

- [ ] T001 Create `Directory.Build.props` at repo root with `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, `<AnalysisLevel>latest-recommended</AnalysisLevel>`, `<TargetFramework>net10.0</TargetFramework>` in `Directory.Build.props`
- [ ] T002 [P] Create `Directory.Build.targets` at repo root excluding `src/Buildout.Core/Buildin/Generated/` from analyzers and code-style checks in `Directory.Build.targets`
- [ ] T003 [P] Create `.config/dotnet-tools.json` pinning `Microsoft.OpenApi.Kiota` as a local tool in `.config/dotnet-tools.json`
- [ ] T004 [P] Create `src/Buildout.Core/Buildout.Core.csproj` with `Microsoft.Kiota.Bundle`, `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, and `Microsoft.Extensions.Options` in `src/Buildout.Core/Buildout.Core.csproj`
- [ ] T005 [P] Create `src/Buildout.Mcp/Buildout.Mcp.csproj` referencing `ModelContextProtocol` and `Buildout.Core`, plus `src/Buildout.Mcp/Program.cs` as a minimal console shell in `src/Buildout.Mcp/`
- [ ] T006 [P] Create `src/Buildout.Cli/Buildout.Cli.csproj` referencing `Spectre.Console.Cli` and `Buildout.Core`, plus `src/Buildout.Cli/Program.cs` as a Spectre app shell in `src/Buildout.Cli/`
- [ ] T007 [P] Create `tests/Buildout.UnitTests/Buildout.UnitTests.csproj` with `xunit.v3`, `xunit.runner.visualstudio`, `NSubstitute`, `Microsoft.NET.Test.Sdk`, and project reference to `Buildout.Core` in `tests/Buildout.UnitTests/Buildout.UnitTests.csproj`
- [ ] T008 [P] Create `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj` with `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, and project reference to `Buildout.Core` in `tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj`
- [ ] T009 Add all five projects to `buildout.slnx` via `dotnet sln buildout.slnx add` in `buildout.slnx`
- [ ] T010 Run `dotnet tool restore` then Kiota generate against `openapi.json` with flags `--language CSharp --class-name BuildinApiClient --namespace-name Buildout.Core.Buildin.Generated --output ./src/Buildout.Core/Buildin/Generated --clean-output --exclude-backward-compatible --log-level Warning` to populate `src/Buildout.Core/Buildin/Generated/`
- [ ] T011 [P] Create `src/Buildout.Core/Buildin/Generated/_README.md` marker explaining the directory is machine-generated and linking to `scripts/regenerate-buildin-client.sh` in `src/Buildout.Core/Buildin/Generated/_README.md`

**Checkpoint**: All five projects compile via `dotnet build buildout.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core abstractions and types that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T012 Create `BuildinClientOptions` POCO with `BaseUrl`, `BotToken`, `HttpTimeout`, `UnsafeAllowInsecure` properties plus `BuildinClientOptionsValidator` implementing `IValidateOptions<BuildinClientOptions>` in `src/Buildout.Core/Buildin/BuildinClientOptions.cs` and `src/Buildout.Core/Buildin/BuildinClientOptionsValidator.cs`
- [ ] T013 [P] Create `BuildinError` discriminated union (`TransportError`, `ApiError`, `UnknownError`) in `src/Buildout.Core/Buildin/Errors/BuildinError.cs`
- [ ] T014 [P] Create `BuildinApiException` with `.Error` property typed as `BuildinError` in `src/Buildout.Core/Buildin/Errors/BuildinApiException.cs`
- [ ] T015 [P] Create `BotTokenAuthenticationProvider` extending `BaseBearerTokenAuthenticationProvider` reading token from `BuildinClientOptions.BotToken` in `src/Buildout.Core/Buildin/Authentication/BotTokenAuthenticationProvider.cs`
- [ ] T016 [P] Create flat domain models (`User`, `UserMe`, `Page`, `Database`, `RichText`, `PaginatedList<T>`) in `src/Buildout.Core/Buildin/Models/`
- [ ] T017 [P] Create polymorphic domain models — `Block` abstract record + sealed subclasses keyed off `type` field, `Parent` abstract record (`ParentDatabase`, `ParentPage`, `ParentBlock`, `ParentSpace`), `Icon` abstract record (`IconEmoji`, `IconExternal`, `IconFile`), `PropertyValue` + 13 sealed subclasses, `PropertySchema` + 14 sealed subclasses — in `src/Buildout.Core/Buildin/Models/`
- [ ] T018 [P] Create request/response DTOs for all 15 operations (`CreatePageRequest`, `UpdatePageRequest`, `UpdateBlockRequest`, `AppendBlockChildrenRequest`, `AppendBlockChildrenResult`, `CreateDatabaseRequest`, `UpdateDatabaseRequest`, `QueryDatabaseRequest`, `QueryDatabaseResult`, `SearchRequest`, `SearchResults`, `PageSearchRequest`, `PageSearchResults`, `BlockChildrenQuery`) in `src/Buildout.Core/Buildin/Models/`
- [ ] T019 Create `IBuildinClient` interface with all 15 async method signatures (`GetMeAsync`, `GetPageAsync`, `CreatePageAsync`, `UpdatePageAsync`, `GetBlockAsync`, `UpdateBlockAsync`, `DeleteBlockAsync`, `GetBlockChildrenAsync`, `AppendBlockChildrenAsync`, `CreateDatabaseAsync`, `GetDatabaseAsync`, `UpdateDatabaseAsync`, `QueryDatabaseAsync`, `SearchAsync`, `SearchPagesAsync`) per `data-model.md` operation table in `src/Buildout.Core/Buildin/IBuildinClient.cs`
- [ ] T020 Create `ServiceCollectionExtensions.AddBuildinClient(IConfiguration)` registering `BuildinClientOptions`, `IAuthenticationProvider`, `IBuildinClient`, and `HttpClient` in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] T021 [P] Add `InternalsVisibleTo` declarations for `Buildout.UnitTests` and `Buildout.IntegrationTests` in `src/Buildout.Core/Properties/AssemblyInfo.cs`

**Checkpoint**: Foundation ready — `IBuildinClient`, all domain models, error types, options, and DI wiring exist. User story implementation can now begin in parallel.

---

## Phase 3: User Story 1 — Buildable solution skeleton (Priority: P1) 🎯 MVP

**Goal**: A developer clones the repo, runs a single build command, and gets five compiling projects with green test suites — no manual configuration beyond .NET SDK.

**Independent Test**: Clone a fresh checkout, run `dotnet build buildout.slnx` then `dotnet test buildout.slnx`; observe green build and green tests across all five projects.

### Tests for User Story 1

- [ ] T022 [P] [US1] Write smoke test that asserts `true` (proves test runner discovers and executes `Buildout.UnitTests`) in `tests/Buildout.UnitTests/SmokeTests.cs`
- [ ] T023 [P] [US1] Write smoke test that asserts `true` (proves test runner discovers and executes `Buildout.IntegrationTests`) in `tests/Buildout.IntegrationTests/SmokeTests.cs`

### Implementation for User Story 1

- [ ] T024 [US1] Verify `dotnet build buildout.slnx` succeeds and produces output for all five projects — fix any compilation errors
- [ ] T025 [US1] Verify `dotnet test buildout.slnx` reports zero failures across both test projects, confirming test infrastructure is wired

**Checkpoint**: At this point, the solution skeleton compiles and all smoke tests pass. US1 is fully delivered.

---

## Phase 4: User Story 2 — Typed Buildin API client (Priority: P2)

**Goal**: A downstream developer calls typed methods on `IBuildinClient` with strongly-typed request/response objects, without writing raw HTTP, JSON, or path interpolation. Errors surface in three typed categories.

**Independent Test**: Spin up an in-process mock HTTP handler returning a canned response for one buildin operation; instantiate the client via DI; call a typed method; assert the response object's typed fields are populated.

### Tests for User Story 2

> **NOTE: Write these tests FIRST; they MUST compile (against `IBuildinClient` and domain models) and FAIL before `BotBuildinClient` implementation exists.**

- [ ] T026 [P] [US2] Write unit tests for `BotBuildinClient` — mock `IRequestAdapter` via `NSubstitute`, verify typed request → typed response mapping for representative operations across all 15 methods in `tests/Buildout.UnitTests/Buildin/BotBuildinClientTests.cs`
- [ ] T027 [P] [US2] Write error mapping tests verifying `TransportError`, `ApiError`, and `UnknownError` surface correctly through `BuildinApiException` in `tests/Buildout.UnitTests/Buildin/ErrorMappingTests.cs`
- [ ] T028 [P] [US2] Write configuration binding tests verifying `BuildinClientOptions` validation rules (`BaseUrl` must be absolute, `BotToken` non-empty, `HttpTimeout > 0`, insecure flag) in `tests/Buildout.UnitTests/Buildin/ConfigurationBindingTests.cs`
- [ ] T029 [P] [US2] Write integration tests with custom `HttpMessageHandler` returning canned JSON responses, exercising full request/serialise/HTTP/deserialise path for representative operations in `tests/Buildout.IntegrationTests/Buildin/MockedHttpHarnessTests.cs`

### Implementation for User Story 2

- [ ] T030 [US2] Implement `BotBuildinClient` — wraps generated Kiota client, implements `IBuildinClient`, translates between generated wrapper types and hand-written domain models via private mapping methods, maps exceptions to `BuildinApiException` with the three `BuildinError` variants in `src/Buildout.Core/Buildin/BotBuildinClient.cs`
- [ ] T031 [US2] Verify all US2 unit tests pass (`dotnet test tests/Buildout.UnitTests`) and all US2 integration tests pass (`dotnet test tests/Buildout.IntegrationTests`)

**Checkpoint**: At this point, `IBuildinClient` is fully implemented behind `BotBuildinClient` with comprehensive unit and integration test coverage. US2 is fully delivered.

---

## Phase 5: User Story 3 — Reproducible client regeneration (Priority: P3)

**Goal**: A maintainer overwrites `openapi.json`, runs a single regeneration command, and the diff is mechanical — confined to the generated subtree with hand-written code untouched. Running twice against an unchanged spec produces zero diff.

**Independent Test**: Run the regeneration command twice; working tree is clean after each. Make a trivial addition to `openapi.json`; regenerate; confirm diff touches only `src/Buildout.Core/Buildin/Generated/` plus `openapi.json`.

### Tests for User Story 3

- [ ] T032 [P] [US3] Write determinism integration test that runs the regeneration script and asserts `git status` is clean (working tree unchanged against committed `Generated/` snapshot), and that `Generated/` is non-empty in `tests/Buildout.IntegrationTests/Buildin/RegenerationDeterminismTests.cs`

### Implementation for User Story 3

- [ ] T033 [US3] Create `scripts/regenerate-buildin-client.sh` — verifies `openapi.json` exists, restores Kiota local tool, invokes canonical `dotnet kiota generate` command with fixed flags, prints summary of changed files under `Generated/`, does NOT auto-commit in `scripts/regenerate-buildin-client.sh`
- [ ] T034 [P] [US3] Create PowerShell companion `scripts/regenerate-buildin-client.ps1` mirroring the bash script's behaviour in `scripts/regenerate-buildin-client.ps1`
- [ ] T035 [US3] Verify US3: run `./scripts/regenerate-buildin-client.sh` with unchanged `openapi.json`, confirm `git status` reports clean working tree; verify `dotnet test buildout.slnx` still passes

**Checkpoint**: Regeneration is deterministic and contained. All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup across all user stories

- [ ] T036 Run full `quickstart.md` validation — `dotnet tool restore`, `dotnet build buildout.slnx`, `dotnet test buildout.slnx` — confirming SC-001 (green build + tests in ≤ 5 min), SC-003 (suite < 60 s), and SC-006 (no outbound to `api.buildin.ai`)
- [ ] T037 [P] Verify `dotnet sln buildout.slnx list` shows exactly the five constitution-mandated projects (SC-002 layout)
- [ ] T038 [P] Verify no test depends on `BUILDOUT__BUILDIN__BOTTOKEN` being set — all tests run with the env var unset (FR-008, FR-010)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion (project files + generated code must exist)
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion (needs DI registration, options, error types to compile)
- **User Story 2 (Phase 4)**: Depends on Phase 2 completion (needs `IBuildinClient`, models, and generated client)
- **User Story 3 (Phase 5)**: Depends on Phase 1 (needs generated code baseline) and Phase 2 (needs `Buildout.Core` to compile for determinism test)
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Phase 2. No dependencies on other stories. MVP target.
- **User Story 2 (P2)**: Can start after Phase 2. No dependencies on US1 (though US1 smoke tests prove the build works).
- **User Story 3 (P3)**: Can start after Phase 1. No dependencies on US1 or US2 (though US3 tests need the project to compile, which requires Phase 2).

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services before endpoints
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- **Phase 1**: T001–T003 (config files) all [P]; T004–T008 (project files) all [P]; T010 (Kiota gen) depends on T003, T004
- **Phase 2**: T013–T018 (types/models) all [P]; T019 (`IBuildinClient`) depends on model types being defined; T020 (DI) depends on T012, T015, T019
- **Phase 3 (US1)**: T022, T023 are [P]
- **Phase 4 (US2)**: T026–T029 (all test tasks) are [P]; T030 depends on tests being written
- **Phase 5 (US3)**: T032 (test) and T034 (PS1 script) are [P]; T033 must exist before T032 can pass
- **Phase 6**: T037, T038 are [P]

---

## Parallel Example: Phase 2 (Foundational)

```text
# Launch all independent model/type tasks together:
Task T013: "Create BuildinError in src/Buildout.Core/Buildin/Errors/BuildinError.cs"
Task T014: "Create BuildinApiException in src/Buildout.Core/Buildin/Errors/BuildinApiException.cs"
Task T015: "Create BotTokenAuthenticationProvider in src/Buildout.Core/Buildin/Authentication/BotTokenAuthenticationProvider.cs"
Task T016: "Create flat domain models in src/Buildout.Core/Buildin/Models/"
Task T017: "Create polymorphic domain models in src/Buildout.Core/Buildin/Models/"
Task T018: "Create request/response DTOs in src/Buildout.Core/Buildin/Models/"
```

## Parallel Example: Phase 4 (US2 Tests)

```text
# Launch all US2 test tasks together:
Task T026: "Write BotBuildinClient unit tests in tests/Buildout.UnitTests/Buildin/BotBuildinClientTests.cs"
Task T027: "Write error mapping tests in tests/Buildout.UnitTests/Buildin/ErrorMappingTests.cs"
Task T028: "Write config binding tests in tests/Buildout.UnitTests/Buildin/ConfigurationBindingTests.cs"
Task T029: "Write integration tests in tests/Buildout.IntegrationTests/Buildin/MockedHttpHarnessTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: `dotnet build buildout.slnx` + `dotnet test buildout.slnx` green
5. Deploy/demo if ready — five projects compiling, smoke tests green

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Green build (MVP!)
3. Add User Story 2 → Test independently → Typed client with full coverage
4. Add User Story 3 → Test independently → Regeneration script + determinism
5. Polish → Quickstart validation, invariant checks

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (smoke tests + build verification)
   - Developer B: User Story 2 (unit tests + BotBuildinClient implementation)
   - Developer C: User Story 3 (regeneration script + determinism test)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Tests MUST fail before implementation — this is non-negotiable (Constitution Principle IV)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- No test may make a network call to `api.buildin.ai` (FR-008, SC-006)
- No secret or token may appear in committed source (FR-010)
- Generated code must not be hand-edited — only regenerated via the script
