# Contract: `IMarkdownToBlocksParser` (core)

**Feature**: [spec.md](../spec.md) · [plan.md](../plan.md)

Pure function from a Markdown string to an `AuthoredDocument` (title +
top-level block tree). No I/O, no buildin client involvement. Lives
under `Buildout.Core.Markdown.Authoring` so it can be unit-tested in
isolation and reused by the round-trip suite (Principle III).

---

## Surface

```csharp
namespace Buildout.Core.Markdown.Authoring;

public interface IMarkdownToBlocksParser
{
    AuthoredDocument Parse(string markdown);
}
```

`AuthoredDocument` is documented in [data-model.md](../data-model.md).

---

## Pipeline

1. **Parse with Markdig** using a `MarkdownPipeline` configured for
   CommonMark + GFM task-list (and only those — no autolinks-extra,
   no smarty-pants, no emoji shortcodes). The configured pipeline is
   a singleton instance owned by the implementation; it is identical
   call-to-call so output is deterministic.

2. **Extract the title** per R2:
   - Skip any leading `LinkReferenceDefinitionGroup`.
   - If the next AST child is a `HeadingBlock` with `Level == 1`,
     capture its inline runs as the candidate title text (joined via
     the inline renderer below, then trimmed), and remove that node
     from the document.
   - Otherwise leave the document untouched.

3. **Walk the body**: dispatch each remaining AST child block to its
   per-block-type parser via a registry mirroring
   `BlockToMarkdownRegistry`. The mapping:

   | Markdig AST node | Buildin block |
   |---|---|
   | `ParagraphBlock` | `ParagraphBlock` |
   | `HeadingBlock` (Level 1) | `Heading1Block` *(only when not consumed as title)* |
   | `HeadingBlock` (Level 2) | `Heading2Block` |
   | `HeadingBlock` (Level 3) | `Heading3Block` |
   | `HeadingBlock` (Level ≥ 4) | falls through to `ParagraphBlock` with leading `#…` text preserved (CommonMark allows H4–H6 but buildin's supported set stops at H3 per feature 002 FR-002; we treat the surplus as text so the round-trip stays unambiguous) |
   | `ListBlock` (BulletChar `-` or `*`) → `ListItemBlock` | `BulletedListItemBlock` (one block per item; children parsed recursively) |
   | `ListBlock` (ordered) → `ListItemBlock` | `NumberedListItemBlock` |
   | `ListItemBlock` whose first content is `- [ ]` / `- [x]` GFM task | `ToDoBlock` with `Checked` set |
   | `FencedCodeBlock` | `CodeBlock` (Language = `Info`) |
   | `QuoteBlock` | `QuoteBlock` |
   | `ThematicBreakBlock` | `DividerBlock` |
   | `HtmlBlock` | falls through to `ParagraphBlock` containing the raw HTML as plain text |
   | Anything else (unsupported in v1) | passed through as `ParagraphBlock` containing the source text |

4. **Walk inline runs** via `IInlineMarkdownParser`:
   - `LiteralInline` → plain `RichText` run.
   - `EmphasisInline` (single asterisk/underscore) → italic annotation.
   - `EmphasisInline` (double asterisk/underscore) → bold annotation.
   - `CodeInline` → inline code annotation.
   - `LinkInline`:
     - If `Url` starts with `buildin://`, recover as a buildin
       mention via `MentionLinkRecovery` (R3): emit a mention
       `RichText` whose target id is the URI's path segment. Default
       to page-mention kind; database-vs-page disambiguation is
       server-side.
     - Otherwise emit a link annotation with the URL preserved.
   - `LineBreakInline` (hard) → embedded `\n` inside the same
     `RichText` run.

5. **Unsupported-block placeholder pass-through** (spec FR-006): if a
   line matches the read side's placeholder shape
   (`<!-- unsupported block: <type> -->`), it survives CommonMark
   round-trip as raw inline HTML and is kept as a paragraph's text
   content — no `Block` of the named type is materialised. The
   in-doc presence of a placeholder is preserved exactly.

---

## Determinism

Given the same input string, `Parse` returns equal
`AuthoredDocument`s. The Markdig pipeline is configured once and
reused. No clock, no environment, no random state.

---

## Tests

Each row of the mapping table above has at least one unit test in
`tests/Buildout.UnitTests/Markdown/Authoring/Blocks/`. Inline behaviour
has tests in `Inline/`. The compatibility matrix (data-model.md) is
verified end-to-end by the round-trip suite in `RoundTrip/`.
