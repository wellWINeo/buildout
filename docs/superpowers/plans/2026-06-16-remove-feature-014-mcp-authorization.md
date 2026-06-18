# Remove Feature 014 (MCP Authorization) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completely remove feature 014 (MCP authorization: modes none/passthrough/proxy/mapped, token registry, CLI auth commands) and revert all shared files to their pre-014 behavior, leaving the build and full test suite green.

**Architecture:** Feature 014 added a per-request authentication layer on top of the MCP HTTP transport (an `IRequestAuthenticator` abstraction with four modes, a SQLite/PostgreSQL token registry, CLI management commands, and an `AuthIdentity` field threaded into the 013 audit trail). Removal is a clean revert: the Buildin client returns to the static `BotTokenAuthenticationProvider`, the read cache is unconditionally enabled per `CacheOptions`, and the audit entry drops `AuthIdentity`. Because `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true`, the build fails on unused `using` directives — so we first **detach** the shared files from the auth types (build stays green while auth types still exist but are unreferenced), then **delete** the auth code and tests, then fix the skill-count tests and docs.

**Tech Stack:** .NET 10, C#, xUnit, Spectre.Console.Cli, FluentMigrator, Microsoft.Kiota auth abstractions. Solution: `buildout.slnx`. CI commands (from `.github/workflows/ci.yml`): `dotnet build -c Release`, `dotnet test tests/Buildout.UnitTests -c Release --no-build`, `dotnet test tests/Buildout.IntegrationTests -c Release --no-build`.

---

## Decisions (locked with user)

- **Migration:** Delete `Migration_002_CreateAuthTables` outright. Fresh databases run only Migration 001. The FluentMigrator runner auto-discovers migrations via `.ScanIn(...assembly).For.Migrations()`, so deleting the file removes it from discovery with no further wiring change. (Existing local dev SQLite DBs already at version 2 keep orphaned `mcp_tokens`/`buildin_keys` tables and an unused `auth_identity` column — harmless; no production DB exists.)
- **Spec directory:** Delete `specs/014-mcp-authorization/` entirely.

## Pre-flight verification (facts this plan relies on)

- `auth_identity` is **never persisted**: `AdoNetAuditTrail.cs` INSERTs only `(id, tool_name, session_id, timestamp, parameters, outcome, duration_ms, error_details)`. The `AuthIdentity` field is set by the filter but never written to the DB column, so removing it needs no audit-store change.
- The only auth references in shared/non-014 files are: `ServiceCollectionExtensions.cs`, `AuditEntry.cs`, `AuditTrailFilter.cs`, MCP `Program.cs`, CLI `Program.cs`, the two Skills tests, `Buildout.Cli.csproj`, and three lines of `specs/013-audit-trails/spec.md`.
- `BotTokenAuthenticationProvider` (the pre-014 static provider) still exists at `src/Buildout.Core/Buildin/Authentication/BotTokenAuthenticationProvider.cs` and is used by three integration-test fixtures, so reverting to it is safe.
- No `appsettings*.json` references an `Auth` section; the README has no auth content; `feature.json`/`CLAUDE.md` already point to feature 015 (not 014), so they need no change.
- `tests/Buildout.IntegrationTests/Audit/MigrationTests.cs` only asserts Migration 001's schema (`audit_entries`, `VersionInfo`, three indexes); it does not assert auth tables or a migration count, so it stays green after Migration 002 is deleted.

---

## Task 0: Create the removal branch

The repository is on `main`. Per the project git rules, do not commit to `main`. Confirm with the user whether to use a git worktree or an in-place branch, then create it.

- [ ] **Step 1: Create the branch (in-place example)**

```bash
git checkout -b chore/remove-feature-014-mcp-authorization
```

- [ ] **Step 2: Confirm clean starting state**

Run: `git status`
Expected: on `chore/remove-feature-014-mcp-authorization`, working tree clean.

---

## Task 1: Revert the Buildin client to the static token provider

**Files:**
- Modify: `src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs`

