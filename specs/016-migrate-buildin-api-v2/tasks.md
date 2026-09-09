---

description: "Implementation tasks for the Buildin Developer API V2 migration"
---

# Tasks: Migrate to Buildin API V2

**Input**: Design documents from `/specs/016-migrate-buildin-api-v2/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`

**Tests**: Tests are mandatory under Constitution Principles III and IV. Write each listed test first, confirm it fails against the current V1 implementation, then implement only enough production code to make it pass. All HTTP tests use WireMock fixtures; no test may contact Buildin or use a real credential.

**Organization**: Tasks are grouped by user story. Phase 2 contains only the shared generated-client and core-boundary work that blocks every V2 runtime scenario.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes a distinct file with no incomplete prerequisite.
- **[Story]**: User story owning the task; labels are omitted for shared setup, foundational, and polish work.

## Path Conventions

- Shared domain and V2 API boundary: `src/Buildout.Core/`
- CLI and MCP presentations: `src/Buildout.Cli/`, `src/Buildout.Mcp/`
- Unit and WireMock integration tests: `tests/Buildout.UnitTests/`, `tests/Buildout.IntegrationTests/`

---

## Phase 1: Setup (Shared Migration Inputs)

**Purpose**: Establish the V2-specific, mock-only fixtures and task-facing documentation without changing a runtime route.

- [ ] T001 [P] Add V2 response builders, three-page cursor fixtures, ETag fixtures, and request-journal helpers in `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs`
- [ ] T002 [P] Add the V2 route assertions and isolated generated-artifact comparison helpers in `tests/Buildout.IntegrationTests/Buildin/BuildinWireMockFixture.cs` and `tests/Buildout.IntegrationTests/Buildin/FileSystemFixture.cs`
- [ ] T003 Record the reviewed V2 snapshot identity, one-operation overlay boundary, and pinned Kiota invocation in `specs/016-migrate-buildin-api-v2/contracts/regeneration.md`

---

## Phase 2: Foundational (Blocking V2 Core Boundary)

**Purpose**: Replace the shared transport contract and expose only version-neutral domain types before any retained workflow is migrated.

**⚠️ CRITICAL**: Complete this phase before implementing any user story.

- [ ] T004 Add the reviewed block-DELETE-only OpenAPI overlay in `scripts/openapi.buildout-overlay.json`
- [ ] T005 Add deterministic overlay application, V2 union naming, discriminator normalization, and collision validation in `scripts/normalize-buildin-openapi.cs`
- [ ] T006 Update V2-only fetch validation and atomic snapshot replacement in `scripts/fetch_openapi.sh`
- [ ] T007 Update the shell and PowerShell generation entry points for the normalized V2 input and `Generated/V2` output in `scripts/regenerate-buildin-client.sh` and `scripts/regenerate-buildin-client.ps1`
- [ ] T008 Regenerate the disposable V2-only Kiota client and generation metadata in `openapi.json`, `src/Buildout.Core/Buildin/Generated/V2/`, `src/Buildout.Core/Buildin/Generated/_README.md`, and `src/Buildout.Core/Buildin/Generated/kiota-lock.json`
- [ ] T009 Replace V1-specific facade types with the V2-ready interface, opaque versioned-page result, native write context, and `InTrash` domain fields in `src/Buildout.Core/Buildin/IBuildinClient.cs`, `src/Buildout.Core/Buildin/Models/VersionedPage.cs`, `src/Buildout.Core/Buildin/Models/NativeWriteContext.cs`, `src/Buildout.Core/Buildin/Models/Page.cs`, `src/Buildout.Core/Buildin/Models/Database.cs`, and `src/Buildout.Core/Buildin/Models/Block.cs`
- [ ] T010 Wire the V2 client, generic authentication provider, and shared configuration resolution through the core composition root in `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`

**Checkpoint**: The project compiles against a V2-only generated boundary; presentation projects still call only `IBuildinClient`.

---

## Phase 3: User Story 1 - Continue Existing Workflows on V2 (Priority: P1) 🎯 MVP

**Goal**: Preserve every retained CLI/MCP read, search, create, update, database-view, and tree workflow using only V2 routes, with stable output and the accepted ETag/`InTrash` naming changes.

**Independent Test**: Run the retained CLI and MCP journeys against the V2 WireMock server; verify the same output/order and no `/v1/` or page-lifecycle request, including a three-page traversal and a mixed non-destructive edit.

### Tests for User Story 1 — write and observe failure first

