# Phase 1 — Data Model: Initial Page Reading

This document captures the data shapes that flow through the renderer, plus
the additive changes to `Buildout.Core/Buildin/Models/` from the feature-001
scaffold. All new shapes live in `Buildout.Core` — none in presentation
projects.

## Entities

### `Page` (modified — additive)

Located at `src/Buildout.Core/Buildin/Models/Page.cs`. Existing fields stay
unchanged. New field:

| Field | Type | Notes |
|---|---|---|
| `Title` | `IReadOnlyList<RichText>?` | Rich-text segments of the page's title-typed property. `null` when the page has no title-typed property; empty when the property exists but has zero segments. |

`MapPage` in `BotBuildinClient` is updated to populate `Title` from the
`title`-typed property in the buildin response (the property whose
discriminator is `"title"`, regardless of property *name*). All other
mapping behaviour is unchanged.

The renderer reads `Title` and treats `null` and empty identically: omit the
H1 line.

### `RichText` (modified — additive)

Located at `src/Buildout.Core/Buildin/Models/RichText.cs`. Existing fields stay
unchanged. New field:

| Field | Type | Notes |
|---|---|---|
| `Mention` | `Mention?` | Non-null **only when** `Type == "mention"`. Carries the mention-specific payload. |

Existing fields retained:

| Field | Type | Notes |
|---|---|---|
| `Type` | `string` | Discriminator: `"text"`, `"mention"`, `"equation"` (and any future buildin types). |
| `Content` | `string` | Buildin-supplied displayed plain text. Always populated. Used as fallback for unknown mention sub-types. |
| `Href` | `string?` | Optional hyperlink target when the rich-text is a link or carries one. |
| `Annotations` | `Annotations?` | Bold / italic / strikethrough / underline / code / colour. |

### `Mention` (new)

Located at `src/Buildout.Core/Buildin/Models/Mention.cs`. Discriminated union
following the existing `Block` / `PropertyValue` / `Parent` pattern.

```text
public abstract record Mention { … }
public sealed record PageMention : Mention { string PageId; }
public sealed record DatabaseMention : Mention { string DatabaseId; }
public sealed record UserMention : Mention { string UserId; string? DisplayName; }
public sealed record DateMention : Mention { string Start; string? End; }
```

- `PageId` / `DatabaseId` / `UserId` are GUID-stringified at the `BotBuildinClient`
  boundary (matching the rest of the model — see `MapPage`'s use of
  `gen.Id?.ToString() ?? string.Empty`).
