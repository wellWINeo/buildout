# Research: Skills & Prompts

**Feature**: 011-skills-prompts
**Date**: 2026-05-23

## Decision 1: Embedded Resources for Skill/Prompt Content

**Decision**: Use .NET embedded resources (`<EmbeddedResource>` in `.csproj`) to bundle markdown content into the assembly. Installed skill directories conform to the [Agent Skills specification](https://agentskills.io/specification).

**Rationale**: Embedded resources are the standard .NET mechanism for shipping static content in a single binary. The Agent Skills specification defines a standard format (`SKILL.md` with YAML frontmatter + reference files) that is supported by Claude Code, OpenCode, and 30+ other agent clients. Following this spec ensures skills work across all supported agents without per-agent customization.

**Alternatives considered**:
- **Source generators / compile-time constants**: Would require recompiling for content changes and produces large string literals. Embedded resources keep markdown files human-readable and editable in the repo.
- **Embedded file provider**: Over-engineered for static read-only content.

## Decision 2: Spectre.Console.Cli Branch Command Pattern

**Decision**: Use the same branch command pattern as the existing `db` command — `SkillsSettings` as the branch root, with `SkillsInstallCommand` and `SkillsRemoveCommand` as leaf commands.

**Rationale**: The `db` branch already establishes this pattern in the codebase. Using `AddBranch<SkillsSettings>("skills", ...)` with sub-commands follows existing conventions and provides a natural `buildout-cli skills install` / `buildout-cli skills remove` syntax.

**Alternatives considered**:
- **Single command with `--action` flag**: Less idiomatic for Spectre and doesn't match the existing `db` pattern.
- **Top-level `install-skills` / `remove-skills` commands**: Clutters the command namespace.

## Decision 3: Agent Target Directory Mapping

**Decision**: Use a simple mapping from agent name to directory paths, with `--local` flag toggling between global (home) and local (cwd-relative) paths.

**Agent paths**:
| Agent | Global | Local |
|-------|--------|-------|
| `claude` | `~/.claude/skills/buildout-cli/` | `.claude/skills/buildout-cli/` |
| `opencode` | `~/.config/opencode/skills/buildout-cli/` | `.opencode/skills/buildout-cli/` |

**Rationale**: These follow each agent's documented configuration conventions. Claude Code discovers skills under `.claude/skills/` at project root or `~/.claude/skills/` globally. OpenCode uses `.opencode/skills/` locally and `~/.config/opencode/skills/` globally. The `buildout-cli` subdirectory namespaces skills to avoid collisions with other tools.

**Alternatives considered**:
- **Agent-specific subdirectory names**: Adds complexity for no benefit — both agents accept skill files under their `skills/` directory.
- **Single directory for all agents**: Would conflict with agent-specific config file conventions.

## Decision 4: MCP Named Prompts via McpServerPromptType

**Decision**: Register update-specific prompts using `[McpServerPromptType]` and `[McpServerPrompt]` attributes with `WithPrompts<BuildoutPrompts>()` registration.

**Rationale**: The ModelContextProtocol C# SDK natively supports named prompts via these attributes. Clients discover and request prompts by name using `GetPromptAsync`. This separates the detailed update instructions from the compact base server instructions, reducing context token usage for clients that don't use the update tool.

**SDK pattern**:
```csharp
[McpServerPromptType]
public class BuildoutPrompts
{
    [McpServerPrompt, Description("Detailed instructions for the page update workflow")]
    public static ChatMessage UpdateWorkflow() => new(ChatRole.User, Prompts.Update);
}
```

**Alternatives considered**:
- **Tool description enrichment**: Would always include update instructions in tool metadata, wasting context tokens.
- **MCP resources**: Resources are data-oriented (URIs, MIME types); prompts are the correct primitive for instructional text.

## Decision 5: Overwrite Protection via File Existence Check

**Decision**: If any file exists in the target `buildout/` directory, refuse to install unless `--overwrite` is passed.

**Rationale**: Simple, predictable behavior. The user explicitly confirmed this approach — no content hashing or manifest files needed.

**Alternatives considered**:
- **Content comparison**: More precise but unnecessarily complex for a developer tool.
- **Manifest with hashes**: Over-engineered; would require maintaining state across runs.

## Decision 6: Skill Content Scope

**Decision**: An entrypoint `SKILL.md` (with YAML frontmatter per the Agent Skills specification) plus one skill markdown file per CLI command/topic: `SKILL.md`, `create.md`, `read.md`, `update.md`, `delete.md`, `restore.md`, `search.md`, `database-views.md`.

**Rationale**: The Agent Skills specification defines progressive disclosure — agents load the `SKILL.md` name/description at startup (~30 tokens), the full body when activated (~200 tokens), and reference files on demand. Granular topic files allow agents to load only relevant context. `SKILL.md` serves as the entrypoint providing common buildout-cli information and referencing topic-specific files.

**Alternatives considered**:
- **Single monolithic skill file**: Wastes agent context tokens by loading all instructions even for simple operations.
