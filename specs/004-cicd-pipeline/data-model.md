# Phase 1 — Data Model: CI/CD Pipeline

This document captures the data shapes that flow through the CI/CD
infrastructure changes. Since this feature is test infrastructure and
CI configuration, no production data models change. The entities
described here are test fixtures, WireMock stubs, and the CI workflow
structure.

## Entities

### `BuildinWireMockFixture` (new — test infrastructure)

Located at `tests/Buildout.IntegrationTests/Buildin/BuildinWireMockFixture.cs`.

```text
public class BuildinWireMockFixture : IDisposable
{
    public WireMockServer Server { get; }
    public string BaseUrl => Server.Url!;
    public IBuildinClient CreateClient();

    public void Dispose() { Server.Dispose(); }
}
```

| Field / Method | Type | Notes |
|---|---|---|
| `Server` | `WireMockServer` | Self-hosted HTTP server started on a random available port. Disposed when the fixture is disposed. |
| `BaseUrl` | `string` | The server's root URL (e.g. `http://localhost:12345`). Used as `HttpClient.BaseAddress`. |
| `CreateClient()` | `IBuildinClient` | Constructs a `BotBuildinClient` with `HttpClient { BaseAddress = BaseUrl }` and `BotTokenAuthenticationProvider("test-token")`. Returns the `IBuildinClient` interface for DI registration. |

**Lifecycle**: xUnit `ICollectionFixture<BuildinWireMockFixture>`.
Server starts once per test collection, stops on disposal. Stub
registration happens in `BuildinStubs.RegisterAll(Server)` called
from the fixture constructor.

### `BuildinStubs` (new — test infrastructure)

Located at `tests/Buildout.IntegrationTests/Buildin/BuildinStubs.cs`.

```text
public static class BuildinStubs
{
    public static void RegisterAll(WireMockServer server);
    public static void RegisterGetMe(WireMockServer server, string? responseBody = null);
    public static void RegisterGetPage(WireMockServer server, string? responseBody = null);
    public static void RegisterGetBlockChildren(WireMockServer server, string? responseBody = null);
    public static void RegisterSearchPages(WireMockServer server, string? responseBody = null);
}
```

| Method | Matching | Default response |
|---|---|---|
| `RegisterGetMe` | `GET /v1/users/me` | JSON matching `UserMe` schema from `openapi.json` |
| `RegisterGetPage` | `GET /v1/pages/*` (wildcard path) | JSON matching `Page` schema |
| `RegisterGetBlockChildren` | `GET /v1/blocks/*/children` (wildcard path) | JSON matching `PaginatedList<Block>` schema |
| `RegisterSearchPages` | `POST /v1/pages/search` | JSON matching `PageSearchResults` schema |
| `RegisterAll` | Calls all four methods | Registers all default stubs |

**Stub design**: Each method registers a WireMock stub using the fluent
API (`Request.Create().WithPath(...).UsingGet/Post()` →
`Response.Create().WithBodyAsJson(...)`). The optional `responseBody`
parameter allows individual tests to override the default response.
Stubs use `WithPath(new RegexMatcher("..."))` for path-parameter
endpoints to match any UUID.

**Verification**: `WireMockContractTests` verifies each stub's default
response deserializes correctly through the Kiota-generated models.

### CI Workflow (new — infrastructure)

Located at `.github/workflows/ci.yml`.

```text
name: CI
on:
  push: { branches: [main] }
  pull_request:

jobs:
  build:
    runs-on: ubuntu-latest
    steps: [checkout, setup-dotnet, dotnet build]

  test-unit:
    needs: build
    runs-on: ubuntu-latest
    steps: [checkout, setup-dotnet, dotnet test Buildout.UnitTests]

  test-integration:
    needs: build
    runs-on: ubuntu-latest
    env: { OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }} }
    steps: [checkout, setup-dotnet, dotnet test Buildout.IntegrationTests]

  publish:
    needs: [test-unit, test-integration]
    runs-on: ubuntu-latest
    steps: [checkout, setup-dotnet, dotnet publish × 2, upload-artifact × 2]
```

| Job | Dependencies | Purpose |
|---|---|---|
| `build` | none | Compile solution, fail fast on errors |
| `test-unit` | `build` | Run `Buildout.UnitTests` |
| `test-integration` | `build` | Run `Buildout.IntegrationTests` (with `OPENROUTER_API_KEY`) |
| `publish` | `test-unit` + `test-integration` | Publish framework-dependent single-file MCP + CLI, upload as artifacts |

### Semantic Kernel test configuration (new — test infrastructure)

Not a persistent entity; constructed per-test in `PageReadingLlmTests`.

```text
var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://openrouter.ai/api/v1")
};
httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/buildout");

var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(
        modelId: "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
        apiKey: openRouterApiKey,
        httpClient: httpClient)
    .Build();
```

| Configuration | Value | Notes |
|---|---|---|
| `modelId` | `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free` | OpenRouter free-tier model specified by user |
| `BaseAddress` | `https://openrouter.ai/api/v1` | OpenRouter's OpenAI-compatible endpoint |
| `apiKey` | From `OPENROUTER_API_KEY` env var | Skip test if absent |
| `FunctionChoiceBehavior` | `Auto()` | Enables automatic tool calling |

## Relationships

```text
BuildinWireMockFixture
  ├── owns WireMockServer
  ├── calls BuildinStubs.RegisterAll()
  └── provides IBuildinClient (via CreateClient)
        └── used by GetCommandTests, PageReadingLlmTests,
            MockedHttpHarnessTests, WireMockContractTests

CI Workflow
  ├── build → test-unit (parallel)
  ├── build → test-integration (parallel)
  └── test-unit + test-integration → publish

Semantic Kernel (per-test)
  └── configured with OpenRouter endpoint
        └── registers MCP tool functions as SK plugins
              └── auto-invoked during LLM test
```

## Existing Entities — Changes

| Entity | Change | Notes |
|---|---|---|
| `MockedHttpHarnessTests` | REWRITTEN | Remove `MockHttpHandler`, use `BuildinWireMockFixture` |
| `GetCommandTests` | MODIFIED | Remove `Substitute.For<IBuildinClient>()`, inject `fixture.CreateClient()` |
| `PageReadingLlmTests` | MODIFIED | Remove `Substitute.For<IBuildinClient>()` + `AnthropicClient`, use `BuildinWireMockFixture` + Semantic Kernel |
| `SearchToolTests` | UNCHANGED | Mocks `ISearchService`, not `IBuildinClient` |
| `Buildout.IntegrationTests.csproj` | MODIFIED | Add `WireMock.Server`, `Microsoft.SemanticKernel`, `Microsoft.SemanticKernel.Connectors.OpenAI`; remove `Anthropic.SDK` |
