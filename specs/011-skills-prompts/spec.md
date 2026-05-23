# Feature Specification: Skills & Prompts

**Feature Branch**: `011-skills-prompts`
**Created**: 2026-05-23
**Status**: Draft
**Input**: User description: "Provide embedded skills for LLM agents to work with buildout-cli and embedded prompts in MCP to instruct models how to deal with tools. CLI command: buildout-cli skills [install|remove] --agent [claude|opencode] [--local]. Skills/prompts stored as markdown in repo. Use embedded resources for prompt content in executable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Install Agent Skills (Priority: P1)

A developer wants an LLM coding agent (Claude Code or OpenCode) to understand how to use buildout-cli commands effectively. They run `buildout-cli skills install --agent claude` and the appropriate skill files are copied to the global agent configuration directory (e.g., `~/.claude/`), enabling the agent to work with buildout-cli autonomously.

**Why this priority**: Without install, the remove subcommand is useless. This is the primary entry point and must work first.

**Independent Test**: Can be fully tested by running the install command and verifying skill files appear in the expected agent configuration directory with correct content.

**Acceptance Scenarios**:

1. **Given** no agent skills installed, **When** the user runs `buildout-cli skills install --agent claude`, **Then** skill markdown files are written to the global agent config directory (`~/.claude/`)
2. **Given** no agent skills installed, **When** the user runs `buildout-cli skills install --agent opencode`, **Then** skill markdown files are written to the global agent config directory (`~/.config/opencode/`)
3. **Given** the `--local` flag, **When** the user runs `buildout-cli skills install --agent claude --local`, **Then** skill files are written to the project-local agent config directory (`.claude/`)
4. **Given** the `--local` flag and no local config directory, **When** the user runs `buildout-cli skills install --agent claude --local`, **Then** the directory is created and skill files are written
5. **Given** previously installed skill files that have not been modified by the user, **When** the user runs `buildout-cli skills install --agent claude`, **Then** existing skill files are overwritten with the latest embedded versions
6. **Given** previously installed skill files that have been modified by the user, **When** the user runs `buildout-cli skills install --agent claude`, **Then** the command reports modified files and suggests using `--overwrite` to replace them
7. **Given** previously installed skill files that have been modified by the user, **When** the user runs `buildout-cli skills install --agent claude --overwrite`, **Then** all skill files (including modified ones) are overwritten with the latest embedded versions

---

### User Story 2 - Remove Agent Skills (Priority: P2)

A developer no longer wants buildout-cli skills in their agent configuration. They run `buildout-cli skills remove --agent claude` and the skill files are deleted from the agent config directory.

**Why this priority**: Cleanup completes the lifecycle but depends on install working first.

**Independent Test**: Can be tested by installing skills then removing them and verifying files are gone.

**Acceptance Scenarios**:

1. **Given** installed skill files for an agent, **When** the user runs `buildout-cli skills remove --agent claude`, **Then** all buildout-cli skill files are deleted from the agent config directory
2. **Given** no installed skill files, **When** the user runs `buildout-cli skills remove --agent claude`, **Then** the command reports no skills were found and exits gracefully

---

### User Story 3 - MCP Embedded Prompts (Priority: P1)

An LLM client connects to the buildout MCP server and receives compact server instructions that guide it on how to effectively use the available tools. Detailed prompts for the update process (the most complex and destructive tool workflow) are loaded only when relevant, to economize context tokens.

**Why this priority**: MCP prompts provide immediate value to every connected LLM client without any manual setup. Moving from hardcoded strings to embedded resources enables maintainable, structured prompt content.

**Independent Test**: Can be tested by inspecting the MCP server metadata and verifying that compact instructions are always present, while detailed update prompts are loaded separately and only on demand.

**Acceptance Scenarios**:

1. **Given** a running MCP server, **When** a client requests server capabilities, **Then** compact server instructions are present with guidance for general tool usage
2. **Given** the MCP server binary, **When** embedded prompt resources are inspected, **Then** base server instructions and update-specific prompts are stored as separate embedded resources, not hardcoded strings
3. **Given** a running MCP server, **When** a client uses the update tool, **Then** detailed update-specific prompt content is available to guide the interaction