After this task the file no longer references any `Buildout.Core.Auth` type. `ContextualTokenProvider` still exists (deleted in Task 5) but is no longer referenced here.

- [ ] **Step 1: Remove the unused Auth using directive**

Delete line 1:

```csharp
using Buildout.Core.Auth;
```

(Keep `using Buildout.Core.Buildin.Authentication;` — both `ContextualTokenProvider` and `BotTokenAuthenticationProvider` live in that namespace.)

- [ ] **Step 2: Swap the authentication provider registration**

Replace:

```csharp
        services.AddSingleton<IAuthenticationProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<BuildinClientOptions>>().Value;
            return new ContextualTokenProvider(opts.BotToken);
        });
```

with:

```csharp
        services.AddSingleton<IAuthenticationProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<BuildinClientOptions>>().Value;
            return new BotTokenAuthenticationProvider(opts.BotToken);
        });
```

- [ ] **Step 3: Remove the AuthMode-driven cache-disable logic**

Replace:

```csharp
        var authModeEnum = configuration.GetValue<string>("Auth:Mode");
        var authMode = string.IsNullOrEmpty(authModeEnum) ? AuthMode.None : Enum.Parse<AuthMode>(authModeEnum, true);

        services.AddSingleton<IPageReadCache>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
            var disableCache = authMode is AuthMode.Passthrough or AuthMode.Mapped;
            return (opts.Enabled && !disableCache) ? new PageReadCache(opts) : new NullPageReadCache();
        });
```

with:

```csharp
        services.AddSingleton<IPageReadCache>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
            return opts.Enabled ? new PageReadCache(opts) : new NullPageReadCache();
        });
```

- [ ] **Step 4: Build to verify green**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 warnings, 0 errors. (A leftover unused `using` would fail here because warnings are errors.)

- [ ] **Step 5: Run unit tests**

Run: `dotnet test tests/Buildout.UnitTests -c Release --no-build`
Expected: PASS (auth code still present, so auth unit tests still pass).

- [ ] **Step 6: Commit**

```bash
git add src/Buildout.Core/DependencyInjection/ServiceCollectionExtensions.cs
git commit -m "refactor: revert Buildin client to static BotTokenAuthenticationProvider"
```

---

## Task 2: Drop AuthIdentity from the audit trail

**Files:**
- Modify: `src/Buildout.Core/Audit/AuditEntry.cs`
- Modify: `src/Buildout.Mcp/Audit/AuditTrailFilter.cs`

- [ ] **Step 1: Remove the AuthIdentity property from AuditEntry**

Delete line 14:

```csharp
    public string? AuthIdentity { get; init; }
```

- [ ] **Step 2: Remove the AuthIdentity read and assignment in the filter**

In `AuditTrailFilter.cs`, delete this line from the `finally` block:

```csharp
                var authIdentity = _httpContextAccessor.HttpContext?.Items["AuthIdentity"] as string;
```

and delete this line from the `new AuditEntry { ... }` initializer:

```csharp
                    AuthIdentity = authIdentity,
```

(`_httpContextAccessor` is still used for `sessionId`, so it stays.)

- [ ] **Step 3: Build to verify green**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run the integration audit tests**

Run: `dotnet test tests/Buildout.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~Audit"`
Expected: PASS (audit tests never referenced `AuthIdentity`; Postgres/Docker tests skip gracefully when Docker is unavailable).

- [ ] **Step 5: Commit**

```bash
git add src/Buildout.Core/Audit/AuditEntry.cs src/Buildout.Mcp/Audit/AuditTrailFilter.cs
git commit -m "refactor: remove AuthIdentity from audit entries"
```

---

## Task 3: Remove auth wiring from the MCP host

**Files:**
- Modify: `src/Buildout.Mcp/Program.cs`

- [ ] **Step 1: Remove the Auth using directive**

Delete line 4:

```csharp
using Buildout.Mcp.Auth;
```

- [ ] **Step 2: Remove the AddAuth call**

