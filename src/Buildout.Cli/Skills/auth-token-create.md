# auth token create

## Overview

Creates a new MCP authorization token. The raw token value is printed once to stdout and never shown again. Store it securely immediately.

## Syntax

```
buildout-cli auth token create <name>
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `<name>` | Yes | Human-readable label for this token (e.g. "ci-agent", "dev-laptop"). |

## Examples

```
buildout-cli auth token create ci-agent
```

Output:
```
mcp_a1b2c3d4e5f6...
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Token created. Raw value printed to stdout. |
| 2 | Validation error (e.g. missing name). |

## Notes

- Requires `Auth:Provider` to be configured (sqlite or postgresql).
- The raw token is shown only once. If lost, create a new token and revoke the old one.
- Tokens are hashed before storage — only the hash is persisted.
