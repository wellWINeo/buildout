# Quickstart: MCP Authorization Modes

**Feature**: `014-mcp-authorization` | **Date**: 2025-05-27

## Scenario 1 — Default Mode (No Auth)

No configuration needed. The MCP server uses the global `BotToken` for all requests.

```bash
# The server starts with Auth:Mode=none by default
buildout-mcp
# or explicitly:
export Buildout__Auth__Mode="none"
buildout-mcp
```

All MCP tool calls succeed without credentials. The `Authorization` header is ignored if provided.

## Scenario 2 — Passthrough Mode

Each MCP client provides its own Buildin Bot API key.

```bash
export Buildout__Auth__Mode="passthrough"
buildout-mcp
```

Clients must include `Authorization: Bearer <buildin-bot-key>` with every request. Requests without the header are rejected.

## Scenario 3 — Proxy Mode (SQLite)

Operator issues MCP tokens; all requests use a shared Buildin Bot API key.

```bash
export Buildout__Auth__Mode="proxy"
export Buildout__Auth__Provider="sqlite"
export Buildout__Auth__SqlitePath="/var/lib/buildout/auth.db"
buildout-mcp
```

Create a token:

```bash
buildout-cli auth token create --name "alice"
# Output: Token created: mcp_a1b2c3d4... (save this — it won't be shown again)
```

Clients use the token:

```
Authorization: Bearer mcp_a1b2c3d4...
```

List tokens:

```bash
buildout-cli auth token list
```

Revoke a token:

```bash
buildout-cli auth token revoke --id <token-id>
```

## Scenario 4 — Mapped Mode (PostgreSQL)

Each MCP token maps to a different Buildin Bot API key.

```bash
export Buildout__Auth__Mode="mapped"
export Buildout__Auth__Provider="postgresql"
export Buildout__Auth__ConnectionString="Host=localhost;Database=buildout;Username=user;Password=pass"
buildout-mcp
```

Create Buildin keys and tokens:

```bash
# Store Buildin Bot API keys
buildout-cli auth key create --name "workspace-a" --key "ntn_xxx_workspace_a_key"
buildout-cli auth key create --name "workspace-b" --key "ntn_xxx_workspace_b_key"

# Create MCP tokens
buildout-cli auth token create --name "alice"
buildout-cli auth token create --name "bob"

# Map tokens to Buildin keys
buildout-cli auth token map --token-id <alice-token-id> --key-id <workspace-a-key-id>
buildout-cli auth token map --token-id <bob-token-id> --key-id <workspace-b-key-id>
```

Alice's requests use workspace A's key; Bob's use workspace B's key.

## JSON Configuration

```json
{
  "BotToken": "ntn_xxx_default_key",
  "Transport": { "Type": "http" },
  "Auth": {
    "Mode": "proxy",
    "Provider": "sqlite",
    "SqlitePath": "/var/lib/buildout/auth.db"
  }
}
```
