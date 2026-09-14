# Implementation Plan: Migrate to Buildin API V2

**Branch**: `016-migrate-buildin-api-v2` | **Date**: 2026-09-06 | **Spec**: `specs/016-migrate-buildin-api-v2/spec.md`
**Input**: Feature specification from `/specs/016-migrate-buildin-api-v2/spec.md`

## Summary

Move every existing Buildout capability with a published equivalent to Buildin
Developer API V2 while preserving retained CLI/MCP, Markdown, caching, telemetry,
and output contracts. Remove the CLI `delete`/`restore` commands, MCP
`delete_page`/`restore_page` tools, and their page-lifecycle core slice as an
accepted breaking change. Replace the checked-in V1 OpenAPI snapshot and
broad V1-generated client with the authoritative V2 2.0.0 contract plus a
minimal documentation-derived overlay and deterministically normalized Kiota
input. The overlay supplies only V2 block DELETE; no page lifecycle operation is
overlaid or exposed. Use V2-native `Idempotency-Key` for page creation and
page-read `ETag` values for block-edit preflight, removing the local CRC revision
token. No retained workflow uses `If-Match` because V2 block writes do not publish
it and page PATCH has no retained caller.
Translate V2 wire models and structured failures into stable Buildout domain
models using `InTrash`, introduce `AccessToken` as the primary generic bearer
credential, retain `BotToken` only as a warning-emitting configuration fallback,
and prove the route boundary with mocked HTTP acceptance tests.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`, SDK 10.0.203), nullable reference types and warnings-as-errors enabled solution-wide
**Primary Dependencies**: Microsoft Kiota CLI 1.31.1 and `Microsoft.Kiota.Bundle` 1.22.1; `HttpClient`; `System.Text.Json`; Microsoft.Extensions.Configuration/Options/Logging; existing Spectre.Console.Cli and ModelContextProtocol presentation stacks
**Storage**: No runtime persistence; version-controlled `openapi.json`, deterministic Kiota normalization metadata/input, and generated C# client sources
**Testing**: xUnit v3, NSubstitute, WireMock.Net, Spectre.Console.Testing, existing cheap-LLM MCP tests, and offline generation/manifest comparisons
**Target Platform**: Cross-platform .NET 10 CLI and MCP server (`stdio` and `http`) calling `https://api.buildin.ai` or the existing configured base URL
**Project Type**: Existing shared core library plus CLI and MCP presentation projects; no new product project
**Performance Goals**: Preserve current command latency characteristics; complete cursor-driven workflows with `page_size <= 100`; permit at most one automatic page-create retry protected by the same native idempotency key and identical body; avoid duplicate writes; add only the page-version reads required for ETag preconditions
**Constraints**: Retained public CLI/MCP contracts stay stable; lifecycle surface removal and `InTrash` naming are accepted breaking changes; one primary secret setting plus one deprecated fallback; bearer token only in the `Authorization` header; native request-safety headers only on endpoints that publish them; no live-service tests; zero V1 traffic; generated code remains disposable; published OpenAPI remains unmodified and documented block DELETE is isolated in a reviewed overlay
**Scale/Scope**: 13 V2 operations backing retained identity, page, block, database, and search capabilities; zero V1 operations; 6 retained CLI commands, 6 retained MCP tools plus the page resource, all currently supported block/property/rich-text variants, and multi-page result sets

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Pre-design | Post-design | Notes |
|-----------|------------|-------------|-------|
| I. Core/Presentation Separation | PASS | PASS | V2 generated code, wire/domain translation, routing, authentication, pagination, and error classification remain under `Buildout.Core/Buildin/`. CLI and MCP continue to depend on domain services only. |
| II. LLM-Friendly Output Fidelity | PASS | PASS | Public Markdown, search, database-view, edit, and tree output contracts retain their semantics. Trash fields use the accepted V2-native `InTrash` naming change, and the unsupported-block policy remains. |
| III. Bidirectional Round-Trip Testing | PASS | PASS | Every affected supported block type is covered in both round-trip directions after the DTO remap; any existing documented loss remains explicit and no new silent loss is allowed. |
| IV. Test-First Discipline | PASS | PASS | Route, mapper, error, pagination, native-header, deprecation, secret-safety, and cross-surface acceptance tests are written against mocks before production changes. The currently skipped regeneration test is replaced with an executable offline check; no real token or live Buildin service is used. |
| V. Buildin API Abstraction | PASS | PASS | `IBuildinClient` stays the sole presentation/domain HTTP boundary and every retained operation routes through the generated V2 client. No V1 adapter, page-lifecycle compatibility service, or presentation version selector exists. |
| VI. Non-Destructive Editing | PASS | PASS | Targeted reconciliation remains unchanged. The public revision string becomes an opaque V2 page ETag instead of a local CRC. A fresh ETag preflight rejects stale block edits before the first write, while partial-write risk remains explicit because V2 block endpoints do not publish `If-Match`. |
| VII. Dual-Channel Configuration | PASS | PASS | `AccessToken` and `Buildout__AccessToken` are the primary JSON/environment names. `BotToken` remains a deprecated fallback with one secret-safe warning; both resolve through the unified loader and no second secret copy is required. |
| VIII. Skills & Prompts Parity | PASS | PASS | Removed delete/restore commands lose their skill files and MCP registrations/prompts in the same change. Retained prompts/skills document ETag revisions and `AccessToken`; no orphan command/tool documentation remains. |

