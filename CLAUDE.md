<!-- SPECKIT START -->
Active feature plan: [specs/010-configuration/plan.md](./specs/010-configuration/plan.md)

For technologies, project structure, shell commands, and other context, read
that plan and its sibling artifacts (`research.md`, `data-model.md`,
`contracts/`, `quickstart.md`). The constitution at
[.specify/memory/constitution.md](./.specify/memory/constitution.md) governs
all features.

## Integration Tests with LLM

Integration tests that require LLM access (e.g., `RegenerateBuildinClient_ProducesCleanWorkingTree`)
are skipped when `OPENROUTER_API_KEY` is not set. These tests are designed to run in CI/CD
with the environment variable configured. Do not run them locally without the API key set.
<!-- SPECKIT END -->
