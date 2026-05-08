# Quickstart — CI/CD Pipeline

How to verify this feature locally once implemented.

## Prerequisites

- .NET 10 SDK installed (already required by features 001–003).
- For LLM tests: `export OPENROUTER_API_KEY=<your-key>`.
  Without this key, LLM tests are skipped; all other tests still pass.

## Verify WireMock integration tests

```bash
dotnet test tests/Buildout.IntegrationTests
```

Expected: all tests green. No network calls to buildin.ai occur.
WireMock server starts and stops per test collection automatically.

With `OPENROUTER_API_KEY` set, the Semantic Kernel LLM test runs and
asserts the OpenRouter model invokes MCP tools successfully. Without
the key, that single test is skipped.

## Verify contract tests

```bash
dotnet test tests/Buildout.IntegrationTests --filter "WireMockContract"
```

These verify WireMock stub responses deserialize through Kiota-generated
models. If `openapi.json` changes without updating stubs, these fail.

## Verify the CI workflow locally

```bash
# Simulate the build job
dotnet build --configuration Release

# Simulate the test-unit job
dotnet test tests/Buildout.UnitTests --configuration Release

# Simulate the test-integration job
dotnet test tests/Buildout.IntegrationTests --configuration Release

# Simulate the publish job
dotnet publish src/Buildout.Mcp -c Release -p:PublishSingleFile=true -o /tmp/buildout-mcp
dotnet publish src/Buildout.Cli -c Release -p:PublishSingleFile=true -o /tmp/buildout-cli

# Verify the published executables run
/tmp/buildout-mcp/Buildout.Mcp --help || true
/tmp/buildout-cli/Buildout.Cli --help || true
```

## Verify the GitHub Actions workflow

After merging, push a commit to a PR branch and check:

1. The `build` job completes successfully.
2. `test-unit` and `test-integration` run in parallel.
3. `publish` runs only after both test jobs pass.
4. Artifacts (`buildout-mcp`, `buildout-cli`) are downloadable from
   the workflow run.

## Definition of done

- [ ] `dotnet test` passes (all projects).
- [ ] No `MockHttpHandler` class remains in `Buildout.IntegrationTests`.
- [ ] No `Substitute.For<IBuildinClient>()` remains in
      `Buildout.IntegrationTests` (except `SearchToolTests` which mocks
      `ISearchService`).
- [ ] No `Anthropic.SDK` reference in any `.csproj`.
- [ ] WireMock contract tests pass for all four stubbed endpoints.
- [ ] Semantic Kernel LLM test passes with `OPENROUTER_API_KEY` set.
- [ ] Semantic Kernel LLM test skips without `OPENROUTER_API_KEY`.
- [ ] `.github/workflows/ci.yml` triggers on push to `main` and
      `pull_request`.
- [ ] Unit and integration test jobs run in parallel.
- [ ] Published artifacts are downloadable from a successful workflow run.
- [ ] Publish job does not run when any test fails.