**Gate result before Phase 0**: PASS — the proposed boundary introduces no
constitution violation.

**Gate result after Phase 1**: PASS — the data model and contracts keep all API
version knowledge inside core, retain public behavior, require mocked test-first
coverage, and add no configuration channel or presentation capability.

## Project Structure

### Documentation (this feature)

```text
specs/016-migrate-buildin-api-v2/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api-routing.md
│   ├── public-surface.md
│   ├── regeneration.md
│   └── scope-matrix.md
└── tasks.md                         # Phase 2 output; not created by /speckit-plan
```

### Source Code (repository root)

```text
openapi.json                         # REPLACED: untouched published V2 2.0.0 snapshot

scripts/
├── fetch_openapi.sh                 # MODIFIED: fetch V2 URL to temp, validate identity/routes, atomically replace snapshot
├── openapi.buildout-overlay.json    # NEW: one reviewed block DELETE definition currently omitted by published OpenAPI
├── normalize-buildin-openapi.cs     # NEW: apply overlay and deterministic Kiota union/discriminator normalization
├── regenerate-buildin-client.sh     # MODIFIED: normalize, generate V2-only client, verify output
└── regenerate-buildin-client.ps1    # MODIFIED: same normalization/generation contract on Windows

src/Buildout.Core/Buildin/
├── IBuildinClient.cs                # MODIFIED: retained semantic methods, versioned page reads; remove unused page-update lifecycle method
├── BuildinClient.cs                 # RENAMED from BotBuildinClient: V2-only facade, ETag capture, Idempotency-Key propagation
├── BuildinClientLog.cs              # RENAMED: stable V2 diagnostic tags; never IDs/tokens/keys/ETags
├── BuildinClientOptions.cs          # MODIFIED: AccessToken primary plus deprecated nullable BotToken alias
├── AccessTokenResolver.cs           # NEW: precedence, validation, and once-per-process secret-safe warning
├── Authentication/
│   └── AccessTokenAuthenticationProvider.cs # RENAMED: generic bearer semantics and configured-host restriction
├── Errors/
│   ├── BuildinError.cs              # MODIFIED: structured request ID, details, retry metadata, failure category
│   └── BuildinApiException.cs       # MODIFIED: safe actionable rendering without secret/header leakage
├── Generated/
│   ├── V2/                          # REGENERATED: Kiota V2 client only
│   ├── _README.md                   # MODIFIED: V2 source, normalizer, and do-not-edit instructions
│   └── kiota-lock.json              # REGENERATED with pinned settings
├── Mapping/
│   ├── BlockMapper.cs
│   ├── DatabaseMapper.cs
│   ├── MappingHelpers.cs
│   ├── PageMapper.cs
│   ├── ParentIconMapper.cs
│   ├── RichTextMapper.cs
│   ├── SearchMapper.cs
│   └── UserMapper.cs                # ALL MODIFIED for V2 shapes and InTrash domain naming
├── Models/
│   ├── Annotations.cs               # MODIFIED only as needed to preserve V2 text/background color semantics
│   ├── Page.cs / Database.cs / Block.cs # MODIFIED: use InTrash terminology
│   ├── UpdatePageRequest.cs         # REMOVED with page lifecycle/current unused facade operation
│   ├── VersionedPage.cs             # NEW: page domain result plus opaque response ETag
│   ├── NativeWriteContext.cs        # NEW: per-invocation idempotency metadata; never persisted
│   └── remaining request/result records # MODIFIED only for V2-to-domain fidelity
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs # MODIFIED: shared bearer HTTP pipeline and V2 facade

src/Buildout.Core/PageLifecycle/      # REMOVED entirely: interface, service, outcome, DI registration

src/Buildout.Core/Markdown/Editing/
├── PageEditor.cs                    # MODIFIED: opaque ETag revisions, fresh preflight, explicit non-atomic block writes
└── Internal/RevisionTokenComputer.cs # REMOVED: no client-computed page-version authority

src/Buildout.Cli/
├── Commands/                        # delete/restore command/settings files and registrations REMOVED
└── Skills/                          # delete/restore skills REMOVED; retained skills use AccessToken/ETag terminology

src/Buildout.Mcp/
├── Resources/                       # SIGNATURES UNCHANGED; error classification adjusted where required
├── Tools/                           # delete/restore handlers and registrations REMOVED
└── Prompts/                         # lifecycle references REMOVED; migration, ETag, error, and scope guidance updated

docs/
├── configuration.md                 # MODIFIED: AccessToken primary; BotToken fallback/deprecation/precedence
└── buildin-api-v2.md                # NEW: supported operations, breaking removals, request safety, refresh/overlay instructions

tests/Buildout.UnitTests/
├── Buildin/                          # V2 client, mapper, auth, token-resolution, pagination, overlay, ETag/idempotency, and block-delete tests
├── RoundTrip/                        # all affected block round-trip suites
├── PageLifecycle/                    # REMOVED with lifecycle production code
└── existing domain/presentation tests # retained contracts plus removed-surface discovery gates

tests/Buildout.IntegrationTests/
├── Buildin/
│   ├── BuildinStubs.cs               # MODIFIED: V2 fixtures including block DELETE, ETag, and idempotency
│   ├── BuildinWireMockFixture.cs
│   ├── WireMockContractTests.cs       # MODIFIED: exact routes/bodies, ETag/Idempotency-Key, and absence of unsupported If-Match
│   ├── MockedHttpHarnessTests.cs
│   └── RegenerationDeterminismTests.cs # MODIFIED: executable isolated two-pass generation comparison
├── Cli/                              # retained command contracts + absence of delete/restore + V2 error/scope cases
├── Mcp/                              # retained resource/tool contracts + absence of lifecycle tools
└── Cross/                            # CLI/MCP parity and non-destructive editing/partial-failure behavior
```

