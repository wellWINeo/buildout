# Contract: CLI Surface — get --editing + update

**Feature**: 008-markdown-page-update
**Date**: 2026-05-16

---

## `buildout get <page_id>` — extended flags

Changes to the existing `GetCommand`. The command's base behaviour (without new flags) is
byte-for-byte identical to spec 002 (SC-007).

### New flags

| Flag | Type | Default | Notes |
|------|------|---------|-------|
| `--editing` | bool | false | Switch to edit-mode read |
| `--print <markdown\|json>` | enum | `markdown` | Only valid with `--editing`; invalid without it |

### Behaviour matrix

| `--editing` | `--print` | stdout | stderr |
|-------------|-----------|--------|--------|
| false | (omitted) | Spec 002 unanchored Markdown (TTY-styled if terminal) | — |
| true | `markdown` | Anchored Markdown body | `revision: <hex>\nunknown_block_id: <id>\n...` |
| true | `json` | Single JSON object (see below) | — |
| false | `json` | **Error**: exit 2, message "—print json requires --editing" | — |

**JSON output** (`--editing --print json`):
```json
{
  "markdown": "<!-- buildin:root -->\n# Title\n...",
  "revision": "1a2b3c4d",
  "unknown_block_ids": ["uuid-of-unsupported-block"]
}
```

### Exit codes

Inherits from spec 002 / spec 006 taxonomy: 0 (success), 2 (validation), 3 (not found),
4 (auth), 5 (transport), 6 (unexpected).

---

## `buildout update`

New top-level command registered as `config.AddCommand<UpdateCommand>("update")`.

### Arguments and flags

| Flag | Type | Required | Default | Notes |
|------|------|----------|---------|-------|
| `--page <id>` | string | Yes | — | Target page ID |
| `--revision <token>` | string | Yes | — | Revision token from `get --editing` |
| `--ops <path\|->` | string | Yes | — | Path to JSON operations file; `-` reads from stdin |
| `--dry-run` | bool | No | false | Preview without committing |
| `--allow-large-delete` | bool | No | false | Bypass the large-delete guard |
| `--print <summary\|json>` | enum | `summary` | — | Output format |

### Output

**`--print summary`** (default, stdout):
```
Reconciled page <page_id>: <preserved> preserved, <updated> updated, <new> new, <deleted> deleted
Revision: <new_revision>
```

Dry-run prefix:
```
[dry-run] Reconciled page <page_id>: ...
Revision: <new_revision> (not committed)
```

**`--print json`** (stdout):
```json
{
  "preserved_blocks": 5,
  "updated_blocks": 1,
  "new_blocks": 2,
  "deleted_blocks": 0,
  "ambiguous_matches": 0,
  "new_revision": "2b3c4d5e",
  "post_edit_markdown": null
}
```

(`post_edit_markdown` is non-null only when `--dry-run`.)

### Exit codes

| Condition | Code |
|-----------|------|
| Success | 0 |
| Validation error | 2 |
| Page not found | 3 |
| Auth / authorisation | 4 |
| Transport | 5 |
| Unexpected / `patch.partial` | 6 |
| Any `patch.*` rejection (`patch.stale_revision`, `patch.ambiguous_match`, etc.) | 7 |

On exit 7, stderr contains:
```
Patch rejected [<patch_error_class>]: <human-readable message>
```

For `patch.stale_revision`, stderr additionally contains:
```
Current revision: <current_revision>
```

### Validation rules

- `--page` non-empty.
- `--revision` non-empty.
- `--ops` must be `-` or a readable file path.
- The JSON at `--ops` must deserialize as a non-empty `PatchOperation[]`.
- `--print json` without `--dry-run` omits `post_edit_markdown`.
- `--allow-large-delete` is accepted silently when no large delete would occur.

### Operations JSON format

Root must be an array:
```json
[
  { "op": "replace_block",    "anchor": "<id>",     "markdown": "..." },
  { "op": "replace_section",  "anchor": "<id>",     "markdown": "..." },
  { "op": "search_replace",   "old_str": "...",     "new_str": "..." },
  { "op": "append_section",   "anchor": "<id>",     "markdown": "..." },
  { "op": "insert_after_block","anchor": "<id>",    "markdown": "..." }
]
```
