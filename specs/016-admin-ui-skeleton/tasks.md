---

description: "Task list for Management UI Skeleton implementation"
---

# Tasks: Management UI Skeleton

**Input**: Design documents from `specs/016-admin-ui-skeleton/`
**Prerequisites**: plan.md ✅ spec.md ✅ data-model.md ✅ contracts/routes.md ✅ quickstart.md ✅

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). bUnit component tests are written in `tests/Buildout.AdminUITests/` before the components they exercise. Test tasks are listed first within each user story phase.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Project Scaffold)

**Purpose**: Create the two new projects and wire them into the solution. No user story work can begin before both projects compile.

- [ ] T001 Create `src/Buildout.AdminUI/Buildout.AdminUI.csproj` as a Blazor Server web app targeting net10.0 with MudBlazor v8, nullable enabled, warnings-as-errors
- [ ] T002 Create `tests/Buildout.AdminUITests/Buildout.AdminUITests.csproj` referencing xunit.v3 3.2.2, bUnit 2.x, and the AdminUI project; nullable enabled, warnings-as-errors
- [ ] T003 [P] Add both projects to `Buildout.sln` via `dotnet sln add`
- [ ] T004 [P] Add `src/Buildout.AdminUI/wwwroot/.gitkeep` placeholder and confirm `dotnet build src/Buildout.AdminUI` and `dotnet build tests/Buildout.AdminUITests` both succeed with zero warnings

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain models, service interfaces, mock implementations, DI registration, and Blazor app bootstrap. All user story phases depend on this phase completing first.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T005 Create `src/Buildout.AdminUI/Models/ApiKeyStatus.cs` with `enum ApiKeyStatus { Active, Revoked }`
- [ ] T006 [P] Create `src/Buildout.AdminUI/Models/ApiKey.cs` with fields: `Guid Id`, `string Name`, `ApiKeyStatus Status`, `DateTimeOffset CreatedAt`, `DateTimeOffset? LastUsedAt`
- [ ] T007 [P] Create `src/Buildout.AdminUI/Models/AuditLogEntry.cs` with fields: `Guid Id`, `string Actor`, `string Action`, `string Resource`, `DateTimeOffset Timestamp`, `string? Details`
- [ ] T008 Create `src/Buildout.AdminUI/Services/IApiKeyService.cs` with `IReadOnlyList<ApiKey> GetAll()`
- [ ] T009 [P] Create `src/Buildout.AdminUI/Services/IAuditLogService.cs` with `IReadOnlyList<AuditLogEntry> GetAll()`
- [ ] T010 Create `src/Buildout.AdminUI/Services/MockApiKeyService.cs` implementing `IApiKeyService`; return ≥5 hardcoded `ApiKey` records covering both `Active` and `Revoked` statuses with varied `CreatedAt` dates
- [ ] T011 [P] Create `src/Buildout.AdminUI/Services/MockAuditLogService.cs` implementing `IAuditLogService`; return ≥10 hardcoded `AuditLogEntry` records spanning the past 30 days with several distinct `Action` values
- [ ] T012 Create `src/Buildout.AdminUI/Components/App.razor` and `src/Buildout.AdminUI/Components/Routes.razor` with standard Blazor Server bootstrapping
- [ ] T013 Create `src/Buildout.AdminUI/Program.cs` registering MudBlazor services, `MockApiKeyService` as `IApiKeyService` (Singleton), and `MockAuditLogService` as `IAuditLogService` (Singleton); set `builder.WebHost.UseUrls("http://localhost:5200")` as the default binding, overridable by the `ASPNETCORE_URLS` environment variable
- [ ] T014 [P] Create `src/Buildout.AdminUI/Components/_Imports.razor` with `@using` statements for MudBlazor, the project's Models and Services namespaces

**Checkpoint**: Foundation ready — `dotnet build` clean, services registered, no user story implementation yet.

---

## Phase 3: User Story 1 — Browse the Admin UI (Priority: P1) 🎯 MVP

**Goal**: A navigable two-tab shell. Switching between tabs renders distinct content without a full-page reload. Direct URL navigation to `/keys` or `/audit` activates the correct tab.