- [ ] T011 [P] [US1] Add exact V2 method/path/body assertions for all 13 retained core operations, including successful and failed block DELETE, in `tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs`
- [ ] T012 [P] [US1] Add V2 facade tests for generated DTO translation, `InTrash`, current user, search projections, cursor bounds/progress, and cancellation in `tests/Buildout.UnitTests/Buildin/BuildinClientTests.cs`
- [ ] T013 [P] [US1] Add tests for page-create idempotency-key reuse/new-invocation uniqueness, ETag capture, stale-edit preflight, absent block `If-Match`, and visible partial writes in `tests/Buildout.UnitTests/Buildin/BuildinClientRequestSafetyTests.cs` and `tests/Buildout.UnitTests/Markdown/Editing/PageEditorTests.cs`
- [ ] T014 [P] [US1] Update every supported block, rich-text, parent, property, and unsupported-block bidirectional V2 fixture case in `tests/Buildout.UnitTests/RoundTrip/MarkdownConverterRoundTripTests.cs` and `tests/Buildout.UnitTests/RoundTrip/WriteReadRoundTripTests.cs`
- [ ] T015 [P] [US1] Add retained CLI acceptance cases and V2 output/revision assertions in `tests/Buildout.IntegrationTests/Cli/GetCommandNoRegressionTests.cs`, `tests/Buildout.IntegrationTests/Cli/CreateCommandTests.cs`, `tests/Buildout.IntegrationTests/Cli/UpdateCommandTests.cs`, `tests/Buildout.IntegrationTests/Cli/SearchCommandTests.cs`, and `tests/Buildout.IntegrationTests/Cli/DbViewCommandTests.cs`; add deleted-command discovery/no-request checks in the new `tests/Buildout.IntegrationTests/Cli/CommandDiscoveryTests.cs`
- [ ] T016 [P] [US1] Add retained MCP resource/tool and CLI/MCP parity cases for V2 outputs, complete pagination, ETag edits, and absent lifecycle tools in `tests/Buildout.IntegrationTests/Mcp/PageResourceTests.cs`, `tests/Buildout.IntegrationTests/Mcp/UpdatePageToolTests.cs`, `tests/Buildout.IntegrationTests/Mcp/McpServerMetadataTests.cs`, and `tests/Buildout.IntegrationTests/Cross/EditModeParityTests.cs`

### Implementation for User Story 1

