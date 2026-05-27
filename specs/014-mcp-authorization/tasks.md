# Tasks: MCP Authorization Modes

**Input**: Design documents from `/specs/014-mcp-authorization/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY per the project constitution (Principle IV — Test-First Discipline, NON-NEGOTIABLE). Every behavioral change ships with unit tests in `tests/Buildout.UnitTests` and, for any change crossing an external boundary, integration tests in `tests/Buildout.IntegrationTests`. Tests are written before the code that satisfies them.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Paths follow the existing project structure: `src/Buildout.Core/`, `src/Buildout.Mcp/`, `src/Buildout.Cli/`, `tests/Buildout.UnitTests/`, `tests/Buildout.IntegrationTests/`.

---

## Phase 1: Setup (Core Abstractions)

**Purpose**: Create the zero-dependency types and interfaces in `Buildout.Core` that all user stories depend on. These files have no cross-dependencies and can all be written in parallel.

- [X] T001 [P] Create `AuthMode` enum in `src/Buildout.Core/Auth/AuthMode.cs` — values: `None = 0`, `Passthrough = 1`, `Proxy = 2`, `Mapped = 3`
- [X] T002 [P] Create `AuthResult` record in `src/Buildout.Core/Auth/AuthResult.cs` — fields: `IsAuthenticated`, `ResolvedBotToken`, `TokenIdentity`, `ErrorMessage`; include static factory methods `Success(string botToken, string? identity)` and `Failure(string error)`
- [X] T003 [P] Create `IRequestAuthenticator` interface in `src/Buildout.Core/Auth/IRequestAuthenticator.cs` — single method: `Task<AuthResult> AuthenticateAsync(string? authorizationHeader)`
- [X] T004 [P] Create `AuthOptions` class in `src/Buildout.Core/Auth/AuthOptions.cs` — properties: `Mode` (AuthMode, default None), `Provider` (string?), `SqlitePath` (string?), `ConnectionString` (string?)
- [X] T005 [P] Create `AuthOptionsValidator` class in `src/Buildout.Core/Auth/AuthOptionsValidator.cs` implementing `IValidateOptions<AuthOptions>` — validate mode-dependent provider/path/connection-string per `contracts/configuration-schema.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Infrastructure shared by ALL authorization modes. Must complete before any user story work begins.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Write unit tests for `AuthOptionsValidator` in `tests/Buildout.UnitTests/Auth/AuthOptionsValidatorTests.cs` — cover all validation rules: valid modes, proxy/mapped require provider, sqlite requires path, postgresql requires connection string, none/passthrough ignore provider fields
- [X] T007 [P] Create `ContextualTokenProvider` in `src/Buildout.Core/Buildin/Authentication/ContextualTokenProvider.cs` — extends `BaseBearerTokenAuthenticationProvider`, uses `AsyncLocal<string?>` for per-request token override, `OverrideToken(string)` returns `IDisposable` scope, falls back to default token when no override is active
- [X] T008 Write unit tests for `ContextualTokenProvider` in `tests/Buildout.UnitTests/Auth/ContextualTokenProviderTests.cs` — verify default token used when no override, override token used within scope, override cleared after dispose, nested overrides restore correctly, concurrent requests isolated
- [X] T009 [P] Add `AuthIdentity` property to `AuditEntry` record in `src/Buildout.Core/Audit/AuditEntry.cs` — add `string? AuthIdentity` field (nullable, for MCP token identity; empty for none/passthrough modes)
- [X] T010 [P] Update `AuditTrailFilter` in `src/Buildout.Mcp/Audit/AuditTrailFilter.cs` to read `AuthIdentity` from `HttpContext.Items["AuthIdentity"]` when constructing `AuditEntry`
- [X] T011 Create `AuthFilter` in `src/Buildout.Mcp/Auth/AuthFilter.cs` — implements `IConfigureOptions<McpServerOptions>` (same pattern as `AuditTrailFilter`), reads `Authorization` header via `IHttpContextAccessor`, calls `IRequestAuthenticator.AuthenticateAsync`, on success calls `ContextualTokenProvider.OverrideToken` and sets `HttpContext.Items["AuthIdentity"]`, on failure returns 401
- [X] T012 Create `AuthMcpServiceExtensions` in `src/Buildout.Mcp/Auth/AuthMcpServiceExtensions.cs` — `AddAuth(this IServiceCollection, IConfiguration, bool)` extension method: binds `AuthOptions`, registers `AuthOptionsValidator`, registers mode-specific `IRequestAuthenticator`, registers `AuthFilter` as `IConfigureOptions<McpServerOptions>`, conditionally registers FluentMigrator runner and `ITokenStore` for proxy/mapped modes
- [X] T013 Update `src/Buildout.Mcp/Program.cs` — call `AddAuth(mergedConfig, isHttpTransport)` after `AddAuditTrail`, broaden migration gate to `if (isHttpTransport && (auditOptions.Enabled || authNeedsDb))` where `authNeedsDb` is true for proxy/mapped modes
- [X] T014 Update `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs` — replace `BotTokenAuthenticationProvider` registration with `ContextualTokenProvider`, conditionally register `NullPageReadCache` when auth mode is `passthrough` or `mapped` (per research R5)

