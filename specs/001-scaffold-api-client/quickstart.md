# Quickstart: Scaffold + Buildin API Client

This is the script a new contributor follows to validate the feature works on
their machine. Following these steps exactly should reproduce success
criteria SC-001, SC-003, and SC-006.

## Prerequisites

- **.NET SDK 10.0+** (`dotnet --version` reports `10.x` or later)
- **bash** (macOS / Linux) or **PowerShell 7+** (Windows)
- **git**

No buildin.ai account, API token, or network access to `api.buildin.ai` is
required to build or run tests.

## Steps

```bash
# 1. Clone
git clone <repo-url>
cd buildout

# 2. Restore the Kiota local tool (one-time per checkout)
dotnet tool restore

# 3. Build the solution
dotnet build buildout.slnx

# 4. Run all tests (unit + integration)
dotnet test buildout.slnx
```

Expected outcomes:

- Step 2 completes in seconds.
- Step 3 builds **all five projects** (`Buildout.Core`, `Buildout.Mcp`,
  `Buildout.Cli`, `Buildout.UnitTests`, `Buildout.IntegrationTests`) green.
- Step 4 reports **zero failures** across both test projects, in well under
  60 seconds on a typical developer laptop.

## Optional: regenerate the buildin client

Only needed when `openapi.json` changes:

```bash
# macOS / Linux
./scripts/regenerate-buildin-client.sh

# Windows
./scripts/regenerate-buildin-client.ps1
```

After running with no changes to `openapi.json`, `git status` MUST be clean.

## Verifying constitutional invariants

These manual checks correspond to the spec's success criteria:

| Check | Command | Pass condition |
|---|---|---|
| Five projects only (SC-002 layout) | `dotnet sln buildout.slnx list` | Lists exactly the five constitution projects. |
| No outbound to api.buildin.ai (SC-006) | run tests with the network blocked (e.g. firewall rule, `--network none` in a container) | Test suite still green. |
| Regen idempotent (SC-004) | `./scripts/regenerate-buildin-client.sh && git status` | Reports clean working tree. |
| Generated diffs contained (SC-005) | edit `openapi.json` (e.g. add a description), regenerate, then `git status` | Only files inside `src/Buildout.Core/Buildin/Generated/` plus `openapi.json` are modified. |
| Bot token not required for tests (FR-008) | `unset BUILDOUT__BUILDIN__BOTTOKEN; dotnet test` | Test suite still green. |

## Where things live

| If you want to … | … look here |
|---|---|
| Call buildin from a downstream feature | `src/Buildout.Core/Buildin/IBuildinClient.cs` |
| Add a new domain model | `src/Buildout.Core/Buildin/Models/` |
| Understand error semantics | `src/Buildout.Core/Buildin/Errors/` |
| See what Kiota generated | `src/Buildout.Core/Buildin/Generated/` (do **not** hand-edit) |
| Configure the client at host startup | `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` |
| Mock the client in a test | `tests/Buildout.UnitTests/Buildin/` (NSubstitute on `IRequestAdapter`) |
| Run the full HTTP pipeline against canned responses | `tests/Buildout.IntegrationTests/Buildin/` |
| Regenerate the client | `scripts/regenerate-buildin-client.sh` |
