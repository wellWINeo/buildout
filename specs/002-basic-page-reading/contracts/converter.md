# Contract — `IPageMarkdownRenderer`

The single seam between `Buildout.Core` and the two presentation projects for
this feature. Both `Buildout.Mcp` and `Buildout.Cli` consume only this
interface for rendering.

## Public surface

Located at `src/Buildout.Core/Markdown/IPageMarkdownRenderer.cs`.

```text
namespace Buildout.Core.Markdown;

public interface IPageMarkdownRenderer
{
    Task<string> RenderAsync(
        string pageId,
        CancellationToken cancellationToken = default);
}
```

The default implementation is `PageMarkdownRenderer` in the same namespace,
constructed with `IBuildinClient` and `ILogger<PageMarkdownRenderer>`.
Registration via `ServiceCollectionExtensions.AddBuildoutCore(...)` (extending
the existing extension method from feature 001).

## Behavioural contract

### Inputs

- `pageId` — a buildin page identifier in any form `IBuildinClient` accepts
  (UUID with or without dashes). Validation is delegated to the client.
- `cancellationToken` — propagated to every `IBuildinClient` call inside the
  fetch loop. The renderer **does not** call `cancellationToken.ThrowIfCancellationRequested()`
  itself; cancellation arrives as `OperationCanceledException` from the client.

### Outputs

A non-null, non-empty (modulo true empty pages — see below) `string` of
deterministic CommonMark + GFM:

1. If the page has a title, the string starts with `# <title>\n\n`.
2. Followed by the rendered body — each block contributes a section of
   Markdown ending in `\n`, with exactly one blank line separating
   block-level constructs.
3. The rendered string ends with a trailing newline (`\n`) when non-empty.

A page with no title and no blocks renders to the empty string. A page with a
title but no blocks renders to `"# <title>\n\n"`.

### Determinism (FR-004 / R10)

For a fixed sequence of `IBuildinClient` responses (i.e. the same pagination
and block payloads), `RenderAsync` MUST return byte-identical strings on
repeat invocations. Verified by `DeterminismTests.cs`.

### Pagination (FR-001 / Q2 / R7)

The renderer:

1. Calls `IBuildinClient.GetPageAsync(pageId, ct)` exactly once to obtain the
   page object (used for the title).
2. Calls `IBuildinClient.GetBlockChildrenAsync(pageId, query, ct)` repeatedly,
   advancing `query.StartCursor` to the previous response's `NextCursor`,
   until `HasMore == false`.
3. For each fetched block of a supported nestable type with
   `HasChildren == true`, repeats step 2 with the block's id.

`PageSize` is left null (uses the buildin server default). v1 does not tune
page size.

### Error contract (FR-009 / FR-012)

Errors propagate from `IBuildinClient` as `BuildinApiException` with the typed
`BuildinError` discriminator (`NotFound`, `Forbidden`/`Unauthorized`,
`TransportError`, `UnknownError`, `ApiError`). The renderer:

- Does **not** catch `BuildinApiException`. It bubbles out; the caller
  (`GetCommand` / `PageResourceHandler`) maps it to its own surface convention.
