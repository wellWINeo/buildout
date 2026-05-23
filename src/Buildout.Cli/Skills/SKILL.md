---
name: buildout-cli
description: CLI for buildin.ai workspace. Use when creating, reading, updating, deleting, restoring, or searching pages, or rendering database views. Covers buildout-cli commands, flags, exit codes, and workflows.
---

# buildout-cli

CLI for buildin.ai — a Notion-like workspace. Manages pages (read, create, update, delete, restore), searches content, and renders database views.

## Quick Reference

| Command | Description |
|---------|-------------|
| `get <page_id>` | Read a page as Markdown |
| `create <markdown_source>` | Create a page from Markdown |
| `update` | Patch-edit page blocks |
| `delete <page_id>` | Archive a page (soft delete) |
| `restore <page_id>` | Un-archive a page |
| `search <query>` | Search pages by keyword |
| `db view <database_id>` | Render a database view |

## Global Option

`--config` / `-c` — path to JSON config file (buildin token, options).

## Typical Workflow

1. **Find** a page: `buildout-cli search "keyword"`
2. **Read** it: `buildout-cli get <page_id>`
3. **Edit** it: `buildout-cli get <page_id> --editing` → `buildout-cli update --page <id> --revision <token> --ops ops.json`
4. **Delete/Restore**: `buildout-cli delete <page_id>` / `buildout-cli restore <page_id>`

## Reference Files

| File | Description |
|------|-------------|
| [create.md](create.md) | Create new pages from Markdown |
| [read.md](read.md) | Read pages, get editing snapshots |
| [update.md](update.md) | Non-destructive patch editing (most complex) |
| [delete.md](delete.md) | Archive pages |
| [restore.md](restore.md) | Un-archive pages |
| [search.md](search.md) | Search pages by keyword |
| [database-views.md](database-views.md) | Render database views in terminal |