Delete this line (and the trailing whitespace-only line directly after it):

```csharp
    builder.Services.AddAuth(mergedConfig, isHttpTransport);
```

- [ ] **Step 3: Remove AuthNeedsDb and simplify the migration condition**

Delete:

```csharp
    var authNeedsDb = AuthMcpServiceExtensions.AuthNeedsDb(mergedConfig);

```

Replace:

```csharp
    if (isHttpTransport && (auditOptions.Enabled || authNeedsDb))
```

with:

```csharp
    if (isHttpTransport && auditOptions.Enabled)
```

- [ ] **Step 4: Build to verify green**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors. (`AuthMcpServiceExtensions` still exists but is now unreferenced — public types produce no unused warning.)

- [ ] **Step 5: Commit**

```bash
git add src/Buildout.Mcp/Program.cs
git commit -m "refactor: remove MCP auth wiring from host startup"
```

---

## Task 4: Remove auth wiring and commands from the CLI host

**Files:**
- Modify: `src/Buildout.Cli/Program.cs`

- [ ] **Step 1: Remove the now-unused Logging using directive**

Delete line 8:

```csharp
using Microsoft.Extensions.Logging;
```

(Its only use is `ILogger<...AdoNetTokenStore>` in the auth block removed below. `services.AddLogging()` comes from `Microsoft.Extensions.DependencyInjection`, which stays.)

The auth block is also the only consumer of `using Microsoft.Extensions.Configuration;` (via `config.GetValue<string>("Auth:...")`). After removing the block (Steps 2–3), the Step 4 build will report `CS8019`/`IDE0005` "unnecessary using directive" for it because warnings are errors. When that happens, delete line 6 as well:

```csharp
using Microsoft.Extensions.Configuration;
```

(`BuildoutConfiguration.Build` is in the `Buildout.Configuration` namespace, and `AddBuildinClient`/`AddBuildoutCore` are extension methods from `Buildout.Core.DependencyInjection` — neither needs this using.)

- [ ] **Step 2: Remove the ITokenStore registration block**

Delete the entire block (the `authProvider` lookup and both `if/else if` branches):

```csharp
    var authProvider = config.GetValue<string>("Auth:Provider")?.ToLowerInvariant();
    if (authProvider == "sqlite")
    {
        var sqlitePath = config.GetValue<string>("Auth:SqlitePath");
        var authCs = $"Data Source={sqlitePath}";
        services.AddSingleton<Buildout.Mcp.Auth.ITokenStore>(sp =>
            new Buildout.Mcp.Auth.AdoNetTokenStore(authCs, "sqlite",
                sp.GetRequiredService<ILogger<Buildout.Mcp.Auth.AdoNetTokenStore>>()));
    }
    else if (authProvider == "postgresql")
    {
        var authCs = config.GetValue<string>("Auth:ConnectionString")!;
        services.AddSingleton<Buildout.Mcp.Auth.ITokenStore>(sp =>
            new Buildout.Mcp.Auth.AdoNetTokenStore(authCs, "postgresql",
                sp.GetRequiredService<ILogger<Buildout.Mcp.Auth.AdoNetTokenStore>>()));
    }
```

- [ ] **Step 3: Remove the `auth` command branch**

Delete the entire branch from the `app.Configure` block:

```csharp
        config.AddBranch<AuthSettings>("auth", auth =>
        {
            auth.AddBranch<AuthSettings>("token", token =>
            {
                token.AddCommand<AuthTokenCreateCommand>("create");
                token.AddCommand<AuthTokenListCommand>("list");
                token.AddCommand<AuthTokenRevokeCommand>("revoke");
                token.AddCommand<AuthTokenMapCommand>("map");
            });
            auth.AddBranch<AuthSettings>("key", key =>
            {
                key.AddCommand<AuthKeyCreateCommand>("create");
                key.AddCommand<AuthKeyListCommand>("list");
            });
        });
```

- [ ] **Step 4: Build to verify green**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors. (The CLI no longer references `Buildout.Mcp` in code; the project reference is removed in Task 6.)

