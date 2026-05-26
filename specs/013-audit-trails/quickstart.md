# Quickstart: MCP Audit Trails

**Feature**: `013-audit-trails` | **Date**: 2025-05-25

## Scenario 1: Enable SQLite Audit Trails

**Goal**: A workspace operator enables audit trails with SQLite for local development and verifies tool invocations are recorded.

### Steps

1. Add an `Audit` section to `~/.config/buildout/config.json`:

```json
{
  "BotToken": "ntn_xxx",
  "Audit": {
    "Enabled": true,
    "Provider": "sqlite",
    "SqlitePath": "/tmp/buildout-audit.db"
  }
}
```

2. Start the MCP server with HTTP transport:

```bash
export Buildout__Transport__Type="http"
buildout-mcp
```

3. An LLM agent invokes `get_page_markdown` with `page_id=abc123`.

4. Query the audit database:

```bash
sqlite3 /tmp/buildout-audit.db "SELECT * FROM audit_entries ORDER BY timestamp DESC LIMIT 1;"
```

**Expected result**: One row with `tool_name='get_page_markdown'`, `outcome=0` (Success), non-null `session_id`, and serialized parameters containing `page_id`.

### Test Mapping

- **Unit**: `AuditOptionsValidatorTests` — validates SQLite config.
- **Integration**: `SqliteAuditTrailTests` — writes and reads an `AuditEntry` from a temp SQLite file.
- **Integration**: `AuditTrailFilterTests` — full MCP pipeline test that invokes a tool and verifies the audit entry appears in the database.

---

## Scenario 2: Enable PostgreSQL Audit Trails

**Goal**: A workspace operator configures PostgreSQL for team/production audit trail storage.

### Steps

1. Set environment variables:

```bash
export Buildout__Audit__Enabled="true"
export Buildout__Audit__Provider="postgresql"
export Buildout__Audit__ConnectionString="Host=db.example.com;Database=buildout_audit;Username=audit_writer;Password=$AUDIT_DB_PASSWORD"
```

2. Start the MCP server with HTTP transport.

3. Invoke any MCP tool.

4. Query PostgreSQL:

```sql
SELECT * FROM audit_entries ORDER BY timestamp DESC LIMIT 10;
```

**Expected result**: Rows persisted with correct tool names, session IDs, timestamps, and outcomes.

### Test Mapping

- **Unit**: `AuditOptionsValidatorTests` — validates PostgreSQL config.
- **Integration**: `PostgresAuditTrailTests` — writes and reads an `AuditEntry` (uses test container or embedded PostgreSQL).

---

## Scenario 3: Disabled by Default (Zero Impact)

**Goal**: Verify no audit overhead when the feature is not configured.

### Steps

1. Start the MCP server with no `Audit` section in configuration.
2. Invoke any MCP tool.
3. Verify no `/tmp/buildout-audit.db` or other audit artifacts exist.
4. Verify no database connections opened (process inspection).

**Expected result**: Tool behavior identical to a build without audit trail code. No files created, no connections opened.

### Test Mapping

- **Unit**: `AuditOptionsValidatorTests` — validates that disabled config returns `Success`.
- **Unit**: `NullAuditTrailTests` — verifies `RecordEntryAsync` completes synchronously with no side effects.
- **Integration**: Audit filter NOT registered when disabled — verified by checking DI container.

---

## Scenario 4: Audit Write Failure Does Not Block Tool Call

**Goal**: Verify tool calls succeed even when audit persistence fails.

### Steps

1. Configure SQLite with a read-only or invalid path.
2. Start the MCP server.
3. Invoke any MCP tool.
4. Verify tool returns a successful result.
5. Check server logs for audit write failure warning.

**Expected result**: Tool succeeds. Audit failure is logged but invisible to the tool caller.

### Test Mapping

- **Unit**: `SqliteAuditTrailTests` — simulates write failure and verifies it's caught and logged.
- **Integration**: `AuditTrailFilterTests` — mock `IAuditTrail` throws, verifies tool still succeeds.