**Checkpoint**: Foundation ready — auth pipeline is wired, all core types exist, audit integration complete. User story implementation can begin.

---

## Phase 3: User Story 1 — Single Global Key, No MCP Auth (Priority: P1)

**Goal**: Preserve current behavior as the default `none` mode — all MCP requests use the global BotToken with no authentication.

**Independent Test**: Start MCP server with `Auth:Mode=none` (default). Call any tool without credentials. Verify request succeeds using the configured global BotToken.

### Tests for User Story 1

- [X] T015 [P] [US1] Write unit tests for `NoneAuthenticator` in `tests/Buildout.UnitTests/Auth/NoneAuthenticatorTests.cs` — verify success with global BotToken when no header provided, success with global BotToken when header provided (header ignored)
- [X] T016 [US1] Implement `NoneAuthenticator` in `src/Buildout.Mcp/Auth/NoneAuthenticator.cs` — reads global BotToken from `IOptions<BuildinClientOptions>`, always returns `AuthResult.Success(globalBotToken, null)` regardless of authorization header
- [ ] T017 [US1] Write end-to-end integration test for `none` mode in `tests/Buildout.IntegrationTests/Auth/AuthModeEndToEndTests.cs` — verify MCP tool call succeeds without credentials in none mode

**Checkpoint**: User Story 1 complete — `none` mode is the default and matches existing behavior. Existing MCP tool tests must pass unchanged.

---

## Phase 4: User Story 2 — Passthrough Mode (Priority: P2)

**Goal**: Each MCP client provides its own Buildin Bot API key via `Authorization: Bearer` header. The server uses the provided key for outbound Buildin API calls.

**Independent Test**: Start server with `Auth:Mode=passthrough`. Call a tool with `Authorization: Bearer <key>`. Verify the provided key is used. Call without header. Verify rejection.

### Tests for User Story 2

- [X] T018 [P] [US2] Write unit tests for `PassthroughAuthenticator` in `tests/Buildout.UnitTests/Auth/PassthroughAuthenticatorTests.cs` — verify success when valid Bearer header provided, failure when header missing, success with any Bearer token (Buildin API validates the key downstream)
- [X] T019 [US2] Implement `PassthroughAuthenticator` in `src/Buildout.Mcp/Auth/PassthroughAuthenticator.cs` — extracts Bearer token from `Authorization` header, returns `AuthResult.Success(providedKey, null)` or `AuthResult.Failure("Authorization header required")` when missing
- [ ] T020 [US2] Add passthrough mode integration test to `tests/Buildout.IntegrationTests/Auth/AuthModeEndToEndTests.cs` — verify tool call succeeds with valid Bearer header, verify tool call rejected without header

**Checkpoint**: User Story 2 complete — passthrough mode enables per-request key attribution.

---

## Phase 5: User Story 3 — Token Proxy Mode (Priority: P3)

**Goal**: Operator issues MCP tokens to clients. Server validates MCP tokens and routes all requests through a shared global BotToken.

**Independent Test**: Configure `Auth:Mode=proxy` with SQLite. Create an MCP token. Call a tool with the token. Verify success with global key. Call without token. Verify rejection. Revoke the token. Verify rejection.

### Tests for User Story 3

