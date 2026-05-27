# Implementation Plan: MCP Authorization Modes

**Branch**: `014-mcp-authorization` | **Date**: 2025-05-27 | **Spec**: `specs/014-mcp-authorization/spec.md`
**Input**: Feature specification from `/specs/014-mcp-authorization/spec.md`

## Summary

Add four authorization modes to the MCP HTTP transport — `none`, `passthrough`, `proxy`, and `mapped` — controlling how incoming MCP requests are authenticated and which Buildin Bot API key is used for outbound requests. The feature introduces an `IRequestAuthenticator` abstraction in `Buildout.Core` that resolves a `Buildin Bot API key` per request, a database-backed token registry (sharing the same FluentMigrator/ADO.NET pattern established by feature 013), CLI commands for token lifecycle management, and a per-request `IAuthenticationProvider` swap in the MCP pipeline. Mode `none` preserves current behavior exactly.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (SDK style projects, `net10.0`)
**Primary Dependencies**: `ModelContextProtocol` 1.2.0 (MCP SDK), `ModelContextProtocol.AspNetCore` (HTTP transport), `Microsoft.Data.Sqlite` (SQLite), `Npgsql` (PostgreSQL), `FluentMigrator` / `FluentMigrator.Runner.SQLite` / `FluentMigrator.Runner.Postgres` (schema migrations), `Microsoft.Extensions.Configuration` / `Options` / `DependencyInjection`, `Spectre.Console.Cli` (CLI commands)
**Storage**: SQLite and PostgreSQL — same dual-provider pattern as audit trails (feature 013). Token registry tables live in the same database as audit entries (shared connection).
**Testing**: xUnit v3, NSubstitute, WireMock.Net (existing stack), `Testcontainers.PostgreSql` (PostgreSQL integration tests)
**Target Platform**: Server-side .NET (MCP server over HTTP transport)
**Project Type**: MCP server extension + CLI commands
**Performance Goals**: <5ms added latency per MCP request for `proxy`/`mapped` mode token validation (SC-004). Zero overhead in `none` mode.
**Constraints**: HTTP transport only (FR-011). Stdio and CLI bypass all authorization. Auth mode is read at startup; restart required to change. Tokens hashed with one-way hash; shown only once at creation.
**Scale/Scope**: Up to 100 tokens (SC-003). Single-workspace. No token expiration, rate limiting, or brute-force protection in scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Core/Presentation Separation (NON-NEGOTIABLE) | PASS | `IRequestAuthenticator` interface, `AuthOptions`, `AuthTokenResult` defined in `Buildout.Core`. Concrete implementations (`NoneAuthenticator`, `PassthroughAuthenticator`, `ProxyAuthenticator`, `MappedAuthenticator`), token data access (`TokenStore`), migrations, and the MCP filter live in `Buildout.Mcp`. CLI commands for token management live in `Buildout.Cli`. |
| II. LLM-Friendly Output Fidelity | N/A | Authorization does not affect Markdown rendering or block conversion. |
| III. Bidirectional Round-Trip Testing | N/A | No block/Markdown conversion. |
| IV. Test-First Discipline (NON-NEGOTIABLE) | PASS | Unit tests for each authenticator, token hashing, options validation. Integration tests for SQLite and PostgreSQL token store. MCP pipeline integration test exercising all four modes. CLI command tests. Migration verification tests. |
| V. Buildin API Abstraction | PASS | The authenticator resolves which `BotToken` to use per request. A per-request `IAuthenticationProvider` is constructed from the resolved token. The `IBuildinClient` interface is unchanged — only the `IAuthenticationProvider` registered in DI varies. Switching to User API remains possible by adding a new authenticator variant. |
| VI. Non-Destructive Editing | N/A | Authorization does not implement block edits. |
| VII. Dual-Channel Configuration (NON-NEGOTIABLE) | PASS | All auth options (`Auth:Mode`, `Auth:Provider`, `Auth:SqlitePath`, `Auth:ConnectionString`) exposed via both JSON config and `Buildout__Auth__*` env vars. `docs/configuration.md` updated. `IValidateOptions<AuthOptions>` for fail-fast startup validation. |
| VIII. Skills & Prompts Parity | PASS | New CLI subcommands (`auth token create`, `auth token list`, `auth token revoke`, `auth token map`) require skill files in `Buildout.Cli/Skills/`. No MCP tool changes. |

**Gate result**: PASS — no violations. All non-N/A principles satisfied by the design.

## Project Structure

### Documentation (this feature)

```text
specs/014-mcp-authorization/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
  Buildout.Core/
    Auth/
      IRequestAuthenticator.cs          # Interface: AuthenticateAsync(HttpContext) → AuthResult
      AuthResult.cs                     # record: IsAuthenticated, ResolvedBotToken, TokenIdentity, ErrorMessage
      AuthMode.cs                       # enum: None, Passthrough, Proxy, Mapped
      AuthOptions.cs                    # Options class: Mode, Provider, SqlitePath, ConnectionString
      AuthOptionsValidator.cs           # IValidateOptions<AuthOptions>

  Buildout.Mcp/
    Auth/
      NoneAuthenticator.cs              # Uses global BotToken, always succeeds
      PassthroughAuthenticator.cs       # Extracts Buildin Bot API key from Authorization header
      ProxyAuthenticator.cs             # Validates MCP token via TokenStore, returns global BotToken
      MappedAuthenticator.cs            # Validates MCP token via TokenStore, returns mapped BotToken
      TokenStore.cs                     # ADO.NET token registry: create, validate, revoke, list, map
      AuthFilter.cs                     # MCP CallToolFilter: authenticates request, swaps IAuthenticationProvider
      AuthMcpServiceExtensions.cs       # DI registration: authenticator, token store, FluentMigrator, filter
      Migrations/
        Migration_002_CreateAuthTables.cs  # FluentMigrator: mcp_tokens, buildin_keys, token_key_mappings

  Buildout.Cli/
    Commands/
      AuthSettings.cs                   # Base settings for auth commands
      AuthTokenCreateCommand.cs         # `auth token create`
      AuthTokenListCommand.cs           # `auth token list`
      AuthTokenRevokeCommand.cs         # `auth token revoke`
      AuthTokenMapCommand.cs            # `auth token map`

tests/
  Buildout.UnitTests/
    Auth/
      AuthOptionsValidatorTests.cs      # Options validation tests
      NoneAuthenticatorTests.cs         # None mode tests
      PassthroughAuthenticatorTests.cs  # Passthrough mode tests
      TokenHashingTests.cs              # Hash generation and verification
  Buildout.IntegrationTests/
    Auth/
      AuthTestFixture.cs                # Shared fixture for SQLite + PostgreSQL token store tests
      SqliteTokenStoreTests.cs          # SQLite token store integration tests
      PostgresTokenStoreTests.cs        # PostgreSQL token store integration tests (Testcontainers)
      ProxyAuthenticatorTests.cs        # Proxy mode integration test
      MappedAuthenticatorTests.cs       # Mapped mode integration test
      AuthModeEndToEndTests.cs          # Full MCP pipeline test: all four modes
      MigrationTests.cs                 # Migration 002 verification (SQLite + PostgreSQL)
      CliAuthTokenTests.cs              # CLI auth token command tests
```

**Structure Decision**: Follows the established project layout. Interface + options + enum in `Buildout.Core` (per Principle I), implementations in `Buildout.Mcp`, CLI commands in `Buildout.Cli`. Auth tables share the same database as audit entries (feature 013). FluentMigrator migration numbered `002` to follow the existing `001_CreateAuditEntries`.
