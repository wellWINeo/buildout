# Contract: `buildout-cli tree`

## Synopsis

```
buildout-cli tree <page_id> [--format ascii|json] [--depth N] [--config|-c PATH]
```

## Arguments

| Argument | Required | Type | Description |
|---|---|---|---|
| `<page_id>` | yes | string (UUID) | UUID of the root page or database. Same identifier shape used by `get`, `update`, `delete`, etc. |

## Options

| Flag | Type | Default | Description |
|---|---|---|---|
| `--format` | enum (`ascii` \| `json`) | `ascii` | Output format. ASCII is a Unix-tree-style rendering of markdown links; JSON is a recursive `{name, uri, children}` object. |
| `--depth` | integer | `3` | Inclusive number of descendant levels to traverse. Must be in `[1, 7]`. |
| `--config` / `-c` | path | `~/.config/buildout/config.json` | Path to the JSON configuration file (inherited from `BuildoutCommandSettings`). |

## Output

- **`--format ascii`** (default): plain-text Unix-tree-style hierarchy on
  stdout. Each line is a markdown link of the form `[Name](<URL>)`. The root
  line has no connector prefix; children use `├── `, `└── `, `│   `, and four
  spaces per the standard `tree(1)` convention.
- **`--format json`**: a pretty-printed JSON document on stdout in the shape
  defined by [`service.md`](./service.md) — a single root object containing
  `name`, `uri`, and `children`, recursive.

In both formats, descendant failures are surfaced inline as a node with name
`(unavailable)` and `children: []`; empty-or-whitespace titles appear as
`(untitled)`. The exit code remains `0` when only descendant failures occurred.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. |
| `2` | Invalid usage: `--depth` out of `[1, 7]`, unknown `--format` value, missing `<page_id>`. |
| `3` | Root page or database not found (404). |
| `4` | Authentication failure (401/403). |
| `5` | Transport failure (network / API connectivity). |
| `6` | Unexpected buildin error. |
| `7` | Cycle detected in the page hierarchy. |

## Examples

Default ASCII tree, depth 3:

```
$ buildout-cli tree 11111111-2222-3333-4444-555555555555
[Engineering](<https://buildin.ai/...>)
├── [Onboarding](<https://buildin.ai/...>)
│   ├── [Day 1](<https://buildin.ai/...>)
│   └── [Day 2](<https://buildin.ai/...>)
└── [Runbooks](<https://buildin.ai/...>)
    └── [Incidents DB](<https://buildin.ai/...>)
```

JSON, depth 1:

```
$ buildout-cli tree 11111111-2222-3333-4444-555555555555 --format json --depth 1
{
  "name": "Engineering",
  "uri": "https://buildin.ai/...",
  "children": [
    { "name": "Onboarding", "uri": "https://buildin.ai/...", "children": [] },
    { "name": "Runbooks",   "uri": "https://buildin.ai/...", "children": [] }
  ]
}
```

Invalid depth:

```
$ buildout-cli tree 1111... --depth 8
depth must be between 1 and 7 (inclusive); got 8
$ echo $?
2
```

## Stdout / stderr split

- All rendered output goes to **stdout** so it is pipe-friendly.
- Error messages go to **stderr**.
- Descendant failures are logged via the standard logger (configured by the
  shared host) at `Warning` level; they do not appear on stderr unless the
  logger is configured to emit there.
