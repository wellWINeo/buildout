# Research: Management UI Skeleton

**Feature**: 016-admin-ui-skeleton  
**Date**: 2026-06-01

---

## Decision 1: Blazor Hosting Model

**Decision**: Blazor Server (ASP.NET Core interactive server rendering)

**Rationale**: Server-side rendering keeps the startup ceremony minimal — a single `dotnet run` with no WASM toolchain or publish step. SignalR circuit interactivity is sufficient for two tabs and an in-memory data layer. The server model also makes bUnit tests straightforward: components resolve DI services the same way as production code.

**Alternatives considered**:
- *Blazor WebAssembly* — works offline, but adds WASM publish complexity and a separate API host for data; unnecessary for a mocked-data skeleton.
- *Blazor Static SSR* — no interactivity without a circuit; tab-switching without a reload requires interactive rendering.

---

## Decision 2: UI Component Library

**Decision**: MudBlazor v8 (latest stable, MIT license)

**Rationale**: `MudTabs` / `MudTabPanel` provide accessible, animated tab navigation with minimal boilerplate. MudBlazor is the most widely adopted Blazor component library, Material Design-aligned, and has first-class .NET 10 / Blazor 10 support. `MudDataGrid` covers the key and log listing columns without custom CSS.

**Alternatives considered**:
- *Radzen Blazor* — enterprise-tier components, heavier bundle, subscription required for some features.
- *Blazor Bootstrap* — Bootstrap-based; `<Tabs>` component exists but less feature-complete; mixing Bootstrap + MudBlazor creates CSS conflicts.
- *Vanilla Blazor + CSS* — zero dependencies, but hand-rolling responsive tables and tabs adds scope to a skeleton task.

---

## Decision 3: Mock Data Pattern

**Decision**: Singleton in-memory services registered via ASP.NET Core DI (`IApiKeyService`, `IAuditLogService`), each returning a hardcoded list of domain objects.

**Rationale**: DI-injected services let components remain unaware of the data source — the same component can be wired to real services later by swapping the registration. Singleton scope means data is stable across tab switches. Simple record types carry the mock data; no JSON files or external configuration needed.

**Alternatives considered**:
- *Static class with hardcoded lists* — simpler, but not injectable; harder to test and harder to swap.
- *JSON fixture files* — adds file I/O and parsing; unnecessary complexity for a skeleton.

---

## Decision 4: Testing Framework

**Decision**: bUnit 2.x with xUnit v3 (matching existing `xunit.v3` 3.2.2 dependency in `Buildout.UnitTests`)

**Rationale**: bUnit is the de facto standard for Blazor component tests. It integrates with xUnit v3, matches the project's existing test runner, and supports the same NSubstitute mocking patterns already used. Component tests verify that both tab panels render, columns appear, and mock rows are visible — covering the acceptance scenarios without a real browser.

**Alternatives considered**:
- *Playwright / Selenium* — end-to-end; excessive for a skeleton with mocked data and no routing complexity.
- *MSTest* — not used anywhere in the repo; inconsistency penalty outweighs any benefit.

---

## Decision 5: Project Layout

**Decision**: `src/Buildout.AdminUI/` (Blazor Server web app) + `tests/Buildout.AdminUITests/` (bUnit component tests), both added to `buildout.slnx`.

**Rationale**: Mirrors the existing `src/` and `tests/` folder convention. `Buildout.AdminUI` follows the established `Buildout.*` project naming. A dedicated test project keeps AdminUI tests isolated from `Buildout.UnitTests` (which targets Core, CLI, MCP).

**Alternatives considered**:
- *Add Blazor tests to `Buildout.UnitTests`* — adds bUnit dependency to a project that currently has no Blazor references; couples unrelated test concerns.

---

## Resolved Clarifications

| # | Item | Resolution |
|---|------|-----------|
| 1 | Blazor hosting model | Blazor Server |
| 2 | UI component library | MudBlazor v8 |
| 3 | Mock data delivery | Singleton DI services |
| 4 | Test framework | bUnit + xUnit v3 |
| 5 | Project placement | `src/Buildout.AdminUI/`, `tests/Buildout.AdminUITests/` |
