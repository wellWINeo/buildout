# Contract: Bearer Credential and Scope Matrix

## Credential contract

The primary configuration key is `AccessToken` (environment form
`Buildout__AccessToken`). Deprecated `BotToken` / `Buildout__BotToken` remains a
fallback with a once-per-process, value-free warning. `AccessToken` wins when both
resolve. Either value may be a Buildin integration token or OAuth access token;
Buildout treats both as opaque bearer credentials and does not implement OAuth
authorization, refresh, revocation, or storage.

## Minimum operation scopes

| Capability | Scope |
|------------|-------|
| Current-user identity | valid bearer token; no additional scope |
| Read page metadata/properties | `pages.read` |
| Create page | `pages.write` |
| Read block/block children | `blocks.read` |
| Update/trash/delete block or append block children | `blocks.write` |
| Read/query database | `databases.read` |
| Create/update database | `databases.write` |
| Keyword search | `search.read` |

`users.email.read` is not required by any existing Buildout workflow.

## CLI and MCP workflow matrix

Scopes are cumulative because one public workflow may invoke several domain
operations.

| Public workflow | Required scopes | Conditional/additional behavior |
|-----------------|-----------------|---------------------------------|
| CLI `get`; MCP page resource | `pages.read`, `blocks.read` | `databases.read` when an embedded child database is rendered. |
| CLI `get --editing`; MCP `get_page_markdown` | `pages.read`, `blocks.read` | `databases.read` when rendering an embedded child database. |
| CLI/MCP `search` | `search.read` | Buildin search itself needs only `search.read`; a Buildout follow-up page/ancestor read may also require `pages.read`. |
| CLI `create`; MCP `create_page` | `pages.read`, `pages.write`, `blocks.write` | `databases.read` when the parent is or must be probed as a database. |
| CLI `update`; MCP `update_page` dry run | `pages.read`, `blocks.read` | `databases.read` when an embedded database is rendered. |
| CLI `update`; MCP `update_page` commit | read scopes above plus `blocks.write` | Block removals use V2 block DELETE. |
| CLI `db view`; MCP `database_view` | `databases.read` | None. |
| CLI/MCP `tree` | `pages.read`, `blocks.read`, `databases.read` | Actual calls depend on root/descendant kinds, but users need the combined set for arbitrary trees. |

## Diagnostic rules

- 401 means the bearer credential is missing, malformed, expired, or invalid.
- 403 means authentication succeeded but the token may lack a scope, resource
  access, or workspace-plan capability.
- For 403, preserve the service message/request ID and show the operation's known
  scope requirements as guidance, not as a definitive cause.
- Never echo the credential when reporting either category.
