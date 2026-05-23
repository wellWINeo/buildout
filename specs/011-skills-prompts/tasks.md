# Tasks: Skills & Prompts

**Input**: Design documents from `/specs/011-skills-prompts/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/cli-skills.md

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire embedded resources into both projects and verify the loading mechanism works.

 - [X] T001 Add `<EmbeddedResource Include="Skills\**\*.md" />` to `src/Buildout.Cli/Buildout.Cli.csproj` so all markdown files under `Skills/` are bundled in the assembly
 - [X] T002 [P] Add `<EmbeddedResource Include="Prompts\**\*.md" />` to `src/Buildout.Mcp/Buildout.Mcp.csproj` so all markdown files under `Prompts/` are bundled in the assembly
 - [X] T003 [P] Create `src/Buildout.Cli/Skills/SkillResourceLoader.cs` — a static helper that discovers and loads all embedded skill resources from the assembly (`Assembly.GetExecutingAssembly().GetManifestResourceNames()` filtered to the `Skills.` prefix), returning file names and content strings. Static is appropriate here: the loader reads read-only embedded resources with no external dependencies, so mocking adds no test value. Test via integration tests (T010, T013) against the real assembly.
 - [X] T004 [P] Create `src/Buildout.Mcp/Prompts/PromptResourceLoader.cs` — a static helper that loads a named embedded prompt resource from the assembly by its logical name (e.g., `"server-instructions"`, `"update"`), returning the content string. Static is appropriate here: the loader reads read-only embedded resources with no external dependencies, so mocking adds no test value. Test via integration tests (T011, T020) against the real assembly.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented — settings, DI registration, and the embedded resource loading tests.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

 - [X] T005 Create `src/Buildout.Cli/Commands/SkillsSettings.cs` — bare `CommandSettings` subclass as the branch root (matching the `DbSettings` pattern) in namespace `Buildout.Cli.Commands`
 - [X] T006 [P] Create `src/Buildout.Cli/Commands/SkillsInstallSettings.cs` — inherits `SkillsSettings`, with `[CommandOption("--agent")]` (required, values `claude`/`opencode`), `[CommandOption("--local")]` (boolean, default false), `[CommandOption("--overwrite")]` (boolean, default false)
 - [X] T007 [P] Create `src/Buildout.Cli/Commands/SkillsRemoveSettings.cs` — inherits `SkillsSettings`, with `[CommandOption("--agent")]` (required), `[CommandOption("--local")]` (boolean, default false)
 - [X] T008 Create `src/Buildout.Cli/Commands/AgentTarget.cs` — maps agent name to global/local directory paths per the data model: claude → `~/.claude/skills/buildout-cli/` (global) or `.claude/skills/buildout-cli/` (local); opencode → `~/.config/opencode/skills/buildout-cli/` (global) or `.opencode/skills/buildout-cli/` (local). Include validation for unsupported agent names
 - [X] T009 Register the `skills` branch in `src/Buildout.Cli/Program.cs` — add `config.AddBranch<SkillsSettings>("skills", skills => { skills.AddCommand<SkillsInstallCommand>("install"); skills.AddCommand<SkillsRemoveCommand>("remove"); })` alongside the existing `db` branch
 - [X] T010 [P] Create `tests/Buildout.UnitTests/Cli/EmbeddedResourceTests.cs` — unit tests that verify all 8 skill embedded resources (`SKILL.md`, `create.md`, `read.md`, `update.md`, `delete.md`, `restore.md`, `search.md`, `database-views.md`) are present in the `Buildout.Cli` assembly and contain non-empty content
 - [X] T011 [P] Create `tests/Buildout.UnitTests/Mcp/EmbeddedResourceTests.cs` — unit tests that verify both prompt embedded resources (`server-instructions.md`, `update.md`) are present in the `Buildout.Mcp` assembly and contain non-empty content, AND assert no other resources exist under the `Buildout.Mcp.Prompts.` prefix (FR-014: only relevant prompt resources loaded)

**Checkpoint**: Embedded resources are wired, loaders work, settings types compile, branch is registered — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 — Install Agent Skills (Priority: P1) 🎯 MVP

**Goal**: A developer runs `buildout-cli skills install --agent claude` and skill files are written to the correct agent config directory.

**Independent Test**: Run the install command targeting a temp directory and verify skill files appear with correct content matching embedded resources.

### Tests for User Story 1

- [X] T012 [P] [US1] Create `tests/Buildout.UnitTests/Cli/SkillsInstallCommandTests.cs` — unit tests for: (1) valid agent installs files to correct directory, (2) `--local` creates directory if missing, (3) existing files without `--overwrite` returns error and exit code 2, (4) `--overwrite` replaces existing files, (5) unsupported agent name returns exit code 2 with supported-agent message, (6) global install when parent dir missing returns exit code 2 with create-first-or-local message, (7) correct file count in success output, (8) permission-denied scenario returns exit code 2 with clear error
- [X] T013 [P] [US1] Create `tests/Buildout.IntegrationTests/Cli/SkillsCommandTests.cs` — end-to-end integration tests using `CommandApp` + `TestConsole` + temp directories: (1) `install --agent claude` writes all 8 skill files, (2) `install --agent opencode --local` creates `.opencode/skills/buildout-cli/` and writes files, (3) `install --agent claude` when files exist fails with overwrite suggestion, (4) `install --agent claude --overwrite` succeeds over existing files, (5) install completes in under 1 second (SC-002)

### Implementation for User Story 1

- [X] T014 [US1] Implement `src/Buildout.Cli/Commands/SkillsInstallCommand.cs` — `AsyncCommand<SkillsInstallSettings>` that: resolves target directory via `AgentTarget`, validates agent name, checks for existing files (error if found without `--overwrite`; `--overwrite` is install-only, not available on `remove`), checks global parent dir exists (error if not), creates directory tree for `--local`, loads all skill resources via `SkillResourceLoader`, writes each file to disk, handles `UnauthorizedAccessException` with exit code 2 and clear error message, outputs success message with file count. Return 0 on success, 2 on validation/precondition/permission errors
- [X] T015 [US1] Review `src/Buildout.Cli/Skills/SKILL.md` — verify entrypoint has valid YAML frontmatter (`name: buildout-cli`, `description`), command overview, typical workflow, and relative file references to all topic files per the Agent Skills specification

**Checkpoint**: At this point, `buildout-cli skills install --agent claude` and `--agent opencode` should work fully, including `--local` and `--overwrite` flags

---

## Phase 4: User Story 2 — Remove Agent Skills (Priority: P2)

**Goal**: A developer runs `buildout-cli skills remove --agent claude` and the skill directory is deleted.

**Independent Test**: Install skills, then remove them, verify directory is gone.

### Tests for User Story 2

- [X] T016 [P] [US2] Create `tests/Buildout.UnitTests/Cli/SkillsRemoveCommandTests.cs` — unit tests for: (1) existing directory is deleted, (2) non-existent directory returns graceful message, (3) unsupported agent name returns exit code 2, (4) success message includes file count
- [X] T017 [P] [US2] Add remove scenarios to `tests/Buildout.IntegrationTests/Cli/SkillsCommandTests.cs` — (1) `remove --agent claude` after install deletes directory, (2) `remove --agent claude` when not installed shows not-found message, (3) `remove --agent opencode --local` works correctly

### Implementation for User Story 2

- [X] T018 [US2] Implement `src/Buildout.Cli/Commands/SkillsRemoveCommand.cs` — `AsyncCommand<SkillsRemoveSettings>` that: resolves target directory via `AgentTarget`, validates agent name, checks if directory exists, deletes directory recursively if found (output file count), handles `UnauthorizedAccessException` with exit code 2 and clear error message, outputs not-found message if missing. Return 0 on success or not-found, 2 on validation/permission errors

**Checkpoint**: At this point, both install and remove subcommands work end-to-end for all supported agents and flags

---

## Phase 5: User Story 3 — MCP Embedded Prompts (Priority: P1) 🎯 MVP

**Goal**: MCP server loads base instructions from embedded resources and exposes an `update` named prompt on demand.

**Independent Test**: Start MCP server, verify compact base instructions in server metadata, verify `update` named prompt returns detailed content from embedded resources.

### Tests for User Story 3

- [X] T019 [P] [US3] Create `tests/Buildout.UnitTests/Mcp/BuildoutPromptsTests.cs` — unit tests for: (1) `BuildoutPrompts.UpdateWorkflow()` returns non-empty `ChatMessage` with `ChatRole.User`, (2) content matches the embedded `update.md` resource, (3) base instructions loaded from embedded `server-instructions.md` match expected compact format, (4) base instructions are under 500 tokens (SC-003: approximate by splitting on whitespace and asserting word count < 400)
- [X] T020 [P] [US3] Create `tests/Buildout.IntegrationTests/Mcp/McpPromptTests.cs` — integration tests using `McpSkBridge.CreateHarnessAsync()`: (1) client can list prompts and `update` appears, (2) client requests `prompts/get "update"` and receives detailed update prompt content, (3) server instructions in server metadata are compact (not containing update-specific detail)
- [X] T021 [P] [US3] Update `tests/Buildout.IntegrationTests/Mcp/McpServerMetadataTests.cs` — extend existing test to verify that server instructions come from embedded resources (not hardcoded inline)

### Implementation for User Story 3

- [X] T022 [US3] Create `src/Buildout.Mcp/Prompts/BuildoutPrompts.cs` — `[McpServerPromptType]` class with a `[McpServerPrompt, Description("Detailed instructions for the page update workflow")] UpdateWorkflow()` method returning `new ChatMessage(ChatRole.User, PromptResourceLoader.Load("update"))`
- [X] T023 [US3] Update `src/Buildout.Mcp/Program.cs` — replace hardcoded `options.ServerInstructions` string with `PromptResourceLoader.Load("server-instructions")` loaded from embedded resources; add `.WithPrompts<BuildoutPrompts>()` to the MCP server builder chain
- [X] T024 [US3] Review `src/Buildout.Mcp/Prompts/server-instructions.md` — verify content is compact (< 500 tokens), provides general tool usage guidance, and does not include update-specific detail
- [X] T025 [US3] Review `src/Buildout.Mcp/Prompts/update.md` — verify content provides detailed update workflow instructions suitable for the named MCP prompt

**Checkpoint**: MCP server loads all prompt content from embedded resources, base instructions are compact, update prompt is available on demand

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verify everything works together and clean up.

- [X] T026 Run full test suite: `dotnet test` — verify all unit and integration tests pass
- [X] T027 [P] Run quickstart.md validation — execute each quickstart command and verify expected behavior
- [X] T028 [P] Verify skill files in `src/Buildout.Cli/Skills/` conform to the Agent Skills specification (YAML frontmatter, progressive disclosure structure, file references)
- [X] T029 Verify embedded resource naming — confirm manifest resource names follow `Buildout.Cli.Skills.XXX.md` and `Buildout.Mcp.Prompts.XXX.md` conventions so loaders find them correctly

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **User Stories (Phase 3–5)**: All depend on Phase 2 completion
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 — Install (P1)**: Can start after Phase 2 — no dependencies on other stories
- **US2 — Remove (P2)**: Logically depends on US1 (removes what install creates) but can be developed in parallel since `AgentTarget` and settings are shared infrastructure from Phase 2
- **US3 — MCP Prompts (P1)**: Completely independent of US1/US2 — different project (`Buildout.Mcp` vs `Buildout.Cli`), can start after Phase 2 in parallel

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models/helpers before commands
- Commands before integration tests
- Story complete before moving to Polish

### Parallel Opportunities

- T001 and T002 can run in parallel (different `.csproj` files)
- T003 and T004 can run in parallel (different projects)
- T006 and T007 can run in parallel (different settings files)
- T010 and T011 can run in parallel (different test projects)
- **US1 and US3 can be developed in full parallel** after Phase 2 — different projects, no shared state
- US2 can also be developed in parallel with US1 and US3 (shares only `AgentTarget` from Phase 2)
- T012 and T013 can run in parallel (different test layers)
- T019, T020, T021 can run in parallel (different test files)
- T026, T027, T028, T029 can run in parallel in Polish phase

---

## Parallel Example: After Phase 2 Complete

```bash
# Launch US1 (CLI install) and US3 (MCP prompts) in full parallel:
Task: "T012–T015: Implement install subcommand (US1)"
Task: "T019–T025: Implement MCP embedded prompts (US3)"

# Then US2 (CLI remove) can also run in parallel:
Task: "T016–T018: Implement remove subcommand (US2)"
```

## Parallel Example: Within US1

```bash
# Launch unit + integration tests for US1 together:
Task: "T012: SkillsInstallCommandTests (unit)"
Task: "T013: SkillsCommandTests install scenarios (integration)"
```

---

## Implementation Strategy

### MVP First (US1 + US3)

1. Complete Phase 1: Setup (wire embedded resources)
2. Complete Phase 2: Foundational (settings, loaders, DI registration)
3. Complete Phase 3: US1 — Install subcommand
4. Complete Phase 5: US3 — MCP embedded prompts
5. **STOP and VALIDATE**: Test both stories independently
6. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (Install) + US3 (MCP Prompts) → Test independently → **MVP!**
3. Add US2 (Remove) → Test independently → Complete lifecycle
4. Polish → Full feature complete

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- US1 and US3 are both P1 and form the MVP together
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
