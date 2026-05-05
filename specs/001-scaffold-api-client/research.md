# Phase 0 Research: Scaffold + Buildin API Client

This document resolves every technology choice and unknown identified during
Technical Context elicitation. Each entry follows the **Decision / Rationale /
Alternatives** format. Sources are linked inline; key findings are summarised.

## R1 — OpenAPI client generator

**Decision**: Use **Kiota** (Microsoft.OpenApi.Kiota), pinned as a local
`dotnet-tools.json` tool.

**Rationale**:

- User-directed choice for this feature.
- First-party Microsoft tooling; explicit support for .NET SDK 10 in current
  Microsoft Learn tutorials.
- Generates idiomatic typed clients with full IDE autocomplete; no runtime DTO
  reflection.
- Single runtime dependency: `Microsoft.Kiota.Bundle`.

**Alternatives considered**:

- **NSwag** — battle-tested but actively decoupling from Kiota; less polished
  for OpenAPI 3.1.
- **Refitter / Refit** — generates Refit interfaces; nice for simple APIs, but
  weak on rich polymorphic schemas (which buildin uses heavily).
- **OpenAPI Generator** — multi-language but the C# templates lag the dotnet
  ecosystem; verbose generated code.

## R2 — Generated client visibility and wrapping

**Decision**: The Kiota-generated client is **internal implementation detail of
`Buildout.Core`**, not the public API surface. `Buildout.Core` exposes the
hand-written `IBuildinClient` interface and idiomatic domain types in
`Buildout.Core/Buildin/Models/`. `BotBuildinClient` is the single class that
references generated types and translates between them and the public surface.

**Rationale**:

- Constitution Principle V mandates that User API + OAuth must be addable
  without changes outside `Buildout.Core`. Hiding generated types behind
  `IBuildinClient` makes that swap mechanical.
- Constitution Principle I forbids presentation projects from depending on
  buildin internals. The generated tree is "buildin internals."
- Kiota's handling of `oneOf` without discriminator yields wrapper classes
  with one property per variant (see R12). Exposing those at the API surface
  would push that ergonomics tax onto every downstream feature.
- Regeneration churn (when `openapi.json` evolves) is contained — it cannot
  ripple beyond `BotBuildinClient` and `Buildout.Core/Buildin/Models/`.

**Alternatives considered**:

- **Re-export the generated client directly as `IBuildinClient`** — would
  couple every consumer to Kiota wrapper-class shapes and to Bot-API-specific
  paths, breaking Principle V.
- **`internal` access modifier on the entire generated namespace** — still
  considered, will be enforced via Kiota's namespace-level access modifier
  configuration where supported, otherwise via assembly internal-only access.

## R3 — Test framework

**Decision**: **xUnit** (v3 stream — `xunit.v3` + `xunit.runner.visualstudio`).

**Rationale**:

- Microsoft's recommended framework in the official Kiota testing guide.
- Best parallel-test story among .NET test frameworks; matters for SC-003
  (full suite under 60 s).
- xUnit v3 ships modern test SDK, AOT-friendly, native to .NET 10.

**Alternatives considered**:

- **NUnit** — comparable; less aligned with Kiota's docs.
- **MSTest** — heavier ceremony; weaker parallelism story historically.

## R4 — Mocking library

**Decision**: **NSubstitute**.

**Rationale**:

- Used in the official Microsoft Learn Kiota testing tutorial; copy-pasteable
  patterns from first-party docs.
- Cleaner syntax than Moq for the `IRequestAdapter` mocking pattern (no
  `It.IsAny<T>()` ceremony).

**Alternatives considered**:

- **Moq** — popular, but recent licensing/security incidents (the SponsorLink
  episode) damaged its reputation; team prefers a calmer maintainer history.
- **FakeItEasy** — fine, but no presence in Kiota's docs.

## R5 — Unit-test mocking strategy

**Decision**: Mock `IRequestAdapter` directly via NSubstitute. Tests construct
`BotBuildinClient` (or rather, the generated Kiota client it wraps) with the
substituted adapter and assert on calls and return values.

**Rationale**:

- Matches the official Kiota testing guide verbatim — minimum cognitive load
  for new contributors.
- Avoids serialisation concerns at unit-test scope; serialisation is exercised
  separately by integration tests (R6).

**Alternatives considered**:

- **Mock at `HttpMessageHandler`** for unit tests too — adds end-to-end
  serialisation coverage but slower to write and debug; better as integration
  tests (see R6).

## R6 — Integration-test mocking strategy

