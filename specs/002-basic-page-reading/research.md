# Phase 0 — Research: Initial Page Reading

This document records the technical decisions resolved before implementation
begins. Every "NEEDS CLARIFICATION" item the Technical Context surfaced is
addressed here.

## R1 — Markdown library for the CLI's rich-mode renderer

**Decision**: Use **Markdig** (latest stable on .NET 10) in `Buildout.Cli` only,
plus a small custom walker that maps Markdig's AST to `Spectre.Console`
primitives (`AnsiConsole.Write`, `Markup`, `Rule`, code-block panels). No first-
party Markdown widget exists in `Spectre.Console` 0.55.

**Rationale**:

- Markdig is the de-facto standard CommonMark parser for .NET, well-maintained,
  permissively licensed (BSD-2-Clause).
- Parsing the Markdown the core just produced (rather than walking blocks again)
  preserves Constitution Principle I: presentation never touches buildin block
  shapes.
- A custom walker is small (≤ 200 LOC) because the supported-block subset is
  small. We do not need a general-purpose CommonMark renderer.

**Alternatives considered**:

- *Use `Spectre.Console.Markup` directly on the Markdown string* — rejected:
  Spectre's `[bold]…[/]` markup is **not** a Markdown subset. Treating Markdown
  as Markup would corrupt brackets and miss every formatting cue.
- *Walk the buildin block tree from `Buildout.Cli`* — rejected: violates
  Constitution Principle I ("presentation MUST NOT parse blocks").
- *Third-party `Spectre.Console.Markdown` community packages* — rejected after
  inspection: existing packages are either abandoned, target older Spectre
  versions, or render with quirks (broken nested lists, no language hint on
  fenced code). The maintenance cost of pinning to one of them exceeds the cost
  of the ~150-line custom walker.
- *Use Spectre's `Tree`/`Panel` directly without Markdown* — rejected: would
  require Cli to know block shapes (Principle I again) or maintain a parallel
  intermediate format.

**Implementation invariants**:

- Markdig parses the Markdown produced by Core; the parser sees nothing buildin-
  specific.
- The walker emits to `IAnsiConsole` (test-injectable); unit tests verify the
  styled output by capturing `AnsiConsole.Console` against a fake.
- The walker handles: H1/H2/H3, paragraphs (with inline bold/italic/strike/
  code/link), bulleted/numbered/task lists with nesting, fenced code blocks
  (with optional language tag), block quotes, dividers (thematic break), and
  CommonMark inline links (which is how page mentions appear after Core
  rendering).
- Anything outside this set falls through to plain text — i.e., the placeholder
  comments produced by Core for unsupported blocks render verbatim. This is
  correct: HTML comments are visible-but-unstyled in a terminal.

## R2 — MCP resource template registration

**Decision**: Register the resource template through the
`ModelContextProtocol` SDK 1.2.0's resource-handler API at host construction
time in `Buildout.Mcp/Program.cs`. The handler is implemented as a hosted
class (`PageResourceHandler`) injected via `Microsoft.Extensions.DependencyInjection`.

**Rationale**:

- SDK 1.2.0 is already referenced by `Buildout.Mcp.csproj` (added in feature
  001). It exposes both attribute-based (`[McpServerResource]`) and
  builder-based (`McpServerBuilder.WithResource(...)`) registration.
- A separate handler class keeps the Program.cs surface narrow and is the
  cleanest fit for DI; it is also easy to integration-test with an in-process
  MCP client.
- The URI template `buildin://{page_id}` is registered as a *resource template*
  (with `{page_id}` as a templated segment) so MCP clients see it in
  `resources/templates/list` rather than enumerating concrete page URIs.

**Alternatives considered**:

- *Attribute-decorated static method on `Program`* — works, but DI is awkward
  for the renderer dependency, and tests need to spin up the full host. Class
  form is cleaner.