**Independent Test**: Open `http://localhost:5200`; verify two tabs "Keys Management" and "Audit Logs" are visible and switching between them works.

### Tests for User Story 1

> **Write these tests FIRST — they must FAIL before component implementation.**

- [ ] T015 [US1] Write bUnit tests in `tests/Buildout.AdminUITests/NavigationTests.cs` verifying: (a) MainLayout renders exactly two MudTab items labelled "Keys Management" and "Audit Logs"; (b) navigating to `/keys` makes the Keys tab active — use `ctx.Services.GetRequiredService<FakeNavigationManager>().NavigateTo("/keys")` and assert the rendered markup shows the Keys tab as active; (c) navigating to `/audit` makes the Audit Logs tab active using the same approach

### Implementation for User Story 1

- [ ] T016 [US1] Create `src/Buildout.AdminUI/Components/Layout/MainLayout.razor` with a `MudLayout` shell containing a `MudTabs` bar with two tabs — "Keys Management" linking to `/keys` and "Audit Logs" linking to `/audit`; use `@Body` to render the active tab content
- [ ] T017 [US1] Create `src/Buildout.AdminUI/Components/Pages/Home.razor` with `@page "/"` that redirects to `/keys` on `OnInitialized` using `NavigationManager`
- [ ] T018 [US1] Update `src/Buildout.AdminUI/Components/Routes.razor` to set `DefaultLayout` pointing at `MainLayout`; confirm `@page "/"` is in `Home.razor`, `@page "/keys"` is in `KeysPage.razor`, and `@page "/audit"` is in `AuditPage.razor` (Blazor Server routes are declared via `@page` directives on each component, not in `Routes.razor`)

**Checkpoint**: US1 complete — two-tab navigation shell is functional and tested.

---

## Phase 4: User Story 2 — View Keys Management Tab (Priority: P2)

**Goal**: The Keys Management tab renders a table of mock API keys with name, status, and creation date columns. An empty-state message is shown when the list has no entries.

**Independent Test**: Navigate to `http://localhost:5200/keys`; verify a table with at least name, status, and creation date columns is rendered with ≥5 mock rows.

### Tests for User Story 2

> **Write these tests FIRST — they must FAIL before component implementation.**

- [ ] T019 [US2] Write bUnit tests in `tests/Buildout.AdminUITests/KeysPageTests.cs` verifying: (a) `KeysPage` renders a `MudDataGrid` with columns Name, Status, and Creation Date; (b) all mock keys returned by `IApiKeyService.GetAll()` appear as rows; (c) status values display as "Active" or "Revoked"; (d) when the service returns an empty list, the text "No API keys found." is shown instead of an empty table

### Implementation for User Story 2

- [ ] T020 [US2] Create `src/Buildout.AdminUI/Components/Pages/KeysPage.razor` with `@page "/keys"` injecting `IApiKeyService`; render a `MudDataGrid` with columns for Name, Status, and Creation Date; render a `MudText` with content "No API keys found." when the list is empty

**Checkpoint**: US2 complete — Keys Management tab shows mock data; independently testable without Audit Logs.

---

## Phase 5: User Story 3 — View Audit Logs Tab (Priority: P3)

**Goal**: The Audit Logs tab renders a chronological (newest-first) table of mock audit log entries with actor, action, affected resource, and timestamp columns. An empty-state message is shown when the list has no entries.

**Independent Test**: Navigate to `http://localhost:5200/audit`; verify a table with at least actor, action, resource, and timestamp columns is rendered with ≥10 mock rows in descending timestamp order.

### Tests for User Story 3

> **Write these tests FIRST — they must FAIL before component implementation.**

- [ ] T021 [US3] Write bUnit tests in `tests/Buildout.AdminUITests/AuditPageTests.cs` verifying: (a) `AuditPage` renders a `MudDataGrid` with columns Actor, Action, Resource, and Timestamp; (b) all mock entries returned by `IAuditLogService.GetAll()` appear as rows; (c) entries are ordered by Timestamp descending (newest first); (d) when the service returns an empty list, the text "No audit log entries found." is shown instead of an empty table

### Implementation for User Story 3