- [ ] **Step 5: Commit**

```bash
git add src/Buildout.Cli/Program.cs
git commit -m "refactor: remove auth commands and token-store wiring from CLI"
```

---

## Task 5: Delete the auth source and auth tests

No remaining code references these types after Tasks 1–4, so deletion compiles cleanly.

**Files (delete):**
- `src/Buildout.Core/Auth/` (whole directory: `AuthMode.cs`, `AuthOptions.cs`, `AuthOptionsValidator.cs`, `AuthResult.cs`, `IRequestAuthenticator.cs`)
- `src/Buildout.Core/Buildin/Authentication/ContextualTokenProvider.cs`
- `src/Buildout.Mcp/Auth/` (whole directory: `AdoNetTokenStore.cs` incl. `TokenHasher`, `AuthFilter.cs`, `AuthMcpServiceExtensions.cs`, `ITokenStore.cs`, `MappedAuthenticator.cs`, `NoneAuthenticator.cs`, `PassthroughAuthenticator.cs`, `ProxyAuthenticator.cs`, `Migrations/Migration_002_CreateAuthTables.cs`)
- `src/Buildout.Cli/Commands/AuthSettings.cs`, `AuthKeyCreateCommand.cs`, `AuthKeyListCommand.cs`, `AuthTokenCreateCommand.cs`, `AuthTokenListCommand.cs`, `AuthTokenMapCommand.cs`, `AuthTokenRevokeCommand.cs`
- `tests/Buildout.UnitTests/Auth/` (whole directory: `AuthOptionsValidatorTests.cs`, `ContextualTokenProviderTests.cs`, `NoneAuthenticatorTests.cs`, `PassthroughAuthenticatorTests.cs`, `ProxyAuthenticatorTests.cs`, `TokenHasherTests.cs`)
- `tests/Buildout.IntegrationTests/Auth/` (whole directory: `CliAuthTokenTests.cs`, `SqliteTokenStoreTests.cs`)

- [ ] **Step 1: Remove the directories and files with git**

```bash
git rm -r \
  src/Buildout.Core/Auth \
  src/Buildout.Core/Buildin/Authentication/ContextualTokenProvider.cs \
  src/Buildout.Mcp/Auth \
  src/Buildout.Cli/Commands/AuthSettings.cs \
  src/Buildout.Cli/Commands/AuthKeyCreateCommand.cs \
  src/Buildout.Cli/Commands/AuthKeyListCommand.cs \
  src/Buildout.Cli/Commands/AuthTokenCreateCommand.cs \
  src/Buildout.Cli/Commands/AuthTokenListCommand.cs \
  src/Buildout.Cli/Commands/AuthTokenMapCommand.cs \
  src/Buildout.Cli/Commands/AuthTokenRevokeCommand.cs \
  tests/Buildout.UnitTests/Auth \
  tests/Buildout.IntegrationTests/Auth
```

- [ ] **Step 2: Build to verify green**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run the full unit suite**

Run: `dotnet test tests/Buildout.UnitTests -c Release --no-build`
Expected: PASS. (Auth unit tests are gone; the skills tests still pass because the auth `.md` files are still embedded — removed in Task 7.)

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor: delete feature 014 auth source and tests"
```

---

## Task 6: Drop the CLI → Mcp project reference

After Task 4 the CLI no longer uses any `Buildout.Mcp` type. The reference was added by feature 014; remove it.

**Files:**
- Modify: `src/Buildout.Cli/Buildout.Cli.csproj`

- [ ] **Step 1: Remove the project reference**

Delete this line from the `<ItemGroup>` of project references:

```xml
    <ProjectReference Include="..\Buildout.Mcp\Buildout.Mcp.csproj" />
