# Contract: `scripts/regenerate-buildin-client.sh`

A single-purpose script that regenerates the buildin API client from the
repo-local `openapi.json` using Kiota.

## Invocation

```bash
./scripts/regenerate-buildin-client.sh
```

No arguments. No environment variables required. Exits 0 on success, non-zero
on any failure.

## Behaviour

1. Verifies `openapi.json` exists at the repo root; aborts otherwise.
2. Verifies the Kiota local tool is restored (`dotnet tool restore` if not).
3. Invokes the canonical Kiota generator command (see
   `kiota-generation.md`).
4. Prints a summary of files changed under
   `src/Buildout.Core/Buildin/Generated/` (suggesting `git diff` for review).
5. Does NOT auto-commit; that is left to the developer per the project's
   commit hygiene rules.

## Determinism guarantees

- Same input + same tool version ⇒ byte-identical output (modulo line
  endings on Windows; Kiota emits LF in v1, the script forces LF).
- Re-running with no `openapi.json` change leaves git's working tree clean.

## Failure modes

| Failure | Behaviour |
|---|---|
| `openapi.json` missing | Print path; exit 1. |
| `dotnet` not on PATH | Print install hint; exit 1. |
| Local tool restore fails | Print error; exit 1. |
| Kiota CLI exits non-zero | Forward Kiota's stderr; exit with Kiota's code. |
| Output unchanged but unexpected files appear in `Generated/` | Caught by determinism integration test; not the script's job. |

## Companion file

A PowerShell equivalent `scripts/regenerate-buildin-client.ps1` mirrors this
contract for Windows users (matches the existing convention in
`.specify/extensions/git/scripts/{bash,powershell}`).

## Test coverage

- Smoke test in `tests/Buildout.IntegrationTests` runs the script in a temp
  copy of the repo and asserts:
  - exit 0,
  - working-tree clean (against the committed `Generated/` snapshot),
  - non-empty `Generated/` directory.
- An optional CI gate (out of scope for this feature) would re-run the
  script in CI and fail the build if `git status` is dirty after regen. We
  document this hook so the future CI feature can pick it up.
