# Contract: `buildout create` (CLI)

**Feature**: [spec.md](../spec.md) · [plan.md](../plan.md)

The CLI surface for page creation. Thin adapter over `IPageCreator`
(see `core-creator.md`).

---

## Synopsis

```text
buildout create <markdown_source>
                --parent <id>
                [--title <text>]
                [--icon <emoji_or_url>]
                [--cover <url>]
                [--property <name>=<value>] ...
                [--print id|json|none]
```

| Argument / option | Type | Required | Notes |
|---|---|---|---|
| `<markdown_source>` | positional, string | yes | Filesystem path to a Markdown file, or `-` to read from stdin (capped at 16 MiB per R9). |
| `--parent` | string | yes | Buildin page id or database id. |
| `--title` | string | no | Overrides leading-H1 title (spec FR-005). |
| `--icon` | string | no | Single emoji grapheme cluster, or `http(s)://` URL. |
| `--cover` | string | no | `http(s)://` URL. |
| `--property` | repeatable, `name=value` | no | Only meaningful when parent is a database. Property kinds supported in v1: title, rich_text, number, select, multi_select (comma-separated), checkbox (`true`/`false`/`yes`/`no`), date (ISO 8601), url, email, phone_number. |
| `--print` | `id` \| `json` \| `none` | no, default `id` | `id` writes `<new_page_id>\n`. `json` writes `{"id":"<new_page_id>","uri":"buildin://<new_page_id>"}\n`. `none` writes nothing. |

---

## Exit codes

Reused from features 002/003 (feature 002 FR-009). No new codes.

| Exit | Class | When |
|---|---|---|
| 0 | success | Page created; all body batches appended. `--print id`/`json` content on stdout. |
| 2 | validation | Bad input (empty `--parent`, malformed `--property`, no title resolvable, etc.). No buildin write call was issued. |
| 3 | not-found | Probe returned 404 for both `GET page` and `GET database`. |
| 4 | auth | Probe or write returned 401/403. |
| 5 | transport | Transport-level error contacting buildin. |
| 6 | unexpected | Any other buildin or local error, *including partial creation* (see R8). |

For the partial-creation case, stderr carries the message defined in
R8:

```text
Partial creation: page <new_page_id> exists but appendBlockChildren failed after <K> of <N> top-level batches: <underlying message>
```

The partial page id is the first whitespace-separated token after
`page` on that line — shell-extractable with `awk` (R8).

---

## TTY behaviour

Inherited from features 002/003 unchanged. `Spectre.Console`'s
`AnsiConsole` detects TTY; when stdout is a pipe / redirect, no ANSI
escape codes are emitted. Under `--print id`/`json`, the output is
plain regardless of TTY — only an optional human-readable suffix
("Created page 'My Page' at buildin://...") in TTY mode is allowed,
and only when `--print id` is the default (not when explicitly set).
Plain-mode output remains a single line.

---

## Validation-error messages

Each pre-write validation produces a specific stderr line and exits 2:

| Violation | Message |
|---|---|
| `--parent` missing | `--parent is required.` |
| `<markdown_source>` missing | `A markdown source (file path or '-') is required.` |
| Stdin > 16 MiB | `Standard input exceeded 16 MiB; refusing to read further.` |
| Markdown body empty *and* no `--title` and no leading H1 | `Cannot determine the new page's title: no leading '# Title' heading found and --title was not provided.` |
| `--property` against a page parent | `--property is only valid when the parent is a database; '<parent_id>' resolved to a page.` |
| Unknown property name | `Unknown property '<name>'. Valid properties: <list>.` |
| Unsupported property kind | `Property '<name>' is of kind '<kind>', which is not supported in v1.` |
| Property value parse failure | `Cannot parse value for property '<name>' (<kind>): <detail>.` |
| Malformed `--icon` | `--icon must be a single emoji or an absolute URL.` |
| Malformed `--cover` | `--cover must be an absolute URL.` |
| Probe 404 / both | `Parent '<parent_id>' was not found as a page or a database.` |

All messages are written to **stderr**. Stdout in the validation case
is empty.

---

## Examples

```sh
# Read a file, create under a page parent
buildout create --parent <page_id> notes.md

# Pipe markdown from another tool
echo "# Hello\n\nWorld" | buildout create --parent <page_id> -

# Create under a database parent with property values
buildout create --parent <db_id> --property Status=Done --property Tags=red,green notes.md

# Get the JSON shape (for tooling)
buildout create --parent <page_id> --print json notes.md
# → {"id":"<new_id>","uri":"buildin://<new_id>"}

# Capture the new id for piping
new_id=$(buildout create --parent <page_id> --print id notes.md)
buildout get "$new_id" > written-back.md
```