- [ ] T017 [US1] Implement the V2-only generated-client facade, route all retained semantic methods through it, and preserve cancellation/operation telemetry in `src/Buildout.Core/Buildin/BuildinClient.cs` and `src/Buildout.Core/Buildin/BuildinClientLog.cs`
- [ ] T018 [P] [US1] Translate V2 pages, databases, blocks, parents/icons, rich text, search unions, and current-user DTOs into stable domain models in `src/Buildout.Core/Buildin/Mapping/PageMapper.cs`, `src/Buildout.Core/Buildin/Mapping/DatabaseMapper.cs`, `src/Buildout.Core/Buildin/Mapping/BlockMapper.cs`, `src/Buildout.Core/Buildin/Mapping/ParentIconMapper.cs`, `src/Buildout.Core/Buildin/Mapping/RichTextMapper.cs`, `src/Buildout.Core/Buildin/Mapping/SearchMapper.cs`, and `src/Buildout.Core/Buildin/Mapping/UserMapper.cs`
- [ ] T019 [US1] Implement V2 cursor forwarding, page-size validation, ordered complete-result traversal, and repeated-cursor failure behavior in `src/Buildout.Core/Buildin/BuildinClient.cs` and `src/Buildout.Core/Buildin/Models/PaginatedList.cs`
- [ ] T020 [US1] Implement native page-create idempotency/retry, page-read ETag capture, V2 block DELETE, and no undocumented block-write precondition in `src/Buildout.Core/Buildin/BuildinClient.cs` and `src/Buildout.Core/Buildin/Models/NativeWriteContext.cs`
- [ ] T021 [US1] Replace content-hash revisions with opaque page ETags and fresh ETag preflight while retaining explicit partial-patch failures in `src/Buildout.Core/Markdown/Editing/PageEditor.cs`, `src/Buildout.Core/Markdown/Editing/AnchoredPageSnapshot.cs`, and `src/Buildout.Core/Markdown/Editing/StaleRevisionException.cs`
- [ ] T022 [US1] Remove the obsolete local revision authority in `src/Buildout.Core/Markdown/Editing/Internal/RevisionTokenComputer.cs` and `tests/Buildout.UnitTests/Markdown/Editing/RevisionTokenComputerTests.cs`
- [ ] T023 [US1] Remove the complete page-lifecycle core vertical slice and its registrations/tests in `src/Buildout.Core/PageLifecycle/`, `src/Buildout.Core/Buildin/Models/UpdatePageRequest.cs`, `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`, `tests/Buildout.UnitTests/PageLifecycle/`, and `tests/Buildout.UnitTests/Caching/PageLifecycleInvalidationTests.cs`
- [ ] T024 [US1] Remove CLI page delete/restore registrations, settings, commands, skills, and their dedicated integration tests in `src/Buildout.Cli/Program.cs`, `src/Buildout.Cli/Commands/DeleteCommand.cs`, `src/Buildout.Cli/Commands/DeleteSettings.cs`, `src/Buildout.Cli/Commands/RestoreCommand.cs`, `src/Buildout.Cli/Commands/RestoreSettings.cs`, `src/Buildout.Cli/Skills/delete.md`, `src/Buildout.Cli/Skills/restore.md`, `tests/Buildout.IntegrationTests/Cli/DeleteCommandTests.cs`, and `tests/Buildout.IntegrationTests/Cli/RestoreCommandTests.cs`
- [ ] T025 [US1] Remove MCP page delete/restore handlers, tool registrations, prompt references, and lifecycle-specific integration tests in `src/Buildout.Mcp/Program.cs`, `src/Buildout.Mcp/Tools/DeletePageToolHandler.cs`, `src/Buildout.Mcp/Tools/RestorePageToolHandler.cs`, `src/Buildout.Mcp/Prompts/server-instructions.md`, `tests/Buildout.IntegrationTests/Mcp/DeletePageToolTests.cs`, `tests/Buildout.IntegrationTests/Mcp/DeleteAndRestoreLlmTests.cs`, and `tests/Buildout.IntegrationTests/Cross/DeleteRestoreSymmetryTests.cs`
- [ ] T026 [US1] Update retained CLI skills and MCP update guidance for V2 `AccessToken`, opaque ETag revisions, scoped access, and non-atomic block reconciliation in `src/Buildout.Cli/Skills/SKILL.md`, `src/Buildout.Cli/Skills/create.md`, `src/Buildout.Cli/Skills/read.md`, `src/Buildout.Cli/Skills/update.md`, `src/Buildout.Mcp/Prompts/update.md`, and `src/Buildout.Mcp/Prompts/server-instructions.md`

**Checkpoint**: Retained workflows are V2-only and independently usable. Removed page lifecycle surfaces are undiscoverable and cannot cause HTTP traffic.

---

## Phase 4: User Story 2 - Diagnose Authentication and Permission Problems (Priority: P2)

**Goal**: Accept integration and OAuth bearer credentials through one safe configuration path and provide classified, actionable diagnostics without leaking secrets.

**Independent Test**: With mock responses and sentinel credentials, exercise missing/invalid credentials, scope/resource/plan denials, validation, not found, conflict, rate limit, and transport failures through both CLI and MCP; verify each category, request ID/details, and absence of secrets.

### Tests for User Story 2 — write and observe failure first

- [ ] T027 [P] [US2] Add primary/legacy credential precedence, exactly-once value-free warning, empty-value validation, configured-host restriction, and integration/OAuth equivalence tests in `tests/Buildout.UnitTests/Buildin/AccessTokenResolverTests.cs`, `tests/Buildout.UnitTests/Buildin/ConfigurationBindingTests.cs`, and `tests/Buildout.UnitTests/Buildin/AccessTokenAuthenticationProviderTests.cs`
- [ ] T028 [P] [US2] Add structured V2 error parsing/classification, retry-after preservation, safe rendering, and secret/header redaction tests in `tests/Buildout.UnitTests/Buildin/ErrorMappingTests.cs` and `tests/Buildout.UnitTests/Buildin/BuildinApiExceptionTests.cs`
- [ ] T029 [P] [US2] Add WireMock protected-operation acceptance cases for 400, 401, 403, 404, 409, 429, transport, and unexpected failures with request IDs/details in `tests/Buildout.IntegrationTests/Buildin/MockedHttpHarnessTests.cs`
- [ ] T030 [P] [US2] Add CLI stderr/MCP logging and cross-transport secret-sentinel assertions for AccessToken and BotToken fallback behavior in `tests/Buildout.IntegrationTests/Configuration/SecretLeakTests.cs`, `tests/Buildout.IntegrationTests/Configuration/PrecedenceMatrixTests.cs`, `tests/Buildout.IntegrationTests/Mcp/McpServerMetadataTests.cs`, and `tests/Buildout.IntegrationTests/Cross/CreatePageIdEquivalenceTests.cs`

