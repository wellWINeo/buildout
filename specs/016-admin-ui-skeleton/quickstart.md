# Quickstart: Buildout.AdminUI

## Prerequisites

- .NET 10 SDK
- No additional tooling, API keys, or external services required

## Run

```bash
dotnet run --project src/Buildout.AdminUI
```

The app starts at **http://localhost:5200** by default. Open that URL in a browser; you will land on the Keys Management tab.

## Navigate

| URL | Content |
|-----|---------|
| http://localhost:5200/ | Redirects to Keys Management |
| http://localhost:5200/keys | Keys Management tab — table of mock API keys |
| http://localhost:5200/audit | Audit Logs tab — table of mock audit events |

## Run Tests

```bash
dotnet test tests/Buildout.AdminUITests
```

Tests use bUnit and run entirely in-process with no browser or network required.

## Run All Tests

```bash
dotnet test
```

The AdminUI test project is part of the solution and runs alongside the existing unit and integration test projects.