**Structure Decision**: Retain the existing solution layout and use
`IBuildinClient` as the stable domain seam. Generated code is V2-only and isolated
under `Generated/V2`. A reviewed one-action overlay supplies only officially
documented block DELETE until the published OpenAPI includes it; the overlay is
not runtime compatibility code. The page lifecycle vertical slice is removed.
Retained cache, renderer, CLI, and MCP services consume version-neutral domain
models using `InTrash`. The editor replaces its locally computed revision
with the opaque page ETag, but V2 block writes remain multiple non-transactional
calls because their published operations do not accept `If-Match`.

## Native V2 Integration Assessment

There is no production V1 client, fallback route, version switch, or page-lifecycle
compatibility stub in the target architecture. `BuildinStubs.cs` is WireMock test
infrastructure, not a runtime shim. The V2 transport is native for every retained
operation and uses published `Idempotency-Key` and `ETag` contracts. `If-Match`
remains generated capability but has no retained production caller.

Three deliberate adaptation boundaries remain:

1. The generation-only overlay adds documented block DELETE currently absent from
   the published OpenAPI; it is removed when upstream catches up.
2. `BotToken` remains a configuration-only compatibility alias with a deprecation
   warning; it never changes routing or authentication semantics.
3. Block reconciliation remains a sequence of native V2 calls. Its initial
   stale-check uses a server-issued page ETag, but it is not atomic because V2 does
   not publish `If-Match` for block update, append, or delete.

## Complexity Tracking

No constitution violations require justification.
