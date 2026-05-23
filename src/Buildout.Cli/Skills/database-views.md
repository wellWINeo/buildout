# database-views

## Overview

Renders a Notion database in a chosen view style (table, board, gallery, list, calendar, or timeline) directly in the terminal.

## Syntax

```
buildout-cli db view <DATABASE_ID> [options]
```

## Arguments

| Name | Required | Description |
|---|---|---|
| `<DATABASE_ID>` | Yes | ID of the Notion database. |

## Options

| Flag | Type | Default | Description |
|---|---|---|---|
| `-s`, `--style` | enum | `table` | View style: `table`, `board`, `gallery`, `list`, `calendar`, `timeline`. |
| `-g`, `--group-by` | string | _(none)_ | Property name to group by. Required for `board` view. |
| `-d`, `--date-property` | string | _(none)_ | Property carrying a date. Required for `calendar` and `timeline` views. |

## Examples

Render a database as a styled table:

```
buildout-cli db view abc123 --style table
```

Render a board grouped by status:

```
buildout-cli db view abc123 --style board --group-by Status
```

Render a calendar using the due-date property:

```
buildout-cli db view abc123 --style calendar --date-property "Due Date"
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 2 | Validation error (missing required options, invalid arguments) |
| 3 | Database not found (404) |
| 4 | Authentication failure (401/403) |
| 5 | Transport failure (network error) |
| 6 | Unexpected API error |

## Notes

- The `table` style uses a Markdown terminal renderer when the terminal supports styled output; all other styles emit plain text.
- `--group-by` is effectively required when using `--style board`. Omitting it triggers a validation error (exit code 2).
- `--date-property` is effectively required for `--style calendar` and `--style timeline`.