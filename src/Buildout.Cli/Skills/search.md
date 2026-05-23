# search

## Overview

Search for content across all pages, or within a specific page.

## Syntax

```
buildout-cli search <query> [options]
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `<query>` | Yes | The search text. |

## Options

| Flag | Type | Default | Description |
|---|---|---|---|
| `--page` | `string?` | `null` | Restrict results to a single page by its ID. |

## Examples

Search all pages:

```
buildout-cli search "project timeline"
```

Search within a specific page:

```
buildout-cli search "meeting notes" --page abc123
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success — results returned (may be empty list). |
| 2 | Invalid input — query is empty or whitespace. |
| 3 | Page not found — the `--page` ID does not exist. |
| 4 | Authentication failure — 401 or 403 from the API. |
| 5 | Transport failure — network or connection error. |
| 6 | Unexpected API error. |

## Notes

- Output is styled (Spectre.Console rich format) when stdout is a capable terminal; plain text otherwise.
- An empty result set is not an error — the command exits 0 and prints nothing.
- The `<query>` argument is passed directly to the search service; no local validation beyond non-empty/whitespace.