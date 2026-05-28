# auth key create

## Overview

Registers a Buildin Bot API key in the token registry. Used in `mapped` auth mode to associate per-client MCP tokens with specific workspace keys.

## Syntax

```
buildout-cli auth key create <name> <key-value>
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `<name>` | Yes | Human-readable label for this key (e.g. "workspace-prod"). |
| `<key-value>` | Yes | The Buildin Bot API key value (stored in plaintext, used as Authorization bearer). |

## Examples

```
buildout-cli auth key create workspace-prod sk-buildin-abc123
```

Output:
```
550e8400-e29b-41d4-a716-446655440000
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Key created. ID printed to stdout. |
| 2 | Validation error. |

## Notes

- Requires `Auth:Provider` to be configured.
- The key value is stored in plaintext because it must be forwarded as a Bearer token.
- Use the printed ID with `auth token map` to associate MCP tokens with this key.