**Decision**: Inject a custom `HttpMessageHandler` (or use `RichardSzalay.MockHttp`
if the team prefers a fluent matcher DSL) into the `HttpClient` Kiota uses, so
the full request/serialise/HTTP/deserialise stack runs against canned responses.

**Rationale**:

- Catches serialisation, header, query-parameter, and content-negotiation bugs
  that unit-level adapter mocking cannot.
- No real network — satisfies Principle IV and SC-006.
- Composable with future MCP / CLI integration tests that exercise the same
  HTTP layer through the presentation projects.

**Alternatives considered**:

- **WireMock.NET** — fully featured but heavyweight; spins up a real local HTTP
  server per test. Worth reconsidering if cross-process tests become necessary
  later. Kept in our back pocket; not picked now.

## R7 — Authentication

**Decision**: A small `BotTokenAuthenticationProvider` derived from Kiota's
`BaseBearerTokenAuthenticationProvider`, returning the Bot token sourced at
construction time from `BuildinClientOptions.BotToken`.

**Rationale**:

- Kiota's bearer-token base class is the documented happy path.
- Token comes from `Microsoft.Extensions.Configuration` (env vars in dev, real
  config in production), keeping secrets out of source per FR-010.
- A future User-API + OAuth implementation supplies a different
  `IAuthenticationProvider` to its own `BuildinClient` impl, with no impact on
  `IBuildinClient` callers.

**Alternatives considered**:

- **`AnonymousAuthenticationProvider`** with a hand-rolled bearer header —
  bypasses Kiota's token-refresh hooks the User-API path will eventually use.

## R8 — Configuration source

**Decision**: `Microsoft.Extensions.Configuration` with environment variable
binding under the `BUILDOUT__BUILDIN__` prefix (double-underscore scoping per
.NET conventions). The `BuildinClientOptions` POCO has `BaseUrl`, `BotToken`,
and `HttpTimeout` properties.

**Rationale**:

- Standard .NET pattern; works identically for `Buildout.Mcp` and `Buildout.Cli`
  hosts and for tests.
- Lets `appsettings.json` override base URL for testing/staging without code
  changes (relevant — `openapi.json` lists prod and "test" with identical URLs;
  see spec edge cases).

**Alternatives considered**:

- **Hard-coded base URL + token via constructor** — works for tests but pushes
  config plumbing into every host; rejected.

## R9 — Logging

**Decision**: `Microsoft.Extensions.Logging` abstractions consumed by
`BotBuildinClient` via constructor `ILogger<BotBuildinClient>`. No log sink is
configured in `Buildout.Core`; presentation projects wire sinks (file, console,
structured) at host startup in later features.

**Rationale**:

- Standard .NET abstraction; trivially substitutable in tests with
  `NullLogger<T>` or NSubstitute mocks.

**Alternatives considered**:

- **Serilog directly** — pinning a concrete sink in the core library would
  violate the abstraction principle and force presentation projects to bring
  Serilog whether they want it or not.

## R10 — Dependency injection

**Decision**: `Microsoft.Extensions.DependencyInjection`. `Buildout.Core`
exposes `services.AddBuildinClient(configuration)` as the canonical
registration entry point in
`src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`.

**Rationale**:

- Idiomatic .NET hosting model; works seamlessly with both presentation
  projects' eventual `Host` setup and with the test projects.

## R11 — Generator regeneration ergonomics

**Decision**: A single bash script `scripts/regenerate-buildin-client.sh`
invokes `dotnet kiota generate` against `openapi.json` with fixed flags. Kiota
itself is pinned via a local `.config/dotnet-tools.json` so all contributors
use the same version. Running the script with no arguments regenerates the
client deterministically.

**Rationale**:

- Single command satisfies FR-006 / SC-004.
- Local tool restore (`dotnet tool restore`) gives every dev the same Kiota
  version without manual `--global` install drift.
- Bash script is portable on macOS/Linux; a parallel `.ps1` is created in the
  same directory for Windows users (mirrors the existing
  `.specify/extensions/git/scripts/{bash,powershell}` pattern in the repo).

**Alternatives considered**:

- **MSBuild target / `<KiotaTargets>`** — generate on every build. Rejected:
  forces all consumers (CI, downstream projects, packaging) to install Kiota
  and read `openapi.json`, and obscures generation diffs in PRs. Constitution
  Principle I prefers reviewable, source-of-truth generated code.
- **Global `dotnet tool install`** — version drift across machines.

## R12 — Polymorphism in `openapi.json`