- [X] T021 [P] [US3] Write unit tests for `TokenHasher` in `tests/Buildout.UnitTests/Auth/TokenHashingTests.cs` — verify hash is 64-char lowercase hex, verify `Verify` returns true for matching token, false for wrong token, timing-safe comparison
- [X] T022 [P] [US3] Create `AuthTestFixture` in `tests/Buildout.IntegrationTests/Auth/AuthTestFixture.cs` — shared fixture providing temporary SQLite database and PostgreSQL Testcontainer, following pattern from `tests/Buildout.IntegrationTests/Audit/AuditTestFixture.cs`
- [X] T023 [US3] Write integration tests for SQLite `TokenStore` in `tests/Buildout.IntegrationTests/Auth/SqliteTokenStoreTests.cs` — cover CreateToken, ListTokens, RevokeToken, ValidateToken (active, revoked, nonexistent)
- [ ] T024 [US3] Write integration tests for PostgreSQL `TokenStore` in `tests/Buildout.IntegrationTests/Auth/PostgresTokenStoreTests.cs` — same test surface as SQLite using Testcontainers
- [ ] T025 [US3] Write migration verification tests in `tests/Buildout.IntegrationTests/Auth/MigrationTests.cs` — verify Migration_002 creates `buildin_keys` and `mcp_tokens` tables with correct schema for both SQLite and PostgreSQL
- [X] T026 [US3] Write unit tests for `ProxyAuthenticator` in `tests/Buildout.UnitTests/Auth/ProxyAuthenticatorTests.cs` — verify success with valid MCP token returning global BotToken, failure when header missing, failure when token invalid, failure when token revoked
- [X] T027 [US3] Create `TokenHasher` in `src/Buildout.Mcp/Auth/TokenHasher.cs` — static class with `Hash(string) → string` (SHA-256, lowercase hex) and `Verify(string token, string storedHash) → bool` (using `CryptographicOperations.FixedTimeEquals`)
- [X] T028 [US3] Create `ITokenStore` interface with `McpTokenRecord` and `BuildinKeyRecord` records in `src/Buildout.Mcp/Auth/ITokenStore.cs` — per `contracts/token-store-interface.md`
- [X] T029 [US3] Implement `AdoNetTokenStore` in `src/Buildout.Mcp/Auth/AdoNetTokenStore.cs` — ADO.NET implementation following `AdoNetAuditTrail` pattern, dual SQLite/PostgreSQL write paths, implements all `ITokenStore` methods
- [X] T030 [US3] Create `Migration_002_CreateAuthTables` in `src/Buildout.Mcp/Auth/Migrations/Migration_002_CreateAuthTables.cs` — FluentMigrator `[Migration(2)]`: create `buildin_keys` and `mcp_tokens` tables, add `auth_identity` column to `audit_entries`, create indexes per `data-model.md`; use `IfDatabase("Postgres")` for PostgreSQL type adjustments
- [X] T031 [US3] Implement `ProxyAuthenticator` in `src/Buildout.Mcp/Auth/ProxyAuthenticator.cs` — validates MCP token via `ITokenStore.ValidateTokenAsync`, returns global BotToken from `IOptions<BuildinClientOptions>` on success
- [ ] T032 [US3] Add proxy mode integration test to `tests/Buildout.IntegrationTests/Auth/AuthModeEndToEndTests.cs` — full pipeline test: create token, call tool with token (success), call without token (rejected), revoke token, call with revoked token (rejected)

**Checkpoint**: User Story 3 complete — proxy mode provides token-based access control with a shared BotToken.

---

## Phase 6: User Story 4 — Token Mapped Mode (Priority: P4)

**Goal**: Each MCP token is mapped to a different Buildin Bot API key. Server validates the MCP token and resolves the mapped Buildin key for outbound requests.

**Independent Test**: Configure `Auth:Mode=mapped`. Create two Buildin keys and two MCP tokens mapped to different keys. Call tools with each token. Verify each uses its mapped key.

### Tests for User Story 4

- [ ] T033 [P] [US4] Write unit tests for `MappedAuthenticator` in `tests/Buildout.UnitTests/Auth/MappedAuthenticatorTests.cs` — verify success with valid mapped token returning mapped key, failure when token unmapped (no BuildinKeyId), failure when token invalid

### Implementation for User Story 4

- [ ] T034 [US4] Implement `MappedAuthenticator` in `src/Buildout.Mcp/Auth/MappedAuthenticator.cs` — validates MCP token via `ITokenStore.ValidateTokenAsync`, resolves mapped Buildin key via `buildin_key_id` FK, returns `AuthResult.Failure("Token has no mapped Buildin key")` when mapping missing
- [ ] T035 [US4] Add mapped mode integration test to `tests/Buildout.IntegrationTests/Auth/AuthModeEndToEndTests.cs` — full pipeline test: create two keys + two mapped tokens, verify each token resolves to its mapped key, verify unmapped token rejected

**Checkpoint**: User Story 4 complete — mapped mode provides per-token Buildin key isolation.