```

- [ ] **Step 2: Build to verify green**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors. (If the build fails with an unresolved `Buildout.Mcp` reference, a code reference was missed in Task 4 — fix it before continuing.)

- [ ] **Step 3: Commit**

```bash
git add src/Buildout.Cli/Buildout.Cli.csproj
git commit -m "chore: drop CLI reference to Buildout.Mcp"
```

---

## Task 7: Delete the auth skills and fix the skill-count tests

Removing the six `auth-*.md` embedded skill files changes the installed skill count from 15 to 9, which breaks the two skill-install tests. Delete the files and update the assertions in the same commit so the suite stays green.

**Files:**
- Delete: `src/Buildout.Cli/Skills/auth-key-create.md`, `auth-key-list.md`, `auth-token-create.md`, `auth-token-list.md`, `auth-token-map.md`, `auth-token-revoke.md`
- Modify: `tests/Buildout.IntegrationTests/Cli/SkillsCommandIntegrationTests.cs`
- Modify: `tests/Buildout.UnitTests/Cli/SkillsInstallCommandTests.cs`

(Remaining skills after deletion: `SKILL.md`, `create.md`, `database-views.md`, `delete.md`, `read.md`, `restore.md`, `search.md`, `tree.md`, `update.md` = 9 files.)

- [ ] **Step 1: Delete the auth skill files**

```bash
git rm \
  src/Buildout.Cli/Skills/auth-key-create.md \
  src/Buildout.Cli/Skills/auth-key-list.md \
  src/Buildout.Cli/Skills/auth-token-create.md \
  src/Buildout.Cli/Skills/auth-token-list.md \
  src/Buildout.Cli/Skills/auth-token-map.md \
  src/Buildout.Cli/Skills/auth-token-revoke.md
```

- [ ] **Step 2: Update SkillsCommandIntegrationTests.cs**

Rename the method on line 27 and update both count assertions (lines 39 and 60):

Replace `public async Task Install_Claude_Local_WritesFifteenSkillFiles()` with `public async Task Install_Claude_Local_WritesNineSkillFiles()`.

Replace both occurrences of:

```csharp
            Assert.Equal(15, Directory.GetFiles(skillsDir).Length);
```

with:

```csharp
            Assert.Equal(9, Directory.GetFiles(skillsDir).Length);
```

- [ ] **Step 3: Update SkillsInstallCommandTests.cs**

Rename the method on line 25 from `Install_Local_Claude_WritesFifteenFiles` to `Install_Local_Claude_WritesNineFiles`, and replace:

```csharp
        Assert.Equal(15, Directory.GetFiles(target).Length);
```

with:

```csharp
        Assert.Equal(9, Directory.GetFiles(target).Length);
```

- [ ] **Step 4: Build and run the skills tests**

Run: `dotnet build -c Release`
Then: `dotnet test tests/Buildout.UnitTests -c Release --no-build --filter "FullyQualifiedName~Skills"`
And: `dotnet test tests/Buildout.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~Skills"`
Expected: PASS with the new count of 9.

- [ ] **Step 5: Commit**

```bash
git add src/Buildout.Cli/Skills tests/Buildout.IntegrationTests/Cli/SkillsCommandIntegrationTests.cs tests/Buildout.UnitTests/Cli/SkillsInstallCommandTests.cs
git commit -m "chore: remove auth skills and update skill-count tests to 9"
```

---

## Task 8: Delete the 014 spec and revert the 013 spec edits

**Files:**
- Delete: `specs/014-mcp-authorization/` (whole directory)
- Modify: `specs/013-audit-trails/spec.md`

- [ ] **Step 1: Delete the 014 spec directory**

```bash
git rm -r specs/014-mcp-authorization
```

- [ ] **Step 2: Revert FR-001 in the 013 spec (line ~70)**

Replace:

```markdown
- **FR-001**: The system MUST record an audit entry for every MCP tool invocation over HTTP transport when audit trails are enabled, capturing: tool name, `Mcp-Session-Id`, timestamp (UTC), invocation parameters, outcome (success or failure), duration, error details on failure, and the authenticated MCP token identity (when authorization is enabled; empty when authorization is `none` or `passthrough`).
```

with:

