# Feature Specification: Skills & Prompts

**Feature Branch**: `011-skills-prompts`
**Created**: 2026-05-23
**Status**: Draft
**Input**: User description: "Provide embedded skills for LLM agents to work with buildout-cli and embedded prompts in MCP to instruct models how to deal with tools. CLI command: buildout-cli skills [install|update|remove] --agent [claude|opencode] --global. Skills/prompts stored as markdown in repo. Use embedded resources for prompt content in executable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Install Agent Skills (Priority: P1)

A developer wants an LLM coding agent (Claude Code or OpenCode) to understand how to use buildout-cli commands effectively. They run `buildout-cli skills install --agent claude` and the appropriate skill files are copied to the project-local agent configuration directory, enabling the agent to work with buildout-cli autonomously.

**Why this priority**: Without install, none of the other subcommands are useful. This is the primary entry point and must work first.

**Independent Test**: Can be fully tested by running the install command and verifying skill files appear in the expected agent configuration directory with correct content.

**Acceptance Scenarios**:

1. **Given** a project directory with no agent skills, **When** the user runs `buildout-cli skills install --agent claude`, **Then** skill markdown files are written to the project's `.claude/` directory
2. **Given** a project directory with no agent skills, **When** the user runs `buildout-cli skills install --agent opencode`, **Then** skill markdown files are written to the project's `.opencode/` directory
3. **Given** an agent config directory that already contains skill files, **When** the user runs `buildout-cli skills install --agent claude`, **Then** existing skill files are overwritten with the latest embedded versions
4. **Given** the `--global` flag, **When** the user runs `buildout-cli skills install --agent claude --global`, **Then** skill files are written to the user's home directory agent config location (e.g., `~/.claude/`)

---

### User Story 2 - Update Agent Skills (Priority: P2)

A developer has previously installed buildout-cli skills and wants to refresh them to the latest version bundled with the current CLI buildout. They run `buildout-cli skills update --agent claude` and the installed skill files are replaced with the latest versions.

**Why this priority**: Update is the natural follow-up to install. It reuses the same content delivery mechanism but with an explicit intent to refresh.

**Independent Test**: Can be tested by installing skills, modifying a file, then running update and verifying files are restored to their embedded versions.

**Acceptance Scenarios**:

1. **Given** previously installed skill files for an agent, **When** the user runs `buildout-cli skills update --agent claude`, **Then** all skill files are replaced with the latest embedded versions
2. **Given** no previously installed skill files, **When** the user runs `buildout-cli skills update --agent claude`, **Then** the command reports that no skills were found to update and suggests running install first

---

### User Story 3 - Remove Agent Skills (Priority: P2)

A developer no longer wants buildout-cli skills in their agent configuration. They run `buildout-cli skills remove --agent claude` and the skill files are deleted from the agent config directory.

**Why this priority**: Cleanup is important for a complete lifecycle but depends on install working first.

**Independent Test**: Can be tested by installing skills then removing them and verifying files are gone.

**Acceptance Scenarios**:

1. **Given** installed skill files for an agent, **When** the user runs `buildout-cli skills remove --agent claude`, **Then** all buildout-cli skill files are deleted from the agent config directory
2. **Given** no installed skill files, **When** the user runs `buildout-cli skills remove --agent claude`, **Then** the command reports no skills were found and exits gracefully

---

### User Story 4 - MCP Embedded Prompts (Priority: P1)

An LLM client connects to the buildout MCP server and receives server instructions that guide it on how to effectively use the available tools. Prompts for the update process (the most complex tool workflow) are embedded in the executable and served as part of the MCP server configuration.

**Why this priority**: MCP prompts provide immediate value to every connected LLM client without any manual setup, and the current hardcoded instructions need to evolve into structured, embedded prompt resources.

**Independent Test**: Can be tested by inspecting the MCP server metadata and verifying that instructions are present, correctly formatted, and contain the expected guidance for tool usage.

**Acceptance Scenarios**:

1. **Given** a running MCP server, **When** a client requests server capabilities, **Then** server instructions include guidance for all available tools, with specific instructions for the update workflow
2. **Given** the MCP server binary, **When** the embedded prompt resources are inspected, **Then** prompts are loaded from embedded resources, not hardcoded strings in source code

---

### Edge Cases

- What happens when an unsupported agent name is provided (e.g., `--agent cursor`)?
- What happens when the target directory doesn't exist (e.g., `.claude/` not yet created)?
- What happens when the user lacks write permissions for the target directory?
- What happens when skill files exist but were modified by the user — does update overwrite them?
- What happens when `--global` is used and the global agent config directory doesn't exist?
- What happens when the MCP server instructions are empty or fail to load from embedded resources?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The CLI MUST provide a `skills` command with `install`, `update`, and `remove` subcommands
- **FR-002**: The `skills install` command MUST accept an `--agent` option with values `claude` or `opencode`
- **FR-003**: The `skills install` command MUST write skill markdown files to the agent's project-local configuration directory
- **FR-004**: The `skills` commands MUST accept a `--global` option that targets the user's home directory agent config instead of the project-local directory
- **FR-005**: The `skills update` command MUST replace previously installed skill files with the latest embedded versions
- **FR-006**: The `skills remove` command MUST delete previously installed skill files from the agent config directory
- **FR-007**: The CLI MUST validate the `--agent` value and reject unsupported agent names with a clear error message
- **FR-008**: Skill content MUST be stored as markdown files in the repository and embedded as resources in the CLI assembly
- **FR-009**: The MCP server MUST load its server instructions from embedded resources rather than hardcoded strings
- **FR-010**: MCP embedded prompts MUST include specific guidance for the page update workflow (the most complex tool interaction)
- **FR-011**: The MCP server MUST only load prompt resources that are relevant to its operation — no unrelated content
- **FR-012**: Each agent type MUST have its own skill file format and installation target path, appropriate for that agent's configuration conventions

### Key Entities

- **Skill**: A markdown document that teaches an LLM agent how to work with buildout-cli commands. Stored in the repository, embedded in the CLI assembly, and installed to agent configuration directories.
- **Prompt**: A markdown document embedded in the MCP assembly that provides instructions to connected LLM clients about how to use buildout MCP tools effectively.
- **Agent Target**: The destination for skill installation, determined by agent type (claude/opencode) and scope (project-local vs global). Maps to a specific directory path and file naming convention.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can install skills for a supported agent with a single command and have the agent immediately recognize buildout-cli capabilities
- **SC-002**: All three skills subcommands (install, update, remove) produce correct file system changes in under 1 second
- **SC-003**: MCP server instructions are loaded from embedded resources and contain at least the same level of detail as the current hardcoded instructions
- **SC-004**: Every skill and prompt embedded in the assembly can be verified by unit tests that confirm correct content loading and absence of unrelated content
- **SC-005**: Invalid agent names produce clear, actionable error messages that guide the user to supported values

## Assumptions

- Claude agent skills are installed to `.claude/` (project-local) or `~/.claude/` (global) — standard Claude Code convention
- OpenCode agent skills are installed to `.opencode/` (project-local) or `~/.config/opencode/` (global) — standard OpenCode convention
- Only `claude` and `opencode` agents are supported initially; other agents can be added later without architectural changes
- Skill files are standalone markdown documents — no templating or variable substitution is required
- The update process is the primary MCP tool workflow needing detailed prompt instructions, as it is the most complex and destructive operation
- Skill file content is specific to each agent type (different formatting, structure, or conventions per agent)
- The `skills` command does not need to detect which agent is currently active — the user explicitly specifies via `--agent`
