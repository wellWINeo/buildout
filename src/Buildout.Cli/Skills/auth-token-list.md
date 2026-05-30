# auth token list

## Overview

Lists all MCP tokens in the token registry, showing their ID, name, status (active/revoked), and creation date.

## Syntax

```
buildout-cli auth token list
```

## Examples

```
buildout-cli auth token list
```

Output:
```
 ID                                   Name       Status  Created
 ───────────────────────────────────────────────────────────────────────────
 550e8400-e29b-41d4-a716-446655440000  ci-agent   active  2025-05-28 12:00:00
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success. Table printed. |

## Notes

- Requires `Auth:Provider` to be configured.
- Revoked tokens remain visible in the list with status `revoked`.