### Implementation for User Story 2

- [ ] T031 [US2] Introduce primary AccessToken resolution with deprecated BotToken fallback and once-per-process safe warnings in `src/Buildout.Core/Buildin/AccessTokenResolver.cs`, `src/Buildout.Core/Buildin/BuildinClientOptions.cs`, and `src/Buildout.Core/Buildin/BuildinClientOptionsValidator.cs`
- [ ] T032 [US2] Replace bot-specific authentication with configured-host generic bearer authentication in `src/Buildout.Core/Buildin/Authentication/AccessTokenAuthenticationProvider.cs` and `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] T033 [US2] Preserve V2 status, code, request ID, details, retry metadata, and distinct failure categories without sensitive data in `src/Buildout.Core/Buildin/Errors/BuildinError.cs`, `src/Buildout.Core/Buildin/Errors/BuildinApiException.cs`, and `src/Buildout.Core/Buildin/BuildinClientLog.cs`
- [ ] T034 [US2] Map classified core failures to established CLI exit/error behavior and MCP error envelopes without writing warnings to protocol stdout in `src/Buildout.Cli/Program.cs`, `src/Buildout.Mcp/Program.cs`, `src/Buildout.Mcp/Resources/PageResourceHandler.cs`, and `src/Buildout.Mcp/Tools/UpdatePageToolHandler.cs`
- [ ] T035 [US2] Document dual-channel AccessToken configuration, BotToken precedence/deprecation, bearer safety, operation scopes, and error guidance in `docs/configuration.md`, `docs/configuration.example.json`, and `specs/016-migrate-buildin-api-v2/contracts/scope-matrix.md`

**Checkpoint**: Both credential types are treated identically as bearer tokens, legacy configuration remains safe and visible, and callers can act on classified failures without credential exposure.

---

## Phase 5: User Story 3 - Maintain Contract Fidelity (Priority: P3)

**Goal**: Make V2 snapshot refresh, overlay application, normalization, and client generation reproducible and reviewable without expanding the public feature surface.

**Independent Test**: In an isolated temporary directory, validate the V2 snapshot and apply the one-operation overlay, regenerate twice, compare effective-contract/generated hashes, and prove that a V1 contract or upstream block-DELETE collision fails before modifying checked-in artifacts.

### Tests for User Story 3 — write and observe failure first

- [ ] T036 [P] [US3] Add canonical V2 identity, V2-only route inventory, one-action overlay, collision, and normalizer determinism tests in `tests/Buildout.UnitTests/Buildin/OpenApiOverlayTests.cs`
- [ ] T037 [P] [US3] Replace the skipped regeneration check with isolated two-pass effective-contract and generated-manifest/hash assertions in `tests/Buildout.IntegrationTests/Buildin/RegenerationDeterminismTests.cs`
- [ ] T038 [P] [US3] Add a source/journal gate that rejects generated or production V1 routes and verifies no overlaid page lifecycle operation exists in `tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs`

### Implementation for User Story 3

- [ ] T039 [US3] Enforce snapshot title/version/hash/path validation, V1 rejection, metadata reporting, and no-op unchanged refresh behavior in `scripts/fetch_openapi.sh`
- [ ] T040 [US3] Enforce overlay hash/absence preconditions, canonical UTF-8/LF effective output, stable union normalization, and failure-on-guess behavior in `scripts/normalize-buildin-openapi.cs`
- [ ] T041 [US3] Make both regeneration scripts use the identical pinned Kiota arguments, clean V2 output, and stable generated README marker in `scripts/regenerate-buildin-client.sh`, `scripts/regenerate-buildin-client.ps1`, and `src/Buildout.Core/Buildin/Generated/_README.md`
- [ ] T042 [US3] Publish the V2 support inventory, accepted removals, native header limits, overlay removal procedure, and mock-only refresh instructions in `docs/buildin-api-v2.md` and `specs/016-migrate-buildin-api-v2/contracts/api-routing.md`

**Checkpoint**: A maintainer can reproduce generation offline, review only V2 artifacts, and is stopped before a stale overlay or V1 source can enter the codebase.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate the full migration and eliminate remaining V1/lifecycle terminology without weakening test coverage.

- [ ] T043 [P] Update the feature validation guide with focused V2, round-trip, CLI/MCP, secret-safety, pagination, and determinism commands in `specs/016-migrate-buildin-api-v2/quickstart.md`
- [ ] T044 [P] Remove remaining V1, `Archived`, lifecycle, and obsolete bot-specific public references while retaining only the documented BotToken fallback in `src/`, `tests/`, `docs/`, and `openapi.json`
- [ ] T045 Run and record the required unit and mocked-integration merge gates from `specs/016-migrate-buildin-api-v2/quickstart.md` in `specs/016-migrate-buildin-api-v2/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately. T001–T002 can proceed in parallel; T003 documents the shared generation constraints.
- **Foundational (Phase 2)**: Depends on setup. T004 → T005 → T007 → T008 establishes the generated V2 client; T009–T010 expose the shared domain boundary. It blocks runtime story work.
- **User Story 1 (Phase 3)**: Depends on Phase 2. It is the MVP and must finish before the end-to-end migration is demonstrated.
- **User Story 2 (Phase 4)**: Depends on Phase 2 and uses the shared V2 facade; it can be developed alongside US1 once the core boundary is available, but its presentation checks should be reconciled after US1 removes the lifecycle surface.
- **User Story 3 (Phase 5)**: Depends on Phase 2. Its tests and maintainer tooling can proceed alongside US1/US2 after the generated V2 input exists.
- **Polish (Phase 6)**: Depends on every desired story and validates the combined system.

