# Implementation Plan: Skills & Prompts

**Branch**: `011-skills-prompts` | **Date**: 2026-05-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/011-skills-prompts/spec.md`

## Summary

Provide embedded skills for LLM agents to work with buildout-cli and embedded prompts in MCP to instruct models how to deal with tools. The CLI gets a `skills` branch command with `install` and `remove` subcommands, supporting `claude` and `opencode` agents. Skills are stored as markdown files in the repository, embedded as resources in the CLI assembly, and installed to a `buildout-cli/` subdirectory within agent config directories. The MCP server moves its hardcoded server instructions to embedded resources and adds a named MCP prompt for the update workflow.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: Spectre.Console.Cli 0.55.0 (CLI commands), ModelContextProtocol 1.2.0 (MCP prompts)
**Storage**: File system (skill markdown files as embedded resources → installed to disk)
**Testing**: xUnit v3, NSubstitute 5.3.0, Spectre.Console.Testing 0.55.0
**Target Platform**: Cross-platform (.NET 10 — macOS, Linux, Windows)
**Project Type**: CLI tool + MCP server (existing projects, new functionality)
**Performance Goals**: Skill install/remove < 1 second (file I/O only)
**Constraints**: MCP base instructions < 500 tokens; no network calls during skill operations
**Scale/Scope**: ~5-8 skill files, ~2 prompt files; single developer tool

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Core/Presentation Separation | **PASS** | Skills management is CLI-presentation logic (file I/O, no domain). MCP prompts are MCP-presentation logic. No changes to `Buildout.Core`. |
| II. LLM-Friendly Output Fidelity | **N/A** | No Markdown conversion or block rendering involved. |
| III. Bidirectional Round-Trip Testing | **N/A** | No block types involved. |
| IV. Test-First Discipline | **PASS** | Plan includes unit tests for skill install/remove logic and embedded resource loading, plus integration tests for CLI commands and MCP prompt registration. |
| V. Buildin API Abstraction | **N/A** | No buildin.ai API calls in skills or prompts. |
| VI. Non-Destructive Editing | **N/A** | No page editing involved. |
| CLI framework | **PASS** | `skills` command uses Spectre.Console.Cli branch pattern (same as existing `db` branch). |
| Solution layout | **PASS** | New files in `Buildout.Cli/` and `Buildout.Mcp/` only. No new projects. |
| VII. Skills & Prompts Parity | **PASS** | Feature creates the skills and prompts infrastructure itself. Every CLI subcommand gets a skill file; the update tool gets a named prompt. |
| MCP tool changes | **N/A** | No tool signature changes. Adding a prompt handler only. |

**Gate result**: PASS — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/011-skills-prompts/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── cli-skills.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
  Buildout.Cli/
    Buildout.Cli.csproj          # Add <EmbeddedResource> for Skills/*.md
    Commands/
      SkillsSettings.cs          # Base settings for skills branch
      SkillsInstallCommand.cs    # install subcommand
      SkillsRemoveCommand.cs     # remove subcommand
    Skills/                      # Embedded markdown skill files
      SKILL.md                   # Entrypoint — common info + references to other skills
      create.md
      read.md
      update.md
      delete.md
      restore.md
      search.md
      database-views.md
    Program.cs                   # Register skills branch command

  Buildout.Mcp/
    Buildout.Mcp.csproj          # Add <EmbeddedResource> for Prompts/*.md
    Prompts/
      BuildoutPrompts.cs         # [McpServerPromptType] handler
    Prompts/                     # Embedded markdown prompt files
      server-instructions.md     # Compact base instructions
      update.md                  # Detailed update workflow prompt
    Program.cs                   # Load instructions from embedded, add WithPrompts<>

tests/
  Buildout.UnitTests/
    Cli/
      SkillsInstallCommandTests.cs
      SkillsRemoveCommandTests.cs
      EmbeddedResourceTests.cs   # Verify skill/prompt content loads correctly
    Mcp/
      BuildoutPromptsTests.cs    # Verify prompt content, base vs update separation
  Buildout.IntegrationTests/
    Cli/
      SkillsCommandTests.cs      # End-to-end CLI install/remove via temp dirs
    Mcp/
      McpServerMetadataTests.cs  # Extend existing: verify instructions from resources
      McpPromptTests.cs          # Verify named prompt is registered and returns content
```

**Structure Decision**: New files added to existing `Buildout.Cli` and `Buildout.Mcp` projects. No new projects needed. Skill markdown files live alongside CLI code as embedded resources; prompt markdown files live alongside MCP code as embedded resources.

## Complexity Tracking

No violations — table not applicable.
