# CLI Skills Command Contract

**Feature**: 011-skills-prompts

## Command: `skills install`

Install buildout-cli skill files to an agent's configuration directory.

```
buildout-cli skills install --agent <claude|opencode> [--local] [--overwrite]
```

### Arguments

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--agent` | Yes | — | Target agent: `claude` or `opencode` |
| `--local` | No | `false` | Install to project-local directory instead of global |
| `--overwrite` | No | `false` | Overwrite existing skill files |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success — skill files installed |
| 2 | Validation error — unsupported agent name |
| 2 | Precondition error — files exist without `--overwrite` |
| 2 | Precondition error — global target directory does not exist |
| 2 | I/O error — permission denied |

### Output (success)

```
Installed 8 skill files to ~/.claude/skills/buildout-cli/
```

### Output (files exist, no --overwrite)

```
Error: Skill files already exist at ~/.claude/skills/buildout-cli/. Use --overwrite to replace them.
```

### Output (unsupported agent)

```
Error: Unsupported agent 'cursor'. Supported agents: claude, opencode.
```

### Output (global dir missing)

```
Error: Global config directory ~/.claude/ does not exist. Create it first or use --local.
```

---

## Command: `skills remove`

Remove buildout-cli skill files from an agent's configuration directory.

```
buildout-cli skills remove --agent <claude|opencode> [--local]
```

### Arguments

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--agent` | Yes | — | Target agent: `claude` or `opencode` |
| `--local` | No | `false` | Remove from project-local directory instead of global |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success — skill files removed |
| 2 | Validation error — unsupported agent name |
| 0 | No files found — graceful exit with message |

### Output (success)

```
Removed ~/.claude/skills/buildout-cli/ (8 files)
```

### Output (not found)

```
No buildout skill files found at ~/.claude/skills/buildout-cli/.
```

---

## MCP Prompt Contract

### Server Instructions (base)

Served as `ServerInstructions` in MCP server capabilities. Loaded from embedded `server-instructions.md`. Must be compact (< 500 tokens). Contains general tool usage guidance only.

### Named Prompt: `update`

Registered as an MCP prompt via `[McpServerPrompt]`. Returns detailed instructions for the page update workflow. Clients request this on demand via `prompts/get`.

**Name**: `update`
**Description**: "Detailed instructions for the page update workflow"
**Arguments**: None
**Returns**: `ChatMessage` with `ChatRole.User` containing the update prompt content
