# Navigation Contract: Buildout.AdminUI

**Feature**: 016-admin-ui-skeleton  
**Date**: 2026-06-01

---

## Routes

| Route | Component | Behaviour |
|-------|-----------|-----------|
| `/` | `Home` (redirect) | Redirects to `/keys` |
| `/keys` | `KeysPage` | Renders the Keys Management tab as the active tab |
| `/audit` | `AuditPage` | Renders the Audit Logs tab as the active tab |

Navigating directly to `/keys` or `/audit` MUST activate the corresponding tab without a full-page reload if the layout is already rendered.

---

## Tab Layout

The shell layout (`MainLayout`) renders a two-tab navigation bar at the top of the content area. Both tabs are always visible; the active tab's panel content is rendered below.

| Tab Label | Route Link | Active When |
|-----------|-----------|-------------|
| Keys Management | `/keys` | Current URL is `/` or `/keys` |
| Audit Logs | `/audit` | Current URL is `/audit` |

---

## HTTP Endpoints

The AdminUI is a Blazor Server app. No JSON REST endpoints are exposed; all data is served via Blazor component rendering over the SignalR circuit. The app binds to `http://localhost:5200` in development (configurable via `ASPNETCORE_URLS`).

---

## Error Pages

| Scenario | Behaviour |
|----------|-----------|
| Unknown route | ASP.NET Core default 404 page |
| Unhandled exception in component | Blazor error boundary renders a user-facing error message; circuit is maintained |
