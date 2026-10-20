---
name: buildout-cli
description: CLI for buildin.ai workspace. Use when creating, reading, updating, deleting, restoring, or searching pages, or rendering database views. Covers buildout-cli commands, flags, exit codes, and workflows.
---

# buildout-cli

CLI for buildin.ai — a Notion-like workspace. Manages pages (read, create, update), searches content, renders trees, and renders database views through Buildin API V2.

## Quick Reference

| Command | Description |
|---------|-------------|
| `get <page_id>` | Read a page as Markdown |
| `create <markdown_source>` | Create a page from Markdown |
| `update` | Patch-edit page blocks |
| `search <query>` | Search pages by keyword |
| `db view <database_id>` | Render a database view |
| `tree <page_id>` | Map a page or database hierarchy as ASCII tree or JSON |

## Global Option

`--config` / `-c` — path to JSON config file (buildin token, options).

## Typical Workflow

1. **Find** a page: `buildout-cli search "keyword"`
2. **Read** it: `buildout-cli get <page_id>`
3. **Edit** it: `buildout-cli get <page_id> --editing` → `buildout-cli update --page <id> --revision <token> --ops ops.json`

## Reference Files

| File | Description |
|------|-------------|
| [create.md](create.md) | Create new pages from Markdown |
| [read.md](read.md) | Read pages, get editing snapshots |
| [update.md](update.md) | Non-destructive patch editing (most complex) |
| [search.md](search.md) | Search pages by keyword |
| [database-views.md](database-views.md) | Render database views in terminal |
| [tree.md](tree.md) | Map a page or database hierarchy as ASCII tree or JSON |
