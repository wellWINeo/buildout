# Implementation Plan: Management UI Skeleton

**Branch**: `016-admin-ui-skeleton` | **Date**: 2026-06-01 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/016-admin-ui-skeleton/spec.md`

## Summary

Add a standalone `Buildout.AdminUI` Blazor Server web application with two tab views — Keys Management and Audit Logs — populated entirely by hardcoded in-memory mock data. No real backend integration; the goal is a navigable, visually complete skeleton that validates the information layout before live data is wired in.

## Technical Context

**Language/Version**: C# 13 / .NET 10  
**Primary Dependencies**: ASP.NET Core 10, Blazor Server (interactive server rendering), MudBlazor v8  
**Storage**: N/A — in-memory mock data only  
**Testing**: bUnit 2.x + xUnit v3 (matching existing `xunit.v3` 3.2.2 in the repo)  
**Target Platform**: Web browser — desktop viewport (1280 px+)  
**Project Type**: Blazor Server web application  
**Performance Goals**: Tab switch < 500 ms; initial page load < 2 s  
**Constraints**: No authentication required; no external API calls; mocked data only  
**Scale/Scope**: Single admin user; 2 tab views; ≥5 mock API keys; ≥10 mock audit log entries

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I — Core/Presentation Separation | ✅ Pass | AdminUI uses no `Buildout.Core` internals; mocked services are self-contained |
| II — LLM-Friendly Output Fidelity | ✅ N/A | No Markdown conversion |
| III — Bidirectional Round-Trip Testing | ✅ N/A | No block conversion |
| IV — Test-First Discipline | ✅ Pass | bUnit tests written before component implementation |
| V — Buildin API Abstraction | ✅ N/A | No buildin.ai API calls |
| VI — Non-Destructive Editing | ✅ N/A | No editing operations |
| VII — Dual-Channel Configuration | ✅ Pass | No user-facing configurable options in the skeleton; dev port documented in quickstart only |
| VIII — Skills & Prompts Parity | ✅ N/A | Not a CLI command or MCP tool |
| **Solution layout** (load-bearing) | ⚠️ **VIOLATION** | Adding `src/Buildout.AdminUI/` and `tests/Buildout.AdminUITests/` changes the constitutionally load-bearing layout. See Complexity Tracking. |
| **Out of scope** clause | ⚠️ **VIOLATION** | Constitution lists "admin dashboard" as out of scope; requires a constitution amendment. See Complexity Tracking. |

> **NOTE — Gate violations present.** The two violations below are documented in the Complexity Tracking table. A constitution amendment (MINOR) is the clean path; alternatively, the violations may be accepted via documented justification in the PR review. Implementation MUST NOT begin until the PR author and reviewer acknowledge the Complexity Tracking entries.

## Project Structure

### Documentation (this feature)

```text
specs/016-admin-ui-skeleton/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── routes.md        # Navigation / route contract
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
  Buildout.AdminUI/              # Blazor Server web application (NEW)
    Components/
      Layout/
        MainLayout.razor         # Shell with MudTabs navigation
      Pages/
        Home.razor               # Redirect to /keys
        KeysPage.razor           # Keys Management tab content
        AuditPage.razor          # Audit Logs tab content
      App.razor
      Routes.razor
    Models/
      ApiKey.cs
      ApiKeyStatus.cs
      AuditLogEntry.cs
    Services/
      IApiKeyService.cs
      IAuditLogService.cs
      MockApiKeyService.cs
      MockAuditLogService.cs
    wwwroot/
    Program.cs
    Buildout.AdminUI.csproj
tests/
  Buildout.AdminUITests/         # bUnit component tests (NEW)
    NavigationTests.cs
    KeysPageTests.cs
    AuditPageTests.cs
    Buildout.AdminUITests.csproj
```

**Structure Decision**: Option 2 variant — standalone web application separate from the existing CLI/MCP/Core structure. The `Buildout.AdminUI` project lives under `src/` following the established naming and folder convention. The test project lives under `tests/` and mirrors the existing unit/integration project layout.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| 5th project added (`Buildout.AdminUI`) — solution layout is constitutionally load-bearing | Admin UI is a distinct presentation surface (web browser) that cannot be folded into CLI or MCP without violating Principle I. The feature explicitly requires a separate project. | Using an existing project (e.g., Buildout.Mcp) as the admin host would couple a transport-specific project to browser rendering concerns — a direct Principle I violation. |
| "Admin dashboard" listed as out of scope | This skeleton delivers exactly the UI foundations described in the constitution's out-of-scope clause. A constitution amendment was the documented path; the user has chosen to proceed under documented violation instead. The skeleton scopes strictly to read-only mocked views with no backend, minimising architectural footprint. | Deferring the feature is the only alternative, which contradicts the user's explicit intent. |