- *Enumerate resources rather than use a template* — rejected: the buildin
  workspace can have unbounded pages; enumeration is wrong here. Templates are
  the correct MCP construct.

**Implementation invariants**:

- The handler depends on `IPageMarkdownRenderer` and a logger; nothing buildin-
  specific.
- On read, the handler returns one `TextResourceContents` with MIME type
  `text/markdown` and body = the renderer's output.
- On `BuildinApiException`, the handler maps the typed error category to an MCP
  error response (404 → `Resource not found`; 401/403 → `Forbidden`; transport
  → `Internal error`); it never returns a 200 with an error blob in the body
  (FR-012).
- The integration test uses the SDK's in-process server/client harness — no
  network, no stdio plumbing in tests.

## R3 — Cheap testing LLM provider for the MCP integration test

**Decision**: Use **Anthropic Claude Haiku 4.5** (model id
`claude-haiku-4-5-20251001`) via the `Anthropic.SDK` NuGet package, gated on
the `ANTHROPIC_API_KEY` environment variable. Tests skip (not fail) when the
key is absent.

**Rationale**:

- Constitution principle IV calls for "a cheap testing LLM" for MCP protocol-
  regression coverage; spec SC-002 commits to "answer at least one factual
  question per supported block type from the rendered output". Haiku 4.5 is
  inexpensive, fast (typically < 1 s per call for short prompts), and capable
  enough to read a small Markdown document and answer factual questions.
- Skipping (rather than failing) on a missing key keeps the suite green for
  contributors who haven't set up an Anthropic account, while still giving full
  coverage on CI / maintainer machines that have a key configured.

**Alternatives considered**:

- *OpenAI gpt-4o-mini* — viable, but adds a non-Anthropic dependency to the
  project for what amounts to one assertion. The ergonomics of the Anthropic
  SDK are simpler in this context.
- *Self-hosted small open-source model* — rejected: heavyweight to set up,
  flaky on contributor laptops, and a poor fit for "cheap" in operational terms
  (compute cost on developer hardware).
- *Skip the LLM test entirely* — rejected: violates spec SC-002 and is the
  exact regression class the constitution warns about.

**Implementation invariants**:

- The integration test issues exactly **one** Haiku call, with one short
  prompt, asking the LLM to answer N factual questions sourced from the
  fixture. Cost target: well under one cent per run.
- When `ANTHROPIC_API_KEY` is unset, the test is reported as skipped (xUnit
  `Skip` attribute or `Skip.IfNull`) — never silently passed.
- The cheap-LLM dependency lives only in `Buildout.IntegrationTests`. Neither
  `Buildout.Core` nor the presentation projects reference Anthropic.