### User Story Dependencies

- **US1 (P1)**: Requires Phase 2 only; no dependency on US2 or US3.
- **US2 (P2)**: Requires Phase 2 only; augments US1 with safe credentials and diagnostics but remains independently testable through protected mocked operations.
- **US3 (P3)**: Requires Phase 2 only; its isolated tooling tests are independent of runtime workflow acceptance.

### Within Each User Story

- Complete its test tasks first and observe their failure.
- Implement domain mapping/client behavior before presentation changes.
- Preserve the core-to-presentation boundary; generated V2 DTOs never cross `IBuildinClient`.
- Run the story’s independent test before moving to the next checkpoint.

## Parallel Opportunities

- T001, T002, and all explicitly `[P]` test tasks modify distinct fixture/test files and can be assigned independently after their phase prerequisites.
- After Phase 2, US1 mapping/client work, US2 credential/error work, and US3 normalization/determinism work use mostly separate files and can proceed concurrently.
- Within US1, DTO mapper work (T018) can proceed alongside the removal-focused presentation work (T024–T026) once T017 establishes the stable V2 facade.

## Parallel Example: User Story 1

```text
Task: "Add exact V2 method/path/body assertions in tests/Buildout.IntegrationTests/Buildin/WireMockContractTests.cs"
Task: "Add V2 facade mapping and pagination tests in tests/Buildout.UnitTests/Buildin/BuildinClientTests.cs"
Task: "Update V2 block round trips in tests/Buildout.UnitTests/RoundTrip/MarkdownConverterRoundTripTests.cs"
Task: "Add retained CLI V2 acceptance cases in tests/Buildout.IntegrationTests/Cli/GetCommandNoRegressionTests.cs"
Task: "Add retained MCP V2 acceptance cases in tests/Buildout.IntegrationTests/Mcp/PageResourceTests.cs"
```

## Implementation Strategy

### MVP First (US1 only)

1. Complete setup and the V2-only foundational boundary.
2. Write T011–T016 and confirm the current implementation fails them.
3. Complete T017–T026, then run the US1 WireMock, CLI/MCP, pagination, and round-trip tests.
4. Demo retained workflows using mock V2 only; verify lifecycle surfaces are absent and the route journal has no `/v1/` request.

### Incremental Delivery

1. Foundation → V2 generated client and stable domain seam.
2. US1 → retained workflows on V2 (MVP).
3. US2 → credential migration and safe diagnostics.
4. US3 → reproducible contract refresh and generation.
5. Polish → full mocked merge gate and terminology/route audit.

## Notes

- `[P]` means distinct files and no unresolved prerequisite, not permission to bypass test-first ordering.
- The only retained compatibility stub is the warning-emitting `BotToken` configuration fallback; there is no V1 runtime fallback or lifecycle alias.
- Block edits are intentionally non-atomic after their fresh ETag comparison; no task may introduce undocumented `If-Match` headers or report a partial failure as success.
- All tasks use the required checklist format with sequential IDs, story labels for story work, and concrete repository paths.