---

## Phase 7: User Story 5 — Token Lifecycle Management (Priority: P4)

**Goal**: CLI commands for creating, listing, revoking MCP tokens, managing Buildin Bot API keys, and managing token-to-key mappings.

**Independent Test**: Use `buildout-cli auth token create --name test`, verify token is displayed. Use `auth token list`, verify token appears. Use `auth token revoke --id <id>`, verify token revoked. Use `auth key create --name my-key --key <value>`, verify key stored. Use `auth key list`, verify key appears. Use `auth token map --token-id <id> --key-id <id>`, verify mapping created.

### Tests for User Story 5

- [ ] T036 [P] [US5] Write CLI auth command tests in `tests/Buildout.IntegrationTests/Auth/CliAuthTokenTests.cs` — cover token create (outputs raw token), token list (displays active tokens), token revoke (marks token revoked), key create (stores Buildin key), key list (displays keys), token map (links token to key), and error cases (mode not proxy/mapped, token not found, key not found)

### Implementation for User Story 5

- [ ] T037 [P] [US5] Create `AuthSettings` base class in `src/Buildout.Cli/Commands/AuthSettings.cs` — extends `BuildoutCommandSettings`, marker class for the `auth` branch
- [ ] T038 [US5] Implement `AuthTokenCreateCommand` in `src/Buildout.Cli/Commands/AuthTokenCreateCommand.cs` — `AsyncCommand<AuthTokenCreateSettings>`, calls `ITokenStore.CreateTokenAsync`, displays raw token value (shown only once)
- [ ] T039 [US5] Implement `AuthTokenListCommand` in `src/Buildout.Cli/Commands/AuthTokenListCommand.cs` — `AsyncCommand<AuthTokenListSettings>`, calls `ITokenStore.ListTokensAsync`, displays table with name, id, created date, status (active/revoked)
- [ ] T040 [US5] Implement `AuthTokenRevokeCommand` in `src/Buildout.Cli/Commands/AuthTokenRevokeCommand.cs` — `AsyncCommand<AuthTokenRevokeSettings>`, calls `ITokenStore.RevokeTokenAsync`, displays confirmation
- [ ] T041 [US5] Implement `AuthTokenMapCommand` in `src/Buildout.Cli/Commands/AuthTokenMapCommand.cs` — `AsyncCommand<AuthTokenMapSettings>`, calls `ITokenStore.MapTokenAsync`, displays confirmation; validates current auth mode is `mapped`
- [ ] T042 [P] [US5] Implement `AuthKeyCreateCommand` in `src/Buildout.Cli/Commands/AuthKeyCreateCommand.cs` — `AsyncCommand<AuthKeyCreateSettings>`, calls `ITokenStore.CreateBuildinKeyAsync`, displays confirmation with key ID; validates current auth mode is `mapped`
- [ ] T043 [P] [US5] Implement `AuthKeyListCommand` in `src/Buildout.Cli/Commands/AuthKeyListCommand.cs` — `AsyncCommand<AuthKeyListSettings>`, calls `ITokenStore.ListBuildinKeysAsync`, displays table with name, id, created date
- [ ] T044 [US5] Register `auth` branch and commands in `src/Buildout.Cli/Program.cs` — add `config.AddBranch<AuthSettings>("auth", auth => { auth.AddCommand<AuthTokenCreateCommand>("create"); ... })` including key-create and key-list, register `ITokenStore` in CLI DI container

**Checkpoint**: User Story 5 complete — full token lifecycle management via CLI.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Logging, documentation, performance validation, skill files, and final checks.