```markdown
- **FR-001**: The system MUST record an audit entry for every MCP tool invocation over HTTP transport when audit trails are enabled, capturing: tool name, `Mcp-Session-Id`, timestamp (UTC), invocation parameters, outcome (success or failure), duration, and error details on failure.
```

- [ ] **Step 3: Revert the AuditEntry key-entity description (line ~88)**

Replace:

```markdown
- **AuditEntry**: Represents a single tool invocation record. Attributes: unique identifier, tool name, `Mcp-Session-Id` (from the protocol-native HTTP header), authenticated token identity (MCP token name/label when authorization is enabled; empty otherwise), timestamp (UTC), invocation parameters (serialized, truncated), outcome (success/failure), duration, error details (on failure).
```

with:

```markdown
- **AuditEntry**: Represents a single tool invocation record. Attributes: unique identifier, tool name, `Mcp-Session-Id` (from the protocol-native HTTP header), timestamp (UTC), invocation parameters (serialized, truncated), outcome (success/failure), duration, error details (on failure).
```

- [ ] **Step 4: Remove the 014-added clarification Q (line ~108)**

Delete this line entirely:

```markdown
- Q: Should audit entries reference the MCP token used for authentication? → A: Yes. Each audit entry must include the MCP token identity (token name/label) when authorization is enabled. This provides traceability to the authenticated caller, not the Buildin API key used behind the scenes.
```

- [ ] **Step 5: Commit**

```bash
git add specs/013-audit-trails/spec.md
git commit -m "docs: delete feature 014 spec and revert 013 audit-trail references"
```

---

## Task 9: Full verification

- [ ] **Step 1: Clean build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 2: Full unit suite**

Run: `dotnet test tests/Buildout.UnitTests -c Release --no-build`
Expected: PASS, 0 failures.

- [ ] **Step 3: Full integration suite**

Run: `dotnet test tests/Buildout.IntegrationTests -c Release --no-build`
Expected: PASS, 0 failures. (LLM tests skip without `OPENROUTER_API_KEY`; PostgreSQL/Testcontainers tests skip without Docker — both per project convention.)

- [ ] **Step 4: Grep sweep — confirm no auth references remain in code**

Run:

```bash
grep -rnE "AuthMode|IRequestAuthenticator|ContextualTokenProvider|ITokenStore|AdoNetTokenStore|AuthFilter|AuthIdentity|AuthMcpServiceExtensions|Migration_002|AuthSettings|AuthOptions" \
  src tests --include='*.cs' | grep -v '/bin/\|/obj/'
```

Expected: no output. (Note: `PassthroughPageContentProvider` and other "passthrough"/"auth"-substring names belong to the read-cache and recorder features and are unrelated; the pattern above is chosen to avoid matching them.)

- [ ] **Step 5: Confirm directories are gone**

```bash
test ! -d specs/014-mcp-authorization && test ! -d src/Buildout.Core/Auth && test ! -d src/Buildout.Mcp/Auth && test ! -d tests/Buildout.UnitTests/Auth && echo "OK: all auth dirs removed"
```

Expected: `OK: all auth dirs removed`

- [ ] **Step 6: Confirm only intended skill files remain (9)**

Run: `ls src/Buildout.Cli/Skills/*.md | wc -l`
Expected: `9`

---

## Notes / out of scope

- **Existing migrated dev databases:** Because Migration 002 is deleted rather than rolled back, any SQLite/PostgreSQL database already migrated to version 2 keeps the orphaned `mcp_tokens`/`buildin_keys` tables and the unused `audit_entries.auth_identity` column. This is harmless and intentional per the locked decision. If a clean schema is ever required for such a DB, drop those objects manually.
- **NuGet packages:** `Buildout.Mcp.csproj` keeps `FluentMigrator*`, `Microsoft.Data.Sqlite`, and `Npgsql` — all still used by the audit feature (013). No package removals.
- **No `appsettings`/README/`feature.json`/`CLAUDE.md` changes** are required (verified in pre-flight).
