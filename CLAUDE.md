<!-- SPECKIT START -->
Active feature plan: [specs/016-admin-ui-skeleton/plan.md](./specs/016-admin-ui-skeleton/plan.md)

For technologies, project structure, shell commands, and other context, read
that plan and its sibling artifacts (`research.md`, `data-model.md`,
`contracts/`, `quickstart.md`). The constitution at
[.specify/memory/constitution.md](./.specify/memory/constitution.md) governs
all features.

<!-- SPECKIT END -->

## Integration Tests with LLM

Tests in `tests/Buildout.IntegrationTests/Llm/` require LLM access (e.g., `RegenerateBuildinClient_ProducesCleanWorkingTree`)
and are skipped when `OPENROUTER_API_KEY` is not set. These tests are designed to run in CI/CD
with the environment variable configured. All other integration tests do not require this key.

**Running locally**: Do not run LLM tests locally without the API key set.
Set `OPENROUTER_API_KEY` in your environment to enable these tests.

**Running in CI**: The CI/CD pipeline automatically runs all tests including LLM tests
when `OPENROUTER_API_KEY` is configured.
