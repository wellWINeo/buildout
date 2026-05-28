# auth token map

## Overview

Maps an MCP token to a specific Buildin API key. Used in `mapped` auth mode so each client token resolves to a different Buildin workspace key.

## Syntax

```
buildout-cli auth token map <token-id> <key-id>
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `<token-id>` | Yes | UUID of the MCP token (from `auth token list`). |
| `<key-id>` | Yes | UUID of the Buildin key (from `auth key list`). |

## Examples

```
buildout-cli auth token map 550e8400-... 661e9511-...
```

Output:
```
Token mapped to key.
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Token mapped to key. |
| 2 | Invalid UUID format for token-id or key-id. |

## Notes

- Only relevant when `Auth:Mode` is `mapped`.
- A token can be re-mapped by running this command again with a different key-id.
