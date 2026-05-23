# Quickstart: Skills & Prompts

**Feature**: 011-skills-prompts

## Prerequisites

- .NET 10 SDK
- Buildout.Cli built from this branch

## Install Skills for Claude (global)

```bash
dotnet run --project src/Buildout.Cli -- skills install --agent claude
```

Skill files are written to `~/.claude/skills/buildout-cli/` (e.g., `create.md`, `update.md`, etc.).

## Install Skills for OpenCode (local to project)

```bash
dotnet run --project src/Buildout.Cli -- skills install --agent opencode --local
```

Skill files are written to `.opencode/skills/buildout-cli/` in the current directory.

## Update Skills

```bash
dotnet run --project src/Buildout.Cli -- skills install --agent claude --overwrite
```

## Remove Skills

```bash
dotnet run --project src/Buildout.Cli -- skills remove --agent claude
```

## MCP Server (prompts auto-loaded)

Start the MCP server — server instructions are loaded from embedded resources automatically:

```bash
dotnet run --project src/Buildout.Mcp
```

Clients can request the `update` named prompt for detailed update workflow instructions.

## Run Tests

```bash
dotnet test --filter "FullyQualifiedName~Skills"
dotnet test --filter "FullyQualifiedName~BuildoutPrompts"
dotnet test --filter "FullyQualifiedName~EmbeddedResource"
```
