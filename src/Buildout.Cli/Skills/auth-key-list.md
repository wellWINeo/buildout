# auth key list

## Overview

Lists all Buildin API keys registered in the token registry.

## Syntax

```
buildout-cli auth key list
```

## Examples

```
buildout-cli auth key list
```

Output:
```
 ID                                   Name            Created
 ────────────────────────────────────────────────────────────────────────
 550e8400-e29b-41d4-a716-446655440000  workspace-prod  2025-05-28 12:00:00
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success. Table printed. |

## Notes

- Requires `Auth:Provider` to be configured.
- Key values are not shown in the list output.
