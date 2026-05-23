# read

## Overview
Retrieves a page by ID and outputs its content as Markdown (styled in a capable terminal, plain otherwise). With `--editing`, returns a snapshot that includes a revision token and unknown block IDs needed for subsequent updates.

## Syntax
```
buildout-cli get <page_id> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `<page_id>` | Yes | The ID of the page to retrieve. |

## Options

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--editing` | bool | `false` | Fetch an editing snapshot instead of rendered Markdown. Emits revision metadata on stderr (or as JSON fields on stdout with `--print json`). |
| `--print` | string | `markdown` | Output format. `markdown` prints the page content. `json` prints a JSON object with `markdown`, `revision`, and `unknown_block_ids` fields. Requires `--editing`. |

## Examples

**Read a page with terminal styling:**
```
buildout-cli get abc123
```

**Read a page as plain text (piped/redirected):**
```
buildout-cli get abc123 > page.md
```

**Fetch an editing snapshot for later update:**
```
buildout-cli get abc123 --editing
# revision token is printed on stderr; capture it for `buildout-cli restore`
```

**Fetch an editing snapshot as JSON:**
```
buildout-cli get abc123 --editing --print json
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success. |
| 2 | Invalid usage (e.g. `--print json` without `--editing`). |
| 3 | Page not found (404). |
| 4 | Authentication failure (401/403). |
| 5 | Transport failure (network/API connectivity). |
| 6 | Unexpected API error. |

## Notes
- In default mode (no `--editing`), the revision token is **not** returned. You must use `--editing` if you plan to modify and restore the page.
- When `--editing` is used with `--print markdown` (the default), the page Markdown goes to **stdout** while the revision token and unknown block IDs go to **stderr**. Use `2>` to capture the revision separately.
- `--print json` writes a single JSON object to stdout with keys `markdown`, `revision`, and `unknown_block_ids`.
- Styled output is automatic when stdout is a terminal that supports ANSI sequences; otherwise plain text is emitted.
