# Data Model: Skills & Prompts

**Feature**: 011-skills-prompts
**Date**: 2026-05-23

## Entities

### Agent Target

Determines where skill files are installed on disk.

| Field | Type | Description |
|-------|------|-------------|
| Name | `string` | Agent identifier: `claude` or `opencode` |
| IsLocal | `bool` | Whether to target project-local (true) or global (false) directory |
| TargetDirectory | `string` | Resolved absolute path to the `buildout/` subdirectory |

**Mapping rules**:

| Name | Global Path | Local Path |
|------|-------------|------------|
| `claude` | `~/.claude/skills/buildout-cli/` | `{cwd}/.claude/skills/buildout-cli/` |
| `opencode` | `~/.config/opencode/skills/buildout-cli/` | `{cwd}/.opencode/skills/buildout-cli/` |

**Validation rules**:
- `Name` must be `claude` or `opencode` — any other value is a validation error
- Global path: parent directory must exist (e.g., `~/.claude/` must exist)
- Local path: entire path is created if missing

### Embedded Skill

A directory conforming to the [Agent Skills specification](https://agentskills.io/specification), bundled in the CLI assembly as embedded resources.

| Field | Type | Description |
|-------|------|-------------|
| ResourceName | `string` | Manifest resource name (e.g., `Buildout.Cli.Skills.SKILL.md`, `Buildout.Cli.Skills.update.md`) |
| FileName | `string` | Output filename (e.g., `SKILL.md`, `update.md`) |
| Content | `string` | Markdown content loaded from embedded resource |

**SKILL.md structure** (per Agent Skills spec):
- YAML frontmatter with `name: buildout-cli` and `description` (required)
- Markdown body with command overview, workflow, and relative file references
- Must match installed directory name (`buildout-cli`)
- Name: lowercase + hyphens only, 1-64 chars

**Progressive disclosure model** (per Agent Skills spec):
1. **Discovery**: Agent loads `name` + `description` from frontmatter (~30 tokens)
2. **Activation**: Agent reads full `SKILL.md` body when task matches (~200 tokens)
3. **Execution**: Agent loads topic reference files on demand (e.g., `update.md` when editing)

**Lifecycle**: Read-only at runtime. Written to disk by `skills install`.

**Files**: `SKILL.md` (entrypoint), `create.md`, `read.md`, `update.md`, `delete.md`, `restore.md`, `search.md`, `database-views.md`

### Embedded Prompt

A markdown document bundled in the MCP assembly.

| Field | Type | Description |
|-------|------|-------------|
| ResourceName | `string` | Manifest resource name (e.g., `Buildout.Mcp.Prompts.server-instructions.md`) |
| Name | `string` | Logical name (e.g., `server-instructions`, `update`) |
| Content | `string` | Markdown content loaded from embedded resource |

**Lifecycle**: Read-only at runtime. Served via MCP protocol (server instructions or named prompts).

**Files**: `server-instructions.md`, `update.md`

## State Transitions

### Skill Installation

```
[No buildout/ dir] → install → [buildout/ dir with skill files]
[buildout/ dir exists] → install (no --overwrite) → ERROR
[buildout/ dir exists] → install (--overwrite) → [buildout/ dir with updated files]
```

### Skill Removal

```
[buildout/ dir exists] → remove → [buildout/ dir deleted]
[No buildout/ dir] → remove → graceful "not found" message
```

### Prompt Loading (MCP)

```
[Server startup] → load server-instructions.md → set as ServerInstructions
[Client requests prompts/list] → enumerate named prompts from BuildoutPrompts
[Client requests prompts/get "update"] → load update.md → return as ChatMessage
```

## Relationships

- `Agent Target` has many `Embedded Skill` files (1:N — all skills installed together)
- `Embedded Prompt` (`server-instructions`) → used as MCP `ServerInstructions`
- `Embedded Prompt` (`update`) → registered as MCP named prompt via `[McpServerPrompt]`
