# delete

## Overview

Archive (soft-delete) a Notion page by ID. Pages are **not permanently removed** — they can be restored with the `restore` command.

## Syntax

```
buildout-cli delete <page_id> [options]
```

## Arguments

| Name | Required | Description |
|---|---|---|
| `<page_id>` | Yes | The buildin page ID to archive. |

## Options

| Flag | Type | Default | Description |
|---|---|---|---|
| `--print` | string | `"summary"` | Output format: `summary` (human-readable) or `json` (machine-readable `PageLifecycleOutcome`). |

## Examples

Delete a page with human-readable output:

```
buildout-cli delete abc123def456
```

Delete a page and get machine-readable JSON output:

```
buildout-cli delete abc123def456 --print json
```

JSON output serializes the full `PageLifecycleOutcome` object (camelCase), including `pageId`, `archived`, `changed`, and `failureClass` fields.

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success — page archived |
| 2 | Missing or empty page ID |
| 3 | Page not found |
| 4 | Authentication failure |
| 5 | Transport / network error |
| 6 | Unexpected error |

## Notes

- This is a soft delete — pages are archived, not permanently removed
- When a page is already archived, the command succeeds with `changed=false` (no-op)
- Use the `restore` command to unarchive a deleted page