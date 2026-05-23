# update

## Overview

Applies patch operations to a page's blocks. This is a non-destructive, patch-based editing operation: each operation targets specific anchored blocks, leaving untargeted blocks untouched. The command uses optimistic concurrency via revision tokens to prevent silent overwrites.

## Syntax

```
buildout-cli update [options]
```

## Options

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--page` | `string` | (required) | Page ID to update |
| `--revision` | `string` | (required) | Revision token from `get --editing` |
| `--ops` | `string` | (required) | Path to a JSON ops file, or `-` to read from stdin |
| `--dry-run` | `bool` | `false` | Compute and display changes without applying them |
| `--allow-large-delete` | `bool` | `false` | Permit operations that delete many blocks at once |
| `--print` | `string` | `summary` | Output format: `summary` (human-readable) or `json` |

## Patch Operations

The `--ops` file must contain a JSON array of operation objects. Each object requires an `op` discriminator field. All property names use `snake_case`.

| `op` value | Fields | Description |
|-------------|--------|-------------|
| `replace_block` | `anchor`, `markdown` | Replace a single anchored block with new markdown |
| `replace_section` | `anchor`, `markdown` | Replace the section (heading + children) under the anchor |
| `search_replace` | `old_str`, `new_str` | Find-and-replace text across the page markdown |
| `append_section` | `anchor` (optional), `markdown` | Append new markdown blocks. If `anchor` is given, appends after that section; otherwise appends at end of page |
| `insert_after_block` | `anchor`, `markdown` | Insert new markdown blocks immediately after the anchored block |

## Examples

Basic update — replace a single block:

```sh
buildout-cli update \
  --page abc123 \
  --revision "r:sha256:..." \
  --ops ops.json
```

Dry-run to preview changes without applying:

```sh
buildout-cli update \
  --page abc123 \
  --revision "r:sha256:..." \
  --ops ops.json \
  --dry-run
```

Read ops from stdin (useful for piped/generated operations):

```sh
echo '[{"op":"search_replace","old_str":"foo","new_str":"bar"}]' | \
  buildout-cli update \
    --page abc123 \
    --revision "r:sha256:..." \
    --ops -
```

Allow a large delete (exceeds the configured safety threshold):

```sh
buildout-cli update \
  --page abc123 \
  --revision "r:sha256:..." \
  --ops ops.json \
  --allow-large-delete
```

JSON output mode:

```sh
buildout-cli update \
  --page abc123 \
  --revision "r:sha256:..." \
  --ops ops.json \
  --print json
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 2 | Missing or invalid argument (missing `--page`, `--revision`, `--ops`, file not found, malformed JSON) |
| 3 | Page not found (HTTP 404) |
| 4 | Authentication failure (HTTP 401/403) |
| 5 | Transport failure (network/API connectivity) |
| 6 | Patch error or unexpected API error (includes partial patch failure — some ops applied before error) |
| 7 | Revision conflict — the page was modified since the revision token was issued. Re-fetch with `get --editing` and retry |

## Important Notes

- Revision tokens are obtained from `buildout-cli get --editing`. A stale token triggers exit code 7.
- The update is non-destructive: operations target specific blocks by anchor. Untouched blocks are preserved as-is.
- `--dry-run` performs full reconciliation and reports block counts and a new revision, but does not call the API to write changes.
- When a patch deletes more blocks than the configured `LargeDeleteThreshold`, the command rejects the operation unless `--allow-large-delete` is passed.
- Ops JSON uses `snake_case` property names (e.g., `old_str`, `new_str`, not `oldStr`).
- Stdin mode (`--ops -`) reads up to 16 MB.
