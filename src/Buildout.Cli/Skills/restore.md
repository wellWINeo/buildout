# restore

## Overview

Restore an archived (deleted) page. Sets `archived` to `false` on the target page.

## Syntax

```
buildout-cli restore <page_id> [options]
```

## Arguments

| Name | Required | Description |
|---|---|---|
| `<page_id>` | Yes | ID of the page to restore. |

## Options

| Flag | Type | Default | Description |
|---|---|---|---|
| `--print` | string | `"summary"` | Output format: `summary` (human-readable) or `json` (machine-readable `PageLifecycleOutcome`). |

## Examples

Restore a page with human-readable output:
```
buildout-cli restore abc123
```

Restore a page with JSON output:
```
buildout-cli restore abc123 --print json
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 2 | Page ID missing/empty |
| 3 | Page not found |
| 4 | Authentication failure |
| 5 | Transport error |
| 6 | Unexpected error |

## Notes

- Only works on archived pages; restoring a non-archived page is a no-op (`changed=false`)
- Pair with the `delete` command, which archives a page
- JSON output serializes the full `PageLifecycleOutcome` object (camelCase)