- Does **not** catch `OperationCanceledException`. It bubbles out unchanged.
- May throw `InvalidOperationException` only for genuine logic errors
  (unreachable branches the static type system can't prove). These are bugs;
  no caller is expected to recover.

The renderer never throws `BuildinApiException` itself; it only forwards.

### Order

Blocks are rendered in fetched order. The renderer never reorders, sorts, or
deduplicates. Within a paginated fetch, page boundaries are invisible — the
output is identical to a single mega-fetch returning the same blocks in the
same order.

## Internal collaborators (informational)

The implementation splits into two phases — fetch (async, I/O) then render
(sync, pure) — connected by an in-memory `BlockSubtree` materialisation. The
render phase is OCP-shaped: per-block-type converter classes dispatched
through a registry. See `block-converters.md` for the full design.

- `PageMarkdownRenderer` — orchestrates the two phases. Fetches the page via
  `IBuildinClient.GetPageAsync`, then recursively fetches every level of
  children via `IBuildinClient.GetBlockChildrenAsync` (paginating to
  exhaustion), assembling the result into a `BlockSubtree` in memory. Then
  walks the subtree synchronously, dispatching each block to a registered
  `IBlockToMarkdownConverter` via `BlockToMarkdownRegistry`. The orchestrator
  is the only place that knows about `IBuildinClient`; converters never see
  it.
- `BlockToMarkdownRegistry` — keyed dispatch by `Block` CLR type. Built from
  `IEnumerable<IBlockToMarkdownConverter>` injected by DI; contains zero
  conditional logic. Returns `null` for types without a registered converter
  (the orchestrator then falls through to `UnsupportedBlockHandler`).
- `IBlockToMarkdownConverter` — one implementation per supported block type,
  each in its own file under `Buildout.Core/Markdown/Conversion/Blocks/`.
  See `block-converters.md`.
- `IMentionToMarkdownConverter` + `MentionToMarkdownRegistry` — same pattern
  for the four supported mention sub-types. Used by `InlineRenderer`.
- `InlineRenderer` — walks `IReadOnlyList<RichText>`, applies CommonMark
  annotation wrapping (bold, italic, strike, inline code, link) for `text`
  items, dispatches `mention` items through the mention registry, and falls
  through to `RichText.Content` for unknown rich-text or mention sub-types.
- `UnsupportedBlockHandler` — single source of truth for the placeholder
  format `<!-- unsupported block: <type> -->`. The orchestrator calls it
  whenever the block registry returns `null`. (No `IBlockToMarkdownConverter`
  is registered for the unsupported types — they get the fallback by
  construction.)
- `MarkdownWriter` — tiny append-only writer (`StringBuilder` underneath) with
  helpers for blank-line spacing. Determinism (R10) follows from
  append-only semantics.

These are *not* part of the public surface and may be refactored freely; only
`IPageMarkdownRenderer.RenderAsync` is contractual.

## Open / closed: how to add a new supported block type

In one PR, after this feature ships, adding (say) `image` support is:

1. Create `src/Buildout.Core/Markdown/Conversion/Blocks/ImageConverter.cs` that
   implements `IBlockToMarkdownConverter` for `ImageBlock`.
2. Register it in `ServiceCollectionExtensions.AddBuildoutCore(...)` — one
   line.
3. Add `tests/Buildout.UnitTests/Markdown/Blocks/ImageConverterTests.cs`.
4. Update the compatibility matrix in `data-model.md`.

Zero existing files are modified except the DI registration line and the
matrix doc. This is the point of the OCP design.

## Test obligations

Per-converter unit tests live next to their converters under
`tests/Buildout.UnitTests/Markdown/Blocks/` and `…/Mentions/`. Each test
class targets exactly one converter and exercises the cases enumerated in
`block-converters.md`'s § Test obligations. Cross-cutting tests live one
level up:

| Test class | Path | Purpose |
|---|---|---|
| `Markdown/Blocks/<Type>ConverterTests` | `tests/Buildout.UnitTests/Markdown/Blocks/` | One file per supported block-type converter. Asserts the per-block output matches the compatibility matrix. |
| `Markdown/Mentions/<Sub>MentionConverterTests` | `tests/Buildout.UnitTests/Markdown/Mentions/` | One file per supported mention sub-type. Asserts inline output (`[title](buildin://…)`, `@name`, ISO date, etc.). |
| `BlockToMarkdownRegistryTests` | `…/Markdown/BlockToMarkdownRegistryTests.cs` | Lookup by `Block` CLR type; returns `null` for unregistered types; rejects duplicate registrations at construction. |
| `MentionToMarkdownRegistryTests` | `…/Markdown/MentionToMarkdownRegistryTests.cs` | Same shape for mentions. |
| `InlineRendererTests` | `…/Markdown/InlineRendererTests.cs` | Annotation stacking, link form, fallback to `Content` for unknown rich-text or mention types. Uses real (DI-built) registry. |
| `UnsupportedBlockHandlerTests` | `…/Markdown/UnsupportedBlockHandlerTests.cs` | Placeholder format; placeholder survives a CommonMark parse without errors. Each unsupported block type listed in `data-model.md` has one assertion-row here. |
| `PageMarkdownRendererTests.PageTitle` | `…/Markdown/PageMarkdownRendererTests.cs` | H1 prepended when title present; H1 omitted when title null/empty; mention-bearing title renders correctly. |
| `PageMarkdownRendererTests.Pagination` | (same file) | Multi-page `GetBlockChildrenAsync` is fully drained; recursion follows the per-converter `RecurseChildren` opt-in (see `block-converters.md`); unsupported parents are not recursed. |
| `PageMarkdownRendererTests.Determinism` | (same file) | Two consecutive `RenderAsync` calls against the same mock produce byte-identical strings. |
| `BotBuildinClientTests` (extended) | `tests/Buildout.UnitTests/Buildin/BotBuildinClientTests.cs` | Added: `MapBlockChildrenResponse` returns the mapped block list (the bug fix); `MapPage` populates `Title` from the title-typed property; `MapRichText` populates `Mention` for each mention sub-type. |

Every test in this section uses `NSubstitute` to mock `IBuildinClient`
directly. No HTTP layer is exercised in this feature's unit tests.

## Configuration / DI

`ServiceCollectionExtensions.AddBuildoutCore(...)` (existing) is extended to
register `IPageMarkdownRenderer` → `PageMarkdownRenderer` as a singleton.
Adding the renderer to DI is the single point of contact for both
presentation projects.