---

### Edge Cases

- **Unsupported agent name**: The command shows a clear error message listing supported agents
- **Target directory doesn't exist (global)**: The command shows an error — the global agent config directory must already exist
- **Target directory doesn't exist (local)**: The directory is created automatically
- **User lacks write permissions**: The command shows an error
- **Modified skill files without `--overwrite`**: The command shows an error listing modified files and suggests using `--overwrite`

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The CLI MUST provide a `skills` command with `install` and `remove` subcommands
- **FR-002**: The `skills install` command MUST accept an `--agent` option with values `claude` or `opencode`
- **FR-003**: The `skills install` command MUST write skill markdown files to the global agent config directory by default
- **FR-004**: The `skills` commands MUST accept a `--local` option that targets the project-local agent config directory instead of the global directory
- **FR-005**: The `skills` commands MUST accept an `--overwrite` option that forces replacement of user-modified skill files
- **FR-006**: The `skills install` command MUST detect when existing skill files have been modified by the user and refuse to overwrite them unless `--overwrite` is specified, showing an error with a suggestion to use the flag
- **FR-007**: The `skills remove` command MUST delete previously installed skill files from the agent config directory
- **FR-008**: The CLI MUST validate the `--agent` value and reject unsupported agent names with a clear error message
- **FR-009**: Skill content MUST be stored as markdown files in the repository and embedded as resources in the CLI assembly
- **FR-010**: Skill content MUST be the same for all supported agents — only the installation target directory differs per agent
- **FR-011**: The MCP server MUST load its base server instructions from embedded resources rather than hardcoded strings
- **FR-012**: MCP server instructions MUST be compact to economize context tokens
- **FR-013**: Detailed update-specific prompts MUST be stored as separate embedded resources and loaded only when needed, not included in the base server instructions
- **FR-014**: The MCP server MUST only load prompt resources that are relevant to its operation — no unrelated content
- **FR-015**: When installing locally (`--local`) and the target directory does not exist, the command MUST create it; when installing globally and the target directory does not exist, the command MUST show an error

### Key Entities

- **Skill**: A markdown document that teaches an LLM agent how to work with buildout-cli commands. Stored in the repository, embedded in the CLI assembly, and installed to agent configuration directories. Content is identical for all agents.
- **Prompt**: A markdown document embedded in the MCP assembly that provides instructions to connected LLM clients about how to use buildout MCP tools effectively. Base instructions are compact; detailed prompts for specific workflows (e.g., update) are loaded separately.
- **Agent Target**: The destination for skill installation, determined by agent type (claude/opencode) and scope (local vs global). Maps to a specific directory path.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can install skills for a supported agent with a single command and have the agent immediately recognize buildout-cli capabilities
- **SC-002**: Both skills subcommands (install, remove) produce correct file system changes in under 1 second
- **SC-003**: MCP server base instructions are compact (under 500 tokens) while still providing sufficient guidance for general tool usage
- **SC-004**: Update-specific prompts are not included in base server instructions and are only loaded when the update workflow is invoked
- **SC-005**: Every skill and prompt embedded in the assembly can be verified by unit tests that confirm correct content loading and absence of unrelated content
- **SC-006**: Invalid agent names produce clear, actionable error messages that guide the user to supported values
- **SC-007**: Modified skill files are protected from accidental overwrite unless the user explicitly passes `--overwrite`

## Assumptions

- Claude agent skills are installed to `~/.claude/` (global, default) or `.claude/` (local) — standard Claude Code convention
- OpenCode agent skills are installed to `~/.config/opencode/` (global, default) or `.opencode/` (local) — standard OpenCode convention
- Only `claude` and `opencode` agents are supported initially; other agents can be added later without architectural changes
- Skill files are standalone markdown documents — no templating or variable substitution is required
- The update process is the primary MCP tool workflow needing detailed prompt instructions, as it is the most complex and destructive operation
- Skill content is identical across all agents — only the installation directory differs
- The `skills` command does not need to detect which agent is currently active — the user explicitly specifies via `--agent`
- Embedded resources are always present in the binary — the failure case of missing resources is not handled at runtime