- `Start` / `End` carry buildin's date string verbatim (typically ISO 8601, but
  could include time and timezone). The renderer trims to date-only when
  buildin supplies a `T…`-bearing string with `00:00:00` time, otherwise emits
  as-is. (Implementation detail — invariant is "the renderer never invents
  more precision than buildin supplied".)
- The base `Mention` record carries no shared fields; the discriminator is the
  concrete record type, matched in C# 12 pattern-matching.

`MapRichText` in `BotBuildinClient` is updated: when the generated
`RichTextItem.Type == "mention"`, inspect `RichTextItem.Mention.Type` and the
nested `Page` / `User` / `Date` payloads; instantiate the appropriate
`Mention` subclass; assign to `RichText.Mention`. When the mention type is
unknown to us (any future buildin sub-type), `Mention` stays null and the
inline renderer falls back to `RichText.Content` per spec FR-005b.

### `Block` (unchanged)

Located at `src/Buildout.Core/Buildin/Models/Block.cs`. The full set of block
records already exists from feature 001:

- Supported in this feature: `ParagraphBlock`, `Heading1Block`,
  `Heading2Block`, `Heading3Block`, `BulletedListItemBlock`,
  `NumberedListItemBlock`, `ToDoBlock`, `CodeBlock`, `QuoteBlock`,
  `DividerBlock`.
- Explicitly unsupported in v1: `ToggleBlock`, `ImageBlock`, `EmbedBlock`,
  `TableBlock`, `TableRowBlock`, `ColumnListBlock`, `ColumnBlock`,
  `ChildPageBlock`, `ChildDatabaseBlock`, `SyncedBlock`, `LinkPreviewBlock`,
  `UnsupportedBlock`.

The renderer pattern-matches on the concrete record type. Any block type the
hand-written `MapBlock` (in `BotBuildinClient`) doesn't recognise becomes
`UnsupportedBlock`, which the renderer treats the same as any other
unsupported type — placeholder per R8.

### `BotBuildinClient.MapBlockChildrenResponse` (modified — bug fix)

The current implementation returns `Results = []`. This feature fills in the
mapping by iterating the generated response's children and routing each one
through the existing `MapBlock` path. No interface change — `IBuildinClient`'s
shape is unaffected; only the implementation is corrected.

After the fix, `IBuildinClient.GetBlockChildrenAsync` returns the actual
fetched blocks plus the existing `HasMore` / `NextCursor` pagination metadata.

## Renderer-internal shapes

These types exist only inside `Buildout.Core/Markdown/` (`Conversion/` and
`Internal/` subtrees) and are not part of the public surface. The full
converter-pattern design lives in `contracts/block-converters.md`; this
section captures only the data shapes.

### `BlockSubtree`

```text
internal sealed record BlockSubtree
{
    public Block Block { get; init; } = null!;
    public IReadOnlyList<BlockSubtree> Children { get; init; } = [];
}
```

Built by the orchestrator after the fetch phase. Children are populated only
for blocks whose registered `IBlockToMarkdownConverter` opts in via
`RecurseChildren = true`. Blocks without a registered converter (i.e.
unsupported types) get `Children = []`, regardless of `Block.HasChildren` —
the placeholder represents the entire subtree per spec edge case.

### `IMarkdownRenderContext`

```text
public interface IMarkdownRenderContext
{
    IMarkdownWriter Writer { get; }
    IInlineRenderer Inline { get; }
    int IndentLevel { get; }
    IMarkdownRenderContext WithIndent(int delta);
    void WriteBlockSubtree(BlockSubtree subtree);
}
```

Passed to every `IBlockToMarkdownConverter.Write` invocation. Converters call
`WriteBlockSubtree` to recurse — the orchestrator/registry handle the
dispatch, so converters are oblivious to other converters' types.

### `MarkdownWriter`

A tiny wrapper over `StringBuilder` that provides:

- `WriteLine(string text)` — appends `text\n`.
- `WriteBlankLine()` — ensures exactly one blank line separates blocks (no
  consecutive blank lines).
- Nothing else — no indentation, no Markdown intelligence; converters and
  the inline renderer own their spacing rules.

Determinism (R10) is achieved trivially because `MarkdownWriter` only appends.

## Compatibility matrix

This is the canonical record of which buildin block types render to what
Markdown form. Tests under `tests/Buildout.UnitTests/Markdown/` reference
this matrix.

| Block / inline type | Status | Markdown form | Notes |
|---|---|---|---|
| `paragraph` | ✅ Supported | `<inline>\n` | Inline content via inline renderer. |
| `heading_1` | ✅ Supported | `## <inline>\n` | Page title takes the single H1; body headings shift down by one — heading_1 → `##`, heading_2 → `###`, heading_3 → `####`. This preserves the H1 invariant from FR-005a. |
| `heading_2` | ✅ Supported | `### <inline>\n` | See above. |
| `heading_3` | ✅ Supported | `#### <inline>\n` | See above. |
| `bulleted_list_item` | ✅ Supported | `- <inline>\n` (indented per nesting) | Children rendered indented two spaces per level. |
| `numbered_list_item` | ✅ Supported | `1. <inline>\n` (indented per nesting) | Markdown auto-numbers; we always emit `1.` for simplicity. CommonMark renderers re-number on display. |
| `to_do` | ✅ Supported | `- [ ] <inline>\n` or `- [x] <inline>\n` | GFM task-list extension. |
| `code` | ✅ Supported | ` ```<lang>\n<text>\n``` ` | Language tag emitted only if buildin provides one. |
| `quote` | ✅ Supported | `> <inline>\n` | Multi-line content prefixed line-by-line. |
| `divider` | ✅ Supported | `---\n` | CommonMark thematic break. |
| `toggle` | ❌ Unsupported v1 | `<!-- unsupported block: toggle -->\n` | Rendering toggle's collapsed/expanded states cleanly is a later-feature decision. Children of toggles are not recursed into. |
| `image` | ❌ Unsupported v1 | `<!-- unsupported block: image -->\n` | A caption-aware Markdown image (`![<caption>](<url>)`) is a clear later improvement. |
| `embed` | ❌ Unsupported v1 | `<!-- unsupported block: embed -->\n` | Per spec input scope. |
| `table` / `table_row` | ❌ Unsupported v1 | `<!-- unsupported block: table -->\n` | GFM tables are a later-feature decision; rows nested under a table are skipped (one placeholder for the whole table). |
| `column_list` / `column` | ❌ Unsupported v1 | `<!-- unsupported block: column_list -->\n` | Layout-only blocks; rendering them flattened is non-trivial and explicit later work. |
| `child_page` / `child_database` | ❌ Unsupported v1 | `<!-- unsupported block: child_page -->\n` | Subpages are not recursed into; reading them is a separate operation. |
| `synced_block` | ❌ Unsupported v1 | `<!-- unsupported block: synced_block -->\n` | |
| `link_preview` | ❌ Unsupported v1 | `<!-- unsupported block: link_preview -->\n` | |
| `unsupported` | ❌ Unsupported v1 | `<!-- unsupported block: unsupported -->\n` | Buildin's own placeholder type, mapped through unchanged. |
| **Inline `text`** | ✅ Supported | `Content` with annotations applied: `**bold**`, `*italic*`, `~~strike~~`, `` `code` ``, link form `[text](href)` if `Href` present. | Annotations stack: bold + italic = `***text***`. Underline has no CommonMark equivalent and is dropped (lossy — documented). |
| **Inline `mention` (page)** | ✅ Supported | `[<page title or display text>](buildin://<page_id>)` | Display text comes from `RichText.Content` (buildin computes it). |
| **Inline `mention` (database)** | ✅ Supported | `[<display text>](buildin://<database_id>)` | Same form; the `buildin://` URI accepts either a page or database id (they share the UUID space in buildin). |
| **Inline `mention` (user)** | ✅ Supported | `@<display name>` | Plain text; no link. If `DisplayName` is null, fall back to `RichText.Content`. |
| **Inline `mention` (date)** | ✅ Supported | `<start>` or `<start> – <end>` | En-dash separator (U+2013) for ranges. |
| **Inline `mention` (other)** | ❌ Unsupported v1 | `RichText.Content` verbatim | Display text fallback (no placeholder comment inline — would visually corrupt prose). |
| **Inline `equation`** | ❌ Unsupported v1 | `RichText.Content` verbatim | Same fallback as unknown mentions. |

### Acknowledged lossy conversions

- **Underline annotation**: dropped. CommonMark has no native underline; emitting
  `<u>…</u>` would mix HTML into prose unnecessarily for v1.
- **Heading levels shift down by one** to make room for the page-title H1.
  buildin's `heading_1` becomes `##`. This is documented as deliberate.
- **Toggle children**: not recursed into. The toggle's existence is signalled
  by the placeholder; its content is elided.
- **Table contents**: same as toggle.
- **Date timezones**: when buildin returns a date with a non-zero time
  component, the renderer emits the buildin string verbatim. We do not
  truncate to date-only for non-midnight times.

Each of these has at least one test fixture in `tests/Buildout.UnitTests/
Markdown/` asserting the documented behaviour.

## State transitions

Read-only feature. No state transitions in scope.

## Validation rules

Validation is contract-level only — FR-001's "single rendering operation"
guarantees the renderer either returns a valid Markdown string or throws a
`BuildinApiException` (or `OperationCanceledException` for cancellation). No
intermediate "partially-rendered" object is ever exposed.

- Page id format: validated by `IBuildinClient` (already throws on malformed
  GUIDs in feature 001's `MapPage` / `MapBlock`).
- Empty page: yields output that is exactly `"# <title>\n\n"` (title only) or
  `""` (no title) — see edge cases in spec.
- Cancellation: `CancellationToken` flows through every `IBuildinClient` call
  and is honoured between block-children fetches.
