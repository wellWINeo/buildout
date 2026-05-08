# Phase 0 — Research: CI/CD Pipeline

This document records the technical decisions resolved before
implementation begins. Every item from the Technical Context and
Constitution Check is addressed here so `/speckit-tasks` has no
ambiguity.

## R1 — WireMock.NET fixture design

**Decision**: Use **WireMock.NET** (`WireMock.Server` NuGet package)
as a self-hosted HTTP server in integration tests. A shared
`BuildinWireMockFixture` class starts `WireMockServer` once per test
collection, registers manual stubs for every buildin endpoint exercised
by the test suite, and exposes the server's base URL so that a real
`BotBuildinClient` can be wired to it via `HttpClient`.

**Rationale**:

- WireMock operates at the HTTP level, which is the same level the
  Kiota-generated client operates at. This catches serialisation,
  header, content-negotiation, and URL-construction bugs that
  interface-level (NSubstitute) mocks cannot.
- The existing `MockHttpHandler` (custom `DelegatingHandler`) mocks at
  the HTTP level too but is limited to one fixed response per test and
  cannot verify incoming request shape. WireMock provides request
  journaling, path/method matching, and flexible response templating.
- Running a real HTTP server means `BotBuildinClient` exercises its
  full Kiota `HttpClientRequestAdapter` pipeline — authentication
  headers, serialisation, deserialization — all verified.
- WireMock.NET does not natively parse OpenAPI specs into stubs. Stubs
  are defined manually in C# code but are verified against `openapi.json`
  by contract tests (SC-007).

**Stubs required** (matching `openapi.json` endpoints):

| Endpoint | Method | Used by tests |
|---|---|---|
| `POST /v1/pages/search` | POST | `MockedHttpHarnessTests`, `PageReadingLlmTests` |
| `GET /v1/pages/{page_id}` | GET | `MockedHttpHarnessTests`, `GetCommandTests`, `PageReadingLlmTests` |
| `GET /v1/blocks/{block_id}/children` | GET | `GetCommandTests`, `PageReadingLlmTests` |
| `GET /v1/users/me` | GET | `MockedHttpHarnessTests` |

**Fixture lifecycle**:

- xUnit `ICollectionFixture<BuildinWireMockFixture>` — server starts
  once per test collection, stops on disposal. Stubs are registered in
  the fixture's constructor. Individual tests may override or add stubs
  per-test via `fixture.Server.Given(...).RespondWith(...)`.
- `BotBuildinClient` is constructed with `HttpClient { BaseAddress =
  fixture.Server.Url }` and `BotTokenAuthenticationProvider("test-token")`.
- The fixture registers a real `IBuildinClient` singleton in the DI
  container, replacing all `Substitute.For<IBuildinClient>()` calls.

**Alternatives considered**:

- *Keep `MockHttpHandler` for existing tests, add WireMock only for new
  tests* — rejected: two mocking strategies create maintenance burden
  and drift. FR-005 mandates replacement.
- *Use WireMock's admin API to load JSON stub files* — deferred: C#
  fluent API is more discoverable, type-safe, and refactor-friendly.
  JSON stub files can be added later if the stub count grows.
- *Use a recording proxy against real buildin.ai to generate stubs* —
  rejected: violates Principle IV (no real buildin.ai in tests).

## R2 — Semantic Kernel as LLM test client

**Decision**: Replace **`Anthropic.SDK`** with **Microsoft Semantic
Kernel** (`Microsoft.SemanticKernel` + `Microsoft.SemanticKernel.Connectors.OpenAI`
NuGet packages) configured to call OpenRouter's OpenAI-compatible
endpoint.

**Rationale**:

- Semantic Kernel is model-agnostic — `AddOpenAIChatCompletion` accepts
  a custom `HttpClient` whose `BaseAddress` can point to any
  OpenAI-compatible endpoint, including OpenRouter at
  `https://openrouter.ai/api/v1`.
- Built-in auto function calling via `FunctionChoiceBehavior.Auto()`
  eliminates the hand-rolled tool-use loop currently in
  `PageReadingLlmTests.cs` (lines 197–251). SK handles the
  request → tool-call → tool-result → continue cycle internally.
- MCP tools are registered as SK plugins via `KernelPluginFactory` or
  `[KernelFunction]`-annotated methods. This maps naturally to our
  existing tool handlers.
- Microsoft-maintained, strong .NET ecosystem fit, lighter weight than
  the current Anthropic SDK for our use case (which is just chat +
  tool calling).

**Configuration**:

