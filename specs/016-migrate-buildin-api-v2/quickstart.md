# Quickstart: Validate the Buildin API V2 Migration

This is a mock-only validation guide for the completed feature. It requires no
live Buildin workspace or real token.

## Prerequisites

- .NET SDK 10.0.203 or a compatible .NET 10 SDK.
- Repository-local tools restored from `.config/dotnet-tools.json`.
- A clean checkout or isolated worktree when validating regeneration.

```bash
dotnet tool restore
dotnet build buildout.slnx
```

Expected: restore/build succeeds with nullable analysis and warnings-as-errors;
generated code compiles only through the core adapter boundary.

## 1. Verify the checked-in contract and generated route boundary

```bash
jq -e '.openapi == "3.1.0"
  and .info.title == "Buildin Developer API V2"
  and .info.version == "2.0.0"' openapi.json

! rg -n '/v1/' src/Buildout.Core/Buildin/Generated/V2
```

Expected: the identity check succeeds and the generated directory contains no V1
route. The effective contract contains 17 paths and 22 operations after applying
the reviewed overlay; no production source contains a V1 request route.

## 2. Run focused core and HTTP contract tests

```bash
dotnet test tests/Buildout.UnitTests/Buildout.UnitTests.csproj \
  --filter 'FullyQualifiedName~Buildin|FullyQualifiedName~RoundTrip|FullyQualifiedName~Markdown.Editing'

dotnet test tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj \
  --filter 'FullyQualifiedName~Buildin'
```

Expected:

- Every existing-capability client method uses the exact V2 method/path/body.
- `AccessToken` integration-shaped and OAuth-shaped fixtures produce the same
  authorized outcomes and appear only in the bearer header observed by WireMock;
  `BotToken` fallback cases emit one value-free deprecation warning.
- Page creation carries a non-empty `Idempotency-Key`; the single permitted retry of one
  invocation reuses the key and identical body, while a distinct invocation gets
  a distinct key.
- A write-oriented page read captures `ETag`, and the existing revision output and
  block-edit preflight use that opaque value.
- The production page-edit path uses the service ETag rather than a CRC/content-derived revision. Block update/append/delete receive
  no undocumented concurrency header and the partial-write boundary stays visible.
- Block deletion uses `DELETE /v2/blocks/{id}`. CLI/MCP discovery contains no page
  delete/restore surfaces and the mock journal contains zero V1 or lifecycle requests.
- Representative 400, 401, 403, 404, 409, 429, transport, and unexpected failures
  preserve category, code, message, request ID, and details safely.
- Supported blocks, rich text, parents, properties, and `in_trash` map to stable
  domain meanings using `InTrash` rather than `Archived`.

## 3. Validate complete pagination and ordering

Run the migration pagination cases in the focused suites. They must use controlled
three-page fixtures for search, block children, database query/view, and tree,
including exactly-100-item boundaries and page sizes 1 and 100.

Expected: exact item counts and service order, no duplicates/omissions, opaque
cursor forwarding, no fourth request after completion, repeated-cursor failure,
and cancellation propagation. Page sizes 0 and 101 fail before unrelated HTTP
requests.

## 4. Validate stable CLI and MCP journeys

```bash
dotnet test tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj \
  --filter 'FullyQualifiedName~Cli|FullyQualifiedName~Mcp|FullyQualifiedName~Cross'
```

Expected: retained read, editing-read, search, create, update, database-view, and
tree journeys keep their command/tool names, parameters, output shapes, ordering,
and error/exit semantics. In particular:

- Read/search/tree/database workflows consume all mocked cursors.
- Create under page/database parents and nested block appends use V2.
- Update dry-run makes no writes and returns the validated current page ETag;
  committed update/append uses V2 and obtains its new revision from a final page
  read.
- A mixed edit uses V2 block DELETE only for removed block IDs and preserves
  unrelated IDs.
- CLI command discovery excludes `delete` and `restore`; MCP tool discovery
  excludes `delete_page` and `restore_page`; lifecycle core types are absent.
- CLI and MCP parity suites remain green.

## 5. Validate secret safety

Run the Buildin, configuration, logging, CLI, and MCP secret-sentinel cases.

Expected: the token sentinel appears only in the mock server's received
Authorization header. It is absent from URLs, bodies, logs, metrics, exceptions,
snapshots, stdout/stderr, and committed fixtures. Idempotency keys are also absent
from logs, metrics, exceptions, and public output; ETags appear only in the page
read metadata path and the established revision field. Deprecation warnings name
`BotToken` and `AccessToken` but contain neither value.

## 6. Verify deterministic generation

```bash
dotnet test tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj \
  --filter 'FullyQualifiedName~RegenerationDeterminismTests'
```

Expected: the test copies the canonical snapshot and generation inputs to an
isolated temporary directory, generates twice with pinned Kiota 1.31.1, and finds
identical file manifests and byte hashes. It does not fetch the network or modify
the working tree.

Maintainers refreshing upstream explicitly run:

```bash
./scripts/fetch_openapi.sh
./scripts/regenerate-buildin-client.sh
git diff -- openapi.json src/Buildout.Core/Buildin/Generated
```

Review must show V2 contract/client changes only. A second regeneration without an
upstream change must produce no diff. On Windows, use the equivalent checked-in
PowerShell regeneration script after refreshing the same canonical snapshot.

## 7. Run the full merge gate

```bash
dotnet test tests/Buildout.UnitTests/Buildout.UnitTests.csproj
dotnet test tests/Buildout.IntegrationTests/Buildout.IntegrationTests.csproj
```

Expected: both suites pass with no skipped migration/determinism test and no live
service dependency. Review the route journal and the scope/public-surface tables in
`specs/016-migrate-buildin-api-v2/contracts/` before merge.
