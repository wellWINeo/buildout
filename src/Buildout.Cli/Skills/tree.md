# tree

## Overview

Traverses a page or database hierarchy and renders it as an ASCII tree (default) or JSON. Only sub-pages and embedded databases are included; content blocks (paragraphs, headings, etc.) are skipped. Inaccessible descendants appear as `(unavailable)`; empty titles appear as `(untitled)`.

## Syntax

```
buildout-cli tree <page_id> [--format ascii|json] [--depth N] [--config PATH]
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `<page_id>` | Yes | UUID of the root page or database to map. |

## Options

| Flag | Type | Default | Description |
|---|---|---|---|
| `--format` | `ascii` \| `json` | `ascii` | Output format. `ascii` produces a Unix-tree-style hierarchy of markdown links; `json` produces a recursive `{name, uri, children}` object. |
| `--depth` | integer | `3` | Number of descendant levels to traverse. Must be in `[1, 7]`. |
| `--config` / `-c` | path | `~/.config/buildout/config.json` | Path to a JSON configuration file. |

## Output Formats

### ASCII (default)

Each node is a CommonMark link `[Name](<URL>)`. Root has no prefix; children use `├──`, `└──`, `│   `, and four-space gutters.

```
[Engineering](<https://buildin.ai/...>)
├── [Onboarding](<https://buildin.ai/...>)
│   ├── [Day 1](<https://buildin.ai/...>)
│   └── [Day 2](<https://buildin.ai/...>)
└── [Runbooks](<https://buildin.ai/...>)
    └── [Incidents DB](<https://buildin.ai/...>)
```

Output is pipe-friendly (no trailing newline after last line).

### JSON

Pretty-printed recursive object. Property order: `name`, `uri`, `children`. Leaf nodes always have `children: []` (never absent).

```json
{
  "name": "Engineering",
  "uri": "https://buildin.ai/...",
  "children": [
    {
      "name": "Onboarding",
      "uri": "https://buildin.ai/...",
      "children": []
    }
  ]
}
```

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | Success. |
| `2` | Invalid usage: `--depth` out of `[1, 7]`, unknown `--format` value. |
| `3` | Root page or database not found (404). |
| `4` | Authentication failure (401/403). |
| `5` | Transport failure (network / API connectivity). |
| `6` | Unexpected buildin error. |
| `7` | Cycle detected in the page hierarchy. |

## Examples

```bash
# ASCII tree, depth 3 (default)
buildout-cli tree 11111111-2222-3333-4444-555555555555

# JSON output
buildout-cli tree <id> --format json

# Limit depth
buildout-cli tree <id> --depth 1   # root + immediate children only
buildout-cli tree <id> --depth 7   # deepest supported

# Pipe JSON to jq
buildout-cli tree <id> --format json | jq '.children[].name'
```