```csharp
var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://openrouter.ai/api/v1")
};
httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/buildout");

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
    apiKey: openRouterApiKey,
    httpClient: httpClient);
var kernel = builder.Build();
```

**Plugin registration for tool calling**:

MCP tool operations are wrapped in `[KernelFunction]` methods that
delegate to the MCP tool handlers. Alternatively, SK's
`OpenAIPromptExecutionSettings` with `FunctionChoiceBehavior.Auto()`
can use manually constructed `Function` definitions matching the MCP
tool schemas — the same pattern as the current Anthropic test but
with SK's orchestration.

**Skip logic**: Tests check for `OPENROUTER_API_KEY` environment
variable. If absent, the test returns early (skip, not fail) — same
pattern as the current `ANTHROPIC_API_KEY` check.

**Alternatives considered**:

- *Raw `HttpClient` calls to OpenRouter's `/chat/completions` endpoint*
  — rejected: would require re-implementing tool-call parsing, message
  cycling, and function-result threading. SK provides this out of the
  box.
- *OpenAI SDK (`OpenAI` NuGet package)* — viable but Semantic Kernel
  adds the function-calling orchestration layer that eliminates the
  manual tool-use loop. Raw OpenAI SDK would still need manual cycling.
- *Keep Anthropic SDK, change model to OpenRouter* — rejected: OpenRouter
  uses OpenAI-compatible API format, not Anthropic's API format. The
  Anthropic SDK cannot call OpenRouter.

## R3 — Which integration tests need WireMock conversion

**Decision**: Convert all integration tests that currently use
`IBuildinClient` mocks or `MockHttpHandler`. Leave `SearchToolTests`
(which mocks `ISearchService`) as-is.

**Scope**:

| Test file | Current mock strategy | Action |
|---|---|---|
| `MockedHttpHarnessTests.cs` | `MockHttpHandler` (DelegatingHandler) | Replace with WireMock. Tests exercise `BotBuildinClient` deserialization — natural fit. |
| `GetCommandTests.cs` | `Substitute.For<IBuildinClient>()` | Replace with WireMock + real `BotBuildinClient`. Tests exercise CLI command over DI — need real client hitting mock server. |
| `PageReadingLlmTests.cs` | `Substitute.For<IBuildinClient>()` + `AnthropicClient` | Replace with WireMock + real `BotBuildinClient` + Semantic Kernel. Both mocking strategies change. |
| `SearchToolTests.cs` | `Substitute.For<ISearchService>()` | **No change.** This tests MCP tool layer over `ISearchService`, not `IBuildinClient`. WireMock would add unnecessary depth to what is a protocol-level test. |

**Rationale for keeping SearchToolTests as-is**:

- `SearchToolTests` validates MCP tool contract (schema, error mapping,
  byte-equality with CLI). Its mock is at the `ISearchService` boundary,
  which is the correct seam for testing MCP protocol concerns.
- Converting it to WireMock would mean the test goes through
  WireMock → `BotBuildinClient` → `SearchService` → MCP tool — three
  extra layers that don't add value for protocol-contract testing.
- The search service's own correctness is covered by unit tests
  (`SearchServiceTests`) and the WireMock-backed integration tests
  (which will exercise the full stack for other features).

## R4 — GitHub Actions workflow structure

**Decision**: A single workflow file `.github/workflows/ci.yml` with
four jobs: `build`, `test-unit`, `test-integration`, and `publish`.

**Job graph**:

```
build  →  test-unit   (parallel)
       →  test-integration (parallel, depends on build)
       →  publish (depends on both test jobs passing)
```

**Trigger**:

```yaml
on:
  push:
    branches: [main]
  pull_request:
```

This fires on push to `main` and on all PR events (opened, synchronize,
reopened). FR-009 (LLM tests only on main + PR branches) is naturally
satisfied because `pull_request` only fires when a PR exists.

**Build job**:

```yaml
build:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - run: dotnet build --configuration Release
```

**Test jobs** (parallel, both depend on `build`):

- `test-unit`: `dotnet test tests/Buildout.UnitTests`
- `test-integration`: `dotnet test tests/Buildout.IntegrationTests`
  - Sets `OPENROUTER_API_KEY` from `${{ secrets.OPENROUTER_API_KEY }}`
  - Key is only available on `main` pushes and same-repo PRs; fork PRs
    get empty string, LLM tests skip gracefully.

**Publish job** (depends on both test jobs):

