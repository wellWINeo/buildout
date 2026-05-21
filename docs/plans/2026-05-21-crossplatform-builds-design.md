# Cross-Platform Builds Design

**Date**: 2026-05-21
**Branch**: `feature/crossplatform-builds`

## Overview

Add CI workflow to build and publish Buildout artifacts for multiple platforms and formats:
- Platforms: `linux-x64`, `linux-arm64`, `osx-arm64`
- Formats: single-file self-contained executables, framework-dependent dlls (FDD)

## CI Workflow Structure

The workflow replaces the existing single-job `publish` with a 3-stage pipeline.

### Stage 1: Build (3 matrix jobs)

| Job | Runner | RID |
|-----|--------|-----|
| `build-linux-x64` | `ubuntu-latest` | `linux-x64` |
| `build-linux-arm64` | `ubuntu-latest` | `linux-arm64` |
| `build-macos-arm64` | `macos-latest` | `osx-arm64` |

Each job:
- Checks out code
- Sets up .NET 10 SDK
- Runs `dotnet build -c Release`
- Caches NuGet packages and `obj/` outputs for downstream jobs to avoid clean rebuilds

### Stage 2: Tests (4 jobs, parallel)

**Unit Tests** (3 matrix jobs, one per RID):
- Depends on corresponding build job
- Runs `dotnet test tests/Buildout.UnitTests`

**Integration Tests** (1 job):
- Runs on `linux-x64` runner
- Depends on `build-linux-x64`
- Runs `dotnet test tests/Buildout.IntegrationTests`
- Requires `OPENROUTER_API_KEY` secret

### Stage 3: Publish (4 jobs)

**Single-File Executables** (3 jobs, one per RID):
- Depends on corresponding unit test job
- Runs for both `Buildout.Cli` and `Buildout.Mcp`:
  ```bash
  dotnet publish src/Buildout.Cli -c Release -r <rid> --self-contained -p:PublishSingleFile=true
  dotnet publish src/Buildout.Mcp -c Release -r <rid> --self-contained -p:PublishSingleFile=true
  ```

**Framework-Dependent DLLs** (1 job):
- Depends on `test-unit-linux-x64` (arbitrary RID pick for dependency chain)
- Runs for both `Buildout.Cli` and `Buildout.Mcp`:
  ```bash
  dotnet publish src/Buildout.Cli -c Release
  dotnet publish src/Buildout.Mcp -c Release
  ```
- No RID specified, no `--self-contained` flag
- Produces framework-dependent dlls requiring .NET 10 runtime

### Artifacts

8 artifacts total:

**Single-file (self-contained):**
- `buildout-cli-linux-x64`
- `buildout-mcp-linux-x64`
- `buildout-cli-linux-arm64`
- `buildout-mcp-linux-arm64`
- `buildout-cli-macos-arm64`
- `buildout-mcp-macos-arm64`

**Framework-dependent (FDD):**
- `buildout-cli-fdd`
- `buildout-mcp-fdd`

## README.md

Create a brief README with the following sections:

1. **Header**: Project name and one-line description
2. **Installation**:
   - Prerequisites (.NET 10 SDK for FDD, none for single-file)
   - Download links pointing to latest GitHub Actions artifact (placeholder until proper release)
   - Manual install instructions: download, `chmod +x`, move to PATH
3. **Usage**: Brief examples for CLI and MCP
4. **Development**: Build, test, and publish commands for local development
5. **License**: Placeholder (to be determined)

## Files Changed

1. `.github/workflows/ci.yml` — complete rewrite with 3-stage pipeline
2. `README.md` — new file

## Design Decisions

- **FDD over FDE**: Framework-dependent deployments (pure dlls) chosen for simplicity and cross-platform compatibility
- **CI-only build logic**: No reusable scripts — all logic inline in CI workflow per user preference
- **RID as CLI argument**: RuntimeIdentifier passed via `-r` flag, not specified in .csproj files
- **Approach B workflow**: Build/test per RID, then publish matrix, to avoid duplicated compilation despite added complexity
- **Caching**: Build artifacts cached between stages to avoid clean rebuilds in publish stage
- **Integration tests once**: Only run integration tests on `linux-x64`, parallel with unit tests on other RIDs

## Future Work

- Once releases are created, update README with stable download links instead of CI artifact links
- Consider adding auto-generated CHANGELOG to releases
- Add release notes for each platform artifact (optional)