- The fixture page used for the test is small (~10 supported blocks covering
  the spec's first-class set) so the prompt fits easily under Haiku's context
  window and finishes within the SC-006 30-second budget.

## R4 — Terminal capability detection

**Decision**: Delegate TTY / styled-terminal detection to
`Spectre.Console.AnsiConsole.Profile`. Read `Capabilities.Ansi` and
`Capabilities.Interactive` and treat the combination as the styled-output
signal. Honour the `NO_COLOR` env var by deferring to Spectre's own handling
(it already respects `NO_COLOR`).

**Rationale**:

- Spectre is already a constitution-mandated dependency; reusing its detection
  avoids reinventing TTY/`isatty()` logic and the nest of edge cases (Windows
  legacy console, redirected stderr but TTY stdout, Cygwin, dumb terminals).
- `NO_COLOR` is the documented opt-out across the ecosystem; Spectre honours it.
- Wrapping Spectre's profile in a thin `TerminalCapabilities` class lets unit
  tests inject a fake.

**Alternatives considered**:

- *Roll our own using `System.Console.IsOutputRedirected`* — works for the
  pipe/redirect case but misses `NO_COLOR`, dumb terminals, and Windows
  legacy.
- *Always render styled and let `NO_COLOR` strip codes downstream* — rejected:
  the spec explicitly forbids escape codes when stdout is non-TTY (FR-007),
  and an emitted-then-stripped flow still puts escapes in pipes mid-flight.

**Implementation invariants**:

- Detection happens once at command entry; the result is captured in an
  immutable struct and passed to the renderer.
- The plain-mode renderer is `Console.Out.Write(markdown)` (no Spectre at all);
  this guarantees zero escape codes by construction.
- The styled-mode renderer drives `IAnsiConsole`, which is the test seam.

## R5 — Page title extraction

**Decision**: Add a `Title` property of type `IReadOnlyList<RichText>?` to the
hand-written `Page` model (mirroring `Database.Title` already there from feature
001). `BotBuildinClient.MapPage` populates it by locating the title-typed
property in the buildin response (the property whose `type` discriminator is
`"title"`) and mapping its rich-text array via the existing `MapRichText`.

**Rationale**:

- Buildin pages have exactly one title-typed property; the property *name* can
  vary by database (e.g. "Name", "Title", "标题"), but the *type* discriminator
  is invariant.
- Mirroring `Database.Title` keeps the domain model uniform and easy to consume
  from the renderer.
- The title rendering rule (FR-005a) says: if no title, omit the H1 line. A
  null/empty `Title` collection naturally encodes "no title".

**Alternatives considered**:

- *Expose the full `Properties` dictionary on `Page`* — rejected as
  premature: this feature only needs the title; the wider properties surface
  belongs with database-row reading work later.
- *Compute the title inside the renderer by reading raw Kiota objects* —
  rejected: violates Principle I/V (Core's hand-written surface is the seam).

**Implementation invariants**:

- `Page.Title` may be null (page has no title-typed property at all) or empty
  (title property is present but has zero rich-text segments). Both produce
  "no H1 line" in rendered output, per FR-005a.
- The mention-aware inline renderer is reused for the title's rich-text, so
  page titles containing inline mentions (rare but possible) render correctly.

## R6 — RichText mention metadata

**Decision**: Extend the hand-written `RichText` model with an optional
`Mention` field of type `Mention?` (a discriminated union: `PageMention`,
`DatabaseMention`, `UserMention`, `DateMention`). `BotBuildinClient.MapRichText`
populates this from `Gen.RichTextItem.Mention` when the rich-text item's type is
`"mention"`. The existing `Type`, `Content`, `Href`, `Annotations` fields stay
unchanged — `Content` continues to carry the buildin-supplied displayed plain
text, which is the fallback used by the inline renderer if a mention sub-type
is unknown.

**Rationale**:

- The Kiota-generated `RichTextItem.Mention` already exposes
  `Page.PageId`, `User.{Id, Name, …}`, `Date.{Start, End}`, and a `Type`
  discriminator. We just need to surface this through the hand-written model.
- A discriminated union (abstract record + sealed records) matches the
  pattern already used for `Block`, `PropertyValue`, and `Parent` in
  `Buildout.Core/Buildin/Models/`.
- The fallback through `Content` means an unknown mention type never produces a
  placeholder comment in mid-paragraph (which would look ugly); it produces the
  user-visible plain text buildin already computed.

**Alternatives considered**:

- *Single `MentionPageId` field on `RichText`* — rejected: doesn't generalise to
  user/date mentions without a sprawl of optional fields.
- *Reach into `AdditionalData` at render time* — rejected: same Principle V
  issue; presentation/renderer would depend on Kiota internals.

**Implementation invariants**:

- `Mention` is non-null **only when** `RichText.Type == "mention"`.
- `Content` is always populated regardless of mention sub-type.
- `Href` may co-exist with a `Mention` (e.g. some buildin clients send a URL
  alongside a page mention); the renderer's rule (FR-005b) prefers the
  `buildin://` link over `Href` for page/database mentions.

## R7 — Pagination strategy for the full block tree

**Decision**: Add an internal helper inside the renderer
(`PageMarkdownRenderer.FetchAllChildrenAsync(blockId, ct)`) that loops
`IBuildinClient.GetBlockChildrenAsync` over `NextCursor` until exhausted, and
recurses into `HasChildren = true` blocks of supported types (toggle is the
notable case among non-supported blocks where children matter — but toggle is
unsupported in v1, so recursion only follows supported parent block types
that can nest: bulleted/numbered/todo list items, quote, and the page itself).

**Rationale**:

- Spec FR-001 + Q2 clarification require always-full fetch with no caching.
  A simple iterative loop satisfies this directly.
- Sequential issuance (no concurrent fetches) keeps within the
  spec's deferred-performance scope.
- Limiting recursion to supported nestable parents avoids fetching children we'd
  only render as a placeholder anyway (the placeholder represents the entire
  unsupported subtree per spec edge case).

**Alternatives considered**:

- *Recurse into every block with `HasChildren` regardless of type* — rejected:
  unnecessary requests for unsupported blocks whose entire subtree we render as
  one placeholder line.
- *Concurrent / parallel fetching* — explicitly out of scope (Q2).
- *Stream blocks to the writer as they arrive* — rejected: not in scope; full
  fetch then render is simpler and matches the spec's correctness focus.

**Implementation invariants**:

- The page's *direct* children are fetched via
  `GetBlockChildrenAsync(pageId, …)` because a buildin page-as-block is the
  parent of its body blocks.
- For each fetched block of a supported nestable type with `HasChildren = true`,
  the helper recurses with that block's id.
- The unit test for pagination uses a mock `IBuildinClient` returning
  `HasMore = true` with a non-null `NextCursor` for the first call and
  `HasMore = false` for the second; the renderer must exhaust both.

## R8 — Unsupported-block placeholder format

**Decision**: Render unsupported blocks (and unsupported inline mentions where
no displayed text is available) as the single line:

```text
<!-- unsupported block: <type> -->
```

surrounded by blank lines so it survives CommonMark parsing as an HTML block.

**Rationale**:

- HTML comments are valid CommonMark, render as blanks in most viewers, and are
  visible in raw text — aligning with the spec's requirement that loss is
  observable.
- A consistent format simplifies the test ("does it match `<!-- unsupported
  block: image -->`?") and the future writing tool's round-trip handling.

**Alternatives considered**:

- *Render as a fenced code block with a label* — rejected: heavier visual
  weight; clutters real code blocks.
- *Render as plain text "[unsupported: image]"* — rejected: inline-only; doesn't
  preserve block-level position cleanly.
- *Skip silently* — rejected: spec edge case explicitly forbids silent loss.

**Implementation invariants**:

- The exact placeholder string is owned by one method
  (`UnsupportedBlock.Format(string blockType)`) so both Core unit tests and
  any future round-trip tests reference one source of truth.
- The placeholder is the same on both surfaces (Core produces it once).

## R9 — CLI exit code mapping

**Decision**: Map `BuildinApiException` (already typed by feature 001) into
distinct CLI exit codes per FR-009:

| Failure class | Exit code |
|---|---|
| Success | 0 |
| Page not found (HTTP 404) | 3 |
| Auth failure (HTTP 401 / 403) | 4 |
| Transport failure (network, timeout, 5xx) | 5 |
| Unexpected error (parsing, deserialisation, etc.) | 6 |

Exit code 1 is reserved for command-parser errors that Spectre.Console.Cli
emits itself; 2 is reserved for argument validation. We avoid colliding.

**Rationale**:

- Distinct exit codes let scripts handle these cases programmatically — the
  whole point of FR-009.
- Aligning "auth" and "transport" with the typed `BuildinError` discriminator
  in `BuildinApiException` (already there from feature 001) keeps the mapping
  trivial: one `switch` on the discriminator.
- Codes 3–6 leave 1 and 2 to Spectre's defaults, avoiding ambiguity.

**Alternatives considered**:

- *Use 1 for everything non-zero* — rejected: violates FR-009.
- *Use sysexits.h-style codes (64–78)* — rejected: not a Unix convention
  buildout has elsewhere; would surprise users.

**Implementation invariants**:

- Exit code mapping is a switch in `GetCommand.ExecuteAsync` — not in
  `Buildout.Core`. The renderer throws typed exceptions; the CLI translates.
- The MCP handler uses the same switch over `BuildinError` to choose its
  MCP-protocol error category (FR-012).

## R10 — Determinism guarantees for re-rendering

**Decision**: Determinism (FR-004 / SC-001 by way of "no silent loss") is
enforced by:

1. Stable iteration order over fetched block lists (insertion order; we never
   sort or reorder).
2. No use of `Dictionary` enumeration in the rendering hot path (use
   `IReadOnlyList` only for sequenced data).
3. No string interpolation of mutable state (e.g. timestamps, GUIDs) into the
   output for supported blocks.

A dedicated test (`DeterminismTests.cs`) renders the same fixture twice and
asserts byte-identical output.

**Rationale**: The constitution and spec both emphasise deterministic
Markdown. Stating these invariants explicitly here makes them gateable in
review.

**Alternatives considered**:

- *Hash-and-compare* — same effect, but byte-identity is stricter and the
  fixture is small enough.
- *No determinism test* — rejected: spec FR-004 is direct.

## R11 — Anthropic.SDK package selection

**Decision**: Use the [`Anthropic.SDK`](https://www.nuget.org/packages/Anthropic.SDK)
NuGet package (community-maintained, MIT-licensed) referenced from
`Buildout.IntegrationTests` only. Pin the latest stable release that supports
Claude Haiku 4.5.

**Rationale**:

- Anthropic does not yet ship a first-party .NET SDK; `Anthropic.SDK` is the
  most active community option and supports the Messages API used by Haiku
  4.5.
- The dependency surface is intentionally test-only; downstream consumers of
  `Buildout.Core` / `Buildout.Cli` / `Buildout.Mcp` never see it.

**Alternatives considered**:

- *Hand-write a minimal HttpClient-based caller* — viable; rejected for
  maintenance cost (auth header, message-format evolution, retry).
- *Use `OpenAI` SDK pointing at an Anthropic-compatible endpoint* — rejected;
  fragile and indirect.

**Implementation invariants**:

- The integration test uses `Anthropic.SDK` exclusively for the LLM call;
  it does **not** wrap or abstract the call (no `ILlmClient` introduced).
- API key is read from `ANTHROPIC_API_KEY`. No other auth path.

## R12 — Stdio / HTTP transport for the MCP integration test

**Decision**: Use the SDK's **in-process** server/client pair for the MCP
integration test (no stdio fork, no HTTP loopback). The stdio transport is
exercised separately by the existing smoke test and manual validation; for the
resource integration test, in-process is sufficient and fastest.

**Rationale**:

- The contract being tested is *protocol-level* (resource list shape, read
  response shape, error mapping), not *transport-level*. The SDK's in-process
  harness is purpose-built for this.
- Stays well under the SC-006 30-second budget.

**Alternatives considered**:

- *Spawn the MCP host over stdio* — rejected: process startup is slow and
  doesn't add coverage for the protocol contract.
- *Run an HTTP server on a random port* — rejected: same issue plus port
  flakiness.

## Open / deferred items

The following spec-level Outstanding items from `/speckit-clarify` are
explicitly **not** addressed in research because they remain low-impact:

- **Rate limiting / 429 handling** — buildin's Bot API may rate-limit. v1
  treats 429 as a transport-class error (no retry, no backoff). A future
  feature can layer retry behind `IBuildinClient`'s implementation. Spec
  FR-009 / FR-012 already cover error reporting.
- **Observability / structured logging** — `BotBuildinClient` already takes an
  `ILogger`; the renderer will too. No structured-event schema is defined in
  v1. A future "telemetry" feature can lift this.