```yaml
publish:
  needs: [test-unit, test-integration]
  runs-on: ubuntu-latest
  steps:
    - run: dotnet publish src/Buildout.Mcp -c Release -p:PublishSingleFile=true --output publish/mcp
    - run: dotnet publish src/Buildout.Cli -c Release -p:PublishSingleFile=true --output publish/cli
    - uses: actions/upload-artifact@v4
      with:
        name: buildout-mcp
        path: publish/mcp/
    - uses: actions/upload-artifact@v4
      with:
        name: buildout-cli
        path: publish/cli/
```

**Alternatives considered**:

- *Single test job running both test projects sequentially* — rejected:
  FR-003 mandates parallel execution.
- *Matrix strategy for test jobs* — rejected: there are only two test
  projects; the overhead of matrix setup isn't worth it for two items.
- *Docker-based build* — rejected: no Docker requirement in the
  constitution; adds complexity for no benefit in a pure .NET solution.
- *Self-contained publish* — rejected per user's choice:
  framework-dependent single-file selected.

## R5 — WireMock stub contract verification

**Decision**: Add a small set of **contract tests** that validate each
WireMock stub's response body against the corresponding JSON schema
from `openapi.json`. These tests live in
`tests/Buildout.IntegrationTests/Buildin/` alongside the existing
`MockedHttpHarnessTests.cs` (which will be replaced).

**Mechanism**:

- Each stub is defined as a static method on a `BuildinStubs` class,
  returning the WireMock `IRespondWithAProvider` builder.
- A contract test loads the relevant JSON schema from `openapi.json`,
  sends a request through the WireMock server, and asserts the response
  matches the schema using `JsonSchema.Net` (or a simple structural
  comparison if schema validation is overkill).
- If a simpler approach suffices: the contract test asserts that the
  stub's response body deserializes without error into the corresponding
  Kiota-generated model class — this implicitly validates against the
  OpenAPI schema because Kiota generated the models from it.

**Pragmatic choice**: Use Kiota-generated model deserialization as the
contract verifier. If the stub response deserializes into the generated
model without error, the stub matches the OpenAPI schema. This avoids
adding a JSON schema validation library and leverages the existing
generated code as the source of truth.

**Alternatives considered**:

- *Full JSON Schema validation* — heavyweight for this use case; the
  generated models already encode the schema structure.
- *No contract tests, just trust manual maintenance* — rejected: SC-007
  requires verification. Contract tests catch drift automatically.

## R6 — .NET 10 SDK availability in GitHub Actions

**Decision**: Use `actions/setup-dotnet@v4` with `dotnet-version:
'10.0.x'`. The `10.0.x` version specifier will pick up the latest
.NET 10 patch.

**Rationale**:

- .NET 10 is in preview as of 2026-05. GitHub-hosted runners may not
  have it pre-installed. `actions/setup-dotnet` downloads and caches
  it automatically.
- The `10.0.x` pattern matches any 10.0 patch version, providing
  stability while picking up security patches.
- If .NET 10 is not yet available via `setup-dotnet`, the workflow
  will fail with a clear error message. Alternative: install from
  the official feed URL if setup-dotnet lags.

**Alternative considered**:

- *Use `dotnet-version: '10.0.100-preview.4'`* — too specific; pins
  to a preview that may have known bugs. Better to use the wildcard
  and let the action resolve the latest.

## R7 — Semantic Kernel NuGet packages

**Decision**: Add the following packages to `Buildout.IntegrationTests`:

- `Microsoft.SemanticKernel` — core SDK
- `Microsoft.SemanticKernel.Connectors.OpenAI` — OpenAI-compatible
  connector (needed for `AddOpenAIChatCompletion`)

Remove:

- `Anthropic.SDK` — replaced by SK

**Rationale**:

- SK's OpenAI connector supports any OpenAI-compatible endpoint via
  custom `HttpClient`. This is the documented path for third-party
  providers like OpenRouter.
- The connector package is a separate NuGet to avoid pulling in
  OpenAI-specific code when using Azure OpenAI or other connectors.
- SK is a Microsoft first-party package with regular releases and
  strong .NET ecosystem support.

## R8 — OpenRouter free-tier model tool-calling support

**Decision**: Assume `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free`
supports OpenAI-compatible tool/function calling until proven otherwise.
Add a defensive skip in the test if the model returns errors indicating
unsupported features.

**Rationale**:

- The model is specified by the user as the target. OpenRouter
  documents OpenAI-compatible tool calling as a standard feature.
- If the model does not support tool calling in practice, the test
  will fail with a clear error. The skip logic should catch model-level
  errors (400/422 responses) and skip the test gracefully rather than
  fail the CI.
- A future model swap is a one-line configuration change.

**Fallback**: If tool calling is unreliable on the free tier, the CI
secret can be pointed to a different model without code changes.