- [ ] T045 [P] Add auth failure logging to all authenticators — in `NoneAuthenticator`, `PassthroughAuthenticator`, `ProxyAuthenticator`, `MappedAuthenticator`: log authentication failures (mode, error) via `ILogger` without exposing token values or secrets; verify in corresponding unit tests that no token/secret appears in log output (FR-014)
- [ ] T046 [P] Update `docs/configuration.md` — add `Auth:*` configuration keys table, JSON examples for all four modes, environment variable examples, shared-database note for combined audit+auth deployments
- [ ] T047 [P] Write performance tests for SC-003 and SC-004 in `tests/Buildout.IntegrationTests/Auth/AuthPerformanceTests.cs` — benchmark token CRUD operations for 100 tokens (assert < 1s total per SC-003), benchmark `ValidateTokenAsync` latency over 100 iterations (assert < 5ms per call per SC-004)
- [ ] T048 [P] Create skill files for `auth` CLI branch in `src/Buildout.Cli/Skills/Auth/` — `SKILL.md` for each auth subcommand (token-create, token-list, token-revoke, token-map, key-create, key-list) per Agent Skills specification; satisfies Principle VIII
- [ ] T049 Run quickstart.md validation — verify all four scenarios from `specs/014-mcp-authorization/quickstart.md` work end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2
- **User Story 2 (Phase 4)**: Depends on Phase 2 (can proceed in parallel with US1)
- **User Story 3 (Phase 5)**: Depends on Phase 2 (can proceed in parallel with US1/US2)
- **User Story 4 (Phase 6)**: Depends on Phase 5 (reuses TokenStore infrastructure)
- **User Story 5 (Phase 7)**: Depends on Phase 5 (CLI commands need TokenStore)
- **Polish (Phase 8)**: Depends on all user stories

### User Story Dependencies

- **US1 (P1)**: No story dependencies — only Phase 2
- **US2 (P2)**: No story dependencies — only Phase 2
- **US3 (P3)**: No story dependencies — only Phase 2
- **US4 (P4)**: Depends on US3 (reuses TokenStore, AdoNetTokenStore, Migration_002)
- **US5 (P4)**: Depends on US3 (CLI commands operate on TokenStore)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Core abstractions before authenticators
- Authenticators before integration tests
- Story complete before moving to next priority

### Parallel Opportunities

- All Phase 1 tasks (T001–T005) can run in parallel
- T007 and T009/T010 in Phase 2 can run in parallel
- US1, US2, US3 can run in parallel after Phase 2 completes
- Within US3: T021, T022 can run in parallel
- US4 and US5 can run in parallel (both depend on US3 but not on each other)
- Within US5: T042 (key-create) and T043 (key-list) can run in parallel
- Phase 8 tasks (T045–T049) can all run in parallel

---

## Parallel Example: Phase 1

```
T001: "Create AuthMode enum in src/Buildout.Core/Auth/AuthMode.cs"
T002: "Create AuthResult record in src/Buildout.Core/Auth/AuthResult.cs"
T003: "Create IRequestAuthenticator interface in src/Buildout.Core/Auth/IRequestAuthenticator.cs"
T004: "Create AuthOptions class in src/Buildout.Core/Auth/AuthOptions.cs"
T005: "Create AuthOptionsValidator in src/Buildout.Core/Auth/AuthOptionsValidator.cs"
```

## Parallel Example: User Stories 1, 2, 3

```
US1: T015 → T016 → T017
US2: T018 → T019 → T020
US3: T021+T022 (parallel) → T023+T024+T025+T026 (parallel) → T027→T028→T029→T030→T031→T032
US5: T037 → T038+T039+T040+T041+T042+T043 (parallel after AuthSettings) → T044
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (none mode)
4. **STOP and VALIDATE**: Run all existing MCP tool tests — they must pass unchanged in `none` mode
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (none mode) → Test independently → Deploy (MVP — zero behavior change)
3. Add US2 (passthrough) → Test independently → Deploy
4. Add US3 (proxy) → Test independently → Deploy (token-based access control)
5. Add US4 (mapped) + US5 (CLI + key management) → Test independently → Deploy (full feature set)
6. Polish → Logging, performance tests, skill files, documentation, quickstart validation

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (none mode)
   - Developer B: US2 (passthrough)
   - Developer C: US3 (proxy + token store)
3. After US3 complete:
   - Developer C: US4 (mapped)
   - Developer D: US5 (CLI commands)
4. All: Polish together

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- `NoneAuthenticator` registration is the default — when `Auth:Mode` is `None` or unset
- `ContextualTokenProvider` replaces `BotTokenAuthenticationProvider` in DI — `OverrideToken` is never called in `none` mode, preserving identical behavior
- Migration_002 follows Migration_001 numbering from feature 013; both run via the shared FluentMigrator runner
- Cache is disabled (`NullPageReadCache`) in `passthrough` and `mapped` modes to prevent cross-workspace data leakage (research R5)
- `AuditEntry.AuthIdentity` extension is cross-cutting: auth filter writes it, audit filter reads it
- CLI `auth` branch follows the existing `db` and `skills` branch patterns in `Program.cs`
- `auth key create` and `auth key list` commands are required by FR-009 for managing Buildin Bot API keys in `mapped` mode
- All authenticators include `ILogger`-based failure logging that never exposes token values or secrets (FR-014)
