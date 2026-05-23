# create

## Overview

Creates a new page from a Markdown source file or stdin. The page is created under an optional parent, with optional metadata (title, icon, cover, properties).

## Syntax

```
buildout-cli create <markdown_source> [options]
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `<markdown_source>` | Yes | Path to a Markdown file, or `-` to read from stdin (max 16 MB). |

## Options

| Flag | Type | Default | Description |
|---|---|---|---|
| `--parent` | `string` | `""` | ID of the parent page. Empty string creates at root level. |
| `--title` | `string` | `null` | Page title. When omitted, the title is derived from the Markdown content. |
| `--icon` | `string` | `null` | Page icon (emoji or identifier). |
| `--cover` | `string` | `null` | URL of a cover image. |
| `--property` | `string[]` | `[]` | Key-value pair in `key=value` format. Repeatable. Malformed entries (no `=`) are silently skipped. |
| `--print` | `string` | `"id"` | Output format on success: `id` (just the page ID), `json` (`{"id":"...","uri":"buildin://..."}`), or `none` (no output). Case-insensitive. |

## Examples

Create a page from a file under a specific parent:

```
buildout-cli create notes.md --parent abc123 --title "Meeting Notes"
```

Create a page from stdin with custom properties:

```
cat page.md | buildout-cli create - --parent abc123 --property status=draft --property priority=high
```

Create a page and capture structured output:

```
buildout-cli create page.md --print json
```

Create a root-level page with an icon and cover:

```
buildout-cli create index.md --icon "🏠" --cover https://example.com/cover.jpg
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success. Page created. |
| 2 | Validation error (e.g., malformed input). |
| 3 | Not found (e.g., parent ID does not exist). |
| 4 | Authentication failure. |
| 5 | Transport failure (network/API issue). |
| 6 | Partial or unexpected error. The page may exist but not be fully populated. |

## Notes

- Exit code 6 indicates a partial failure: the page was created but subsequent operations (e.g., appending child blocks) failed. The page ID is printed to stderr in the error message.
- `--property` entries without an `=` sign are silently ignored rather than causing an error.
- When `--title` is omitted, the backend derives the title from the Markdown content.
- Stdin mode (`-`) buffers up to 16 MB of input.