- [ ] T022 [US3] Create `src/Buildout.AdminUI/Components/Pages/AuditPage.razor` with `@page "/audit"` injecting `IAuditLogService`; render a `MudDataGrid` ordered by `Timestamp` descending with columns for Actor, Action, Resource, and Timestamp; render a `MudText` with content "No audit log entries found." when the list is empty

**Checkpoint**: All three user stories complete — both tabs show mock data; each independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Error boundaries, viewport conformance, and quickstart validation.

- [ ] T023 [P] Add a Blazor `<ErrorBoundary>` wrapper around the `<Router>` in `src/Buildout.AdminUI/Components/App.razor` so unhandled component exceptions render a user-facing error message without dropping the SignalR circuit
- [ ] T024 [P] Confirm all mandatory columns (name, status, creation date for keys; actor, action, resource, timestamp for logs) are visible without horizontal scrolling at 1280 px viewport width; adjust `MudDataGrid` column widths if needed in `KeysPage.razor` and `AuditPage.razor`
- [ ] T025 Run quickstart validation: `dotnet run --project src/Buildout.AdminUI`, navigate to `http://localhost:5200/`, `/keys`, and `/audit`; confirm each loads with mock data and tab switching requires no full-page reload; run `dotnet test tests/Buildout.AdminUITests` and confirm all tests pass
- [ ] T026 [P] Run `dotnet test tests/Buildout.UnitTests tests/Buildout.IntegrationTests` and confirm all existing tests remain green (constitution merge gate — existing suites must not regress)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **User Story Phases (3–5)**: All depend on Phase 2 completion; stories are otherwise independent and may proceed in parallel
- **Polish (Phase 6)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 — no dependency on US2 or US3
- **US2 (P2)**: Can start after Phase 2 — no dependency on US1 or US3
- **US3 (P3)**: Can start after Phase 2 — no dependency on US1 or US2

### Within Each User Story

- Tests MUST be written and confirmed failing before component implementation
- Service/model dependencies (Phase 2) before components
- Component implementation after its test exists and fails

### Parallel Opportunities

- T005–T007: Models can be written in parallel (different files)
- T008–T009: Service interfaces can be written in parallel
- T010–T011: Mock implementations can be written in parallel
- T006, T007, T009, T011, T014: All marked [P] within their phase
- US1, US2, US3 can be worked in parallel by different developers once Phase 2 is done

---

## Parallel Example: Phase 2

```bash
# These model and service files can be written simultaneously:
Task: "Create ApiKey.cs"
Task: "Create AuditLogEntry.cs"
Task: "Create IAuditLogService.cs"
Task: "Create MockAuditLogService.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Project scaffold
2. Complete Phase 2: Foundational models, services, DI, bootstrap
3. Complete Phase 3: US1 — two-tab shell
4. **STOP and VALIDATE**: two tabs visible, navigation works, tests green
5. Proceed to US2 and US3

### Incremental Delivery

1. Phase 1 + 2 → compilable scaffold with DI
2. Phase 3 → navigable shell (MVP, demoable)
3. Phase 4 → Keys tab shows data
4. Phase 5 → Audit tab shows data
5. Phase 6 → polish, validated quickstart

### Parallel Team Strategy

With two developers after Phase 2 is done:

- Developer A: US1 (navigation shell) → then US3 (Audit Logs)
- Developer B: US2 (Keys Management) → then Polish

---

## Notes

- **Test-first is non-negotiable** (Principle IV): each story's test tasks (T015, T019, T021) must be written and confirmed failing before the corresponding component task
- `[P]` tasks touch different files and have no dependency on sibling incomplete tasks — safe to run concurrently
- `[Story]` label maps each task to the acceptance scenarios in `spec.md`
- Empty-state behaviour (FR-006) is covered in both US2 and US3 test tasks
- Direct URL navigation (FR-008, edge case) is covered in the US1 navigation tests (T015)
- Port `5200` is configured via `ASPNETCORE_URLS` in `Program.cs` and documented in `quickstart.md`
- The `NavMenu.razor` stub in the plan's layout structure is not needed — MudTabs in `MainLayout` serves the same navigation role