**Decision**: Accept Kiota's wrapper-class (intersection-type) treatment for the
unkeyed `oneOf` schemas in `openapi.json`. Hand-written domain types in
`Buildout.Core/Buildin/Models/` translate Kiota wrappers into idiomatic
sealed-class hierarchies (e.g. `Parent` → `ParentDatabase | ParentPage |
ParentBlock | ParentSpace`) at the public surface.

**Rationale** (sourced from
[Kiota models docs](https://learn.microsoft.com/openapi/kiota/models)):

- Kiota explicitly states: *"Using `oneOf` to constrain derived types is **not**
  supported as Kiota interprets that as an intersection type."* The buildin
  OpenAPI uses unkeyed `oneOf` for `Parent` (4 variants), `Icon` (3 variants),
  `PropertyValue` (13 variants), and `CreatePagePropertyValue` (13 variants),
  and unkeyed `anyOf` for `PropertySchema` (14 variants).
- Generated wrapper classes (one nullable property per variant, only one
  non-null at a time) are functional but ergonomically poor at call sites.
- Since the generated client is internal (R2), the wrapper-class shape is
  invisible to downstream features. The translation tax is paid once, in
  `BotBuildinClient`.

**Alternatives considered**:

- **Patch `openapi.json` to add discriminators** — changes the source of truth.
  If buildin later corrects their spec, our edits would conflict. Rejected.
- **Post-generation Roslyn rewriter** — possible but high-effort for marginal
  benefit; reconsider if downstream feature work runs into call-site pain
  beyond what `Buildout.Core/Buildin/Models/` can absorb.

**Risk note**: If a future buildin operation introduces a polymorphic shape
that the wrapper-class translation cannot represent cleanly, the unsupported
operation may be hand-written in `Buildout.Core/Buildin/` outside the
generated tree, with a regeneration-script note. This preserves regenerability
of the supported surface.

## R13 — Solution file format

**Decision**: `buildout.slnx` (already present in the repo from the bootstrap
commit). All five projects added via `dotnet sln add`.

**Rationale**:

- `.slnx` is the modern XML solution format supported in .NET SDK 9+, default
  in .NET 10 tooling. The bootstrap commit chose it.
- Compatible with `dotnet build`, Rider, Visual Studio 2022+, and VS Code
  (via the C# Dev Kit).

## R14 — Quality gates baseline

**Decision**: A repo-root `Directory.Build.props` enables solution-wide:

- `<Nullable>enable</Nullable>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `<LangVersion>latest</LangVersion>`
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`
- `<AnalysisLevel>latest-recommended</AnalysisLevel>`

A complementary `Directory.Build.targets` excludes the Kiota `Generated/`
subdirectory from analyzers and code-style checks (Kiota emits idiomatic but
not always analyzer-clean output) while keeping it inside the compilation.

**Rationale**:

- Constitution Technology Standards mandate nullable + warnings-as-errors
  solution-wide.
- Excluding generated code from style enforcement is industry-standard;
  enforcing on generator output produces noise without value.

## R15 — Generated-code marking

**Decision**: Two-pronged marking so contributors and tooling never confuse
generated code with hand-written code:

- `src/Buildout.Core/Buildin/Generated/_README.md` explains the directory is
  machine-generated and links to `scripts/regenerate-buildin-client.sh`.
- Kiota emits `<auto-generated/>` headers on every output `.cs` file by
  default; we keep them.

**Rationale**:

- `<auto-generated/>` is honoured by .NET analyzers and most IDEs (suppresses
  refactorings on generated code).
- A directory-level README is the lowest-friction signal for human readers.

## R16 — OpenAPI base URL handling

**Decision**: Kiota's generated clients pick up the base URL from the
`servers` block at runtime via `IRequestAdapter.BaseUrl`. We override that at
construction time from `BuildinClientOptions.BaseUrl`, defaulting to
`https://api.buildin.ai`.

**Rationale**:

- The bundled `openapi.json` lists production and "test" servers with
  identical URLs (`https://api.buildin.ai` for both). Trusting the document
  would prevent us from pointing tests/staging at a different host without
  editing the OpenAPI file.
- Setting the base URL via configuration (R8) means tests and future staging
  deployments override it without touching either generated code or
  `openapi.json`.

## Open items deferred to later features

These were intentionally NOT resolved in Phase 0 because they belong to later
features and have no impact on this scaffold:

- Block ↔ Markdown converter design (feature: read page as Markdown).
- Specific MCP tool descriptors and CLI commands (those features).
- The "cheap testing LLM" mentioned in the constitution for MCP integration
  tests (selected when MCP tools land).
- User-API + OAuth implementation (interface kept open per R2 / Principle V).
- CI configuration (deferred per spec assumption).
