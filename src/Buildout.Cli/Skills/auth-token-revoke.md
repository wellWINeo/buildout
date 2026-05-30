# auth token revoke

## Overview

Revokes an existing MCP token. Revoked tokens are rejected immediately on the next request.

## Syntax

```
buildout-cli auth token revoke <id>
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `<id>` | Yes | UUID of the token to revoke (from `auth token list`). |

## Examples

```
buildout-cli auth token revoke 550e8400-e29b-41d4-a716-446655440000
```

Output:
```
Token revoked.
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Token revoked. |
| 2 | Invalid UUID format. |
| 3 | Token not found or already revoked. |

## Notes

- Requires `Auth:Provider` to be configured.
- Revocation is permanent — there is no un-revoke command.
