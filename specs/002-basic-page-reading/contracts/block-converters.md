# Contract — Block & Mention Converter Subsystem

The OCP-shaped dispatch layer behind `IPageMarkdownRenderer`. Each supported
block type and each supported mention sub-type lives in its own file
implementing a small, focused interface. Adding a new type means adding one
new file plus one DI registration line. No existing class is modified.

## Goals

1. **Open / closed**: extending support to a new block or mention type does
   not require editing any existing converter, the registry, or the
   orchestrator.
2. **Single-responsibility per file**: each converter handles exactly one
   block / mention type. Tests live next to it, one file per converter.
3. **Symmetric for the future write direction**: when the writing feature
   lands, it adds a paired `IMarkdownToBlockConverter` family. The
   `-ToMarkdown` suffix on this feature's interfaces signals the direction
   so the future siblings drop in cleanly without renaming anything that
   already shipped.
4. **No conditional dispatch in the hot path**: registries are pure
   lookups. There is no `switch` or `if/else if` chain over block / mention
   types anywhere in the rendering pipeline.

## Block converters

### Interface

Located at `src/Buildout.Core/Markdown/Conversion/IBlockToMarkdownConverter.cs`.

```text
namespace Buildout.Core.Markdown.Conversion;

public interface IBlockToMarkdownConverter
{
    /// The CLR type this converter handles. Used as the registry key.
    Type BlockClrType { get; }

    /// The buildin discriminator string ("paragraph", "heading_1", …).
    /// Pairs this converter with a future IMarkdownToBlockConverter of the
    /// same name. Also used for the unsupported-block placeholder if no
    /// converter is registered for a given type.
    string BlockType { get; }

    /// Whether the orchestrator should fetch & recurse this block's
    /// HasChildren = true subtree before invoking Write. List items, quotes,
    /// to-dos return true; leaf blocks (paragraph, heading, code, divider)
    /// return false.
    bool RecurseChildren { get; }

    /// Emit the Markdown form of `block` (and its already-fetched
    /// `children`, if RecurseChildren was true) into the context's writer.
    void Write(Block block,
               IReadOnlyList<BlockSubtree> children,
               IMarkdownRenderContext ctx);
}
```

### Render context

Located at `src/Buildout.Core/Markdown/Conversion/IMarkdownRenderContext.cs`.

```text
public interface IMarkdownRenderContext
{
    IMarkdownWriter Writer { get; }
    IInlineRenderer Inline { get; }
    int IndentLevel { get; }                  // 0 for top-level blocks
    IMarkdownRenderContext WithIndent(int delta);
    void WriteBlockSubtree(BlockSubtree subtree); // dispatches via the registry
}
```

The orchestrator constructs the root context. Converters call
`ctx.WriteBlockSubtree(child)` to recurse — the orchestrator/registry handle
the dispatch, so converters never know about other converters' types.

### Registry

Located at `src/Buildout.Core/Markdown/Conversion/BlockToMarkdownRegistry.cs`.

```text
public sealed class BlockToMarkdownRegistry
{
    private readonly IReadOnlyDictionary<Type, IBlockToMarkdownConverter> _byClrType;

    public BlockToMarkdownRegistry(IEnumerable<IBlockToMarkdownConverter> converters)
    {
        // throws InvalidOperationException on duplicate keys
        _byClrType = converters.ToDictionary(c => c.BlockClrType);
    }

    public IBlockToMarkdownConverter? Resolve(Block block)
        => _byClrType.GetValueOrDefault(block.GetType());
}
```

The registry is built once at process start by DI. `IEnumerable<IBlockToMarkdownConverter>`
is satisfied by the converter implementations registered in
`ServiceCollectionExtensions.AddBuildoutCore(...)`.

### Concrete converters (this feature)

One file each under `src/Buildout.Core/Markdown/Conversion/Blocks/`. Each is
a sealed class implementing `IBlockToMarkdownConverter`. Sketch:

```text
internal sealed class ParagraphConverter : IBlockToMarkdownConverter
{
    public Type BlockClrType => typeof(ParagraphBlock);
    public string BlockType => "paragraph";
    public bool RecurseChildren => false;

    public void Write(Block block, IReadOnlyList<BlockSubtree> children, IMarkdownRenderContext ctx)
    {
        var p = (ParagraphBlock)block;
        ctx.Writer.WriteLine(ctx.Inline.Render(p.RichTextContent, ctx.IndentLevel));
        ctx.Writer.WriteBlankLine();
    }
}
```

| File | `BlockType` | `RecurseChildren` | Notes |
|---|---|---|---|
| `ParagraphConverter.cs` | `paragraph` | false | inline content + trailing blank line |
| `Heading1Converter.cs` | `heading_1` | false | `## ` + inline (heading shift per data-model) |
| `Heading2Converter.cs` | `heading_2` | false | `### ` + inline |
| `Heading3Converter.cs` | `heading_3` | false | `#### ` + inline |
| `BulletedListItemConverter.cs` | `bulleted_list_item` | true | `- ` + inline; nested items rendered indented |
| `NumberedListItemConverter.cs` | `numbered_list_item` | true | `1. ` + inline (CommonMark renumbers) |
| `ToDoConverter.cs` | `to_do` | true | `- [ ] ` / `- [x] ` + inline |
| `CodeConverter.cs` | `code` | false | ` ```<lang>\n…\n``` ` |
| `QuoteConverter.cs` | `quote` | true | line-prefix `> `; child blocks also quoted |
| `DividerConverter.cs` | `divider` | false | `---` thematic break |

Block types not in the table above have **no** registered converter. The
orchestrator's fallback path (see § Fallback below) emits the placeholder.

### DI registration

`ServiceCollectionExtensions.AddBuildoutCore(...)` adds:

```text
services.AddSingleton<IBlockToMarkdownConverter, ParagraphConverter>();
services.AddSingleton<IBlockToMarkdownConverter, Heading1Converter>();
// … one line per converter …
services.AddSingleton<BlockToMarkdownRegistry>();
```

Adding `image` support later means appending one line to this list and one
new file. Nothing else changes.

### Fallback for unsupported blocks

Located at
`src/Buildout.Core/Markdown/Conversion/UnsupportedBlockHandler.cs`. Single
public method:

```text
public static class UnsupportedBlockHandler
{
    public static void Write(Block block, IMarkdownRenderContext ctx)
    {
        ctx.Writer.WriteLine($"<!-- unsupported block: {block.Type} -->");
        ctx.Writer.WriteBlankLine();
    }
}
```

The orchestrator calls this whenever `BlockToMarkdownRegistry.Resolve(block)`
returns `null`. The placeholder uses `Block.Type` (the buildin discriminator
string), which is always populated.

## Mention converters

Same shape for inline mentions, scoped to the `Mention` discriminated union.

### Interface

Located at `src/Buildout.Core/Markdown/Conversion/IMentionToMarkdownConverter.cs`.

```text
public interface IMentionToMarkdownConverter
{
    Type MentionClrType { get; }      // typeof(PageMention), typeof(UserMention), …
    string MentionType { get; }       // "page", "database", "user", "date", …

    /// Emit the inline Markdown form. `displayText` is RichText.Content
    /// (buildin's pre-computed display text) for use as a fallback or as
    /// the link label.
    string Render(Mention mention, string displayText);
}
```

### Registry

`MentionToMarkdownRegistry` mirrors `BlockToMarkdownRegistry` — built from
DI, keyed by `Mention` CLR type, returns `null` for unknown sub-types.

### Concrete converters (this feature)

| File | `MentionType` | Output |
|---|---|---|
| `Conversion/Mentions/PageMentionConverter.cs` | `page` | `[<displayText>](buildin://<page_id>)` |
| `Conversion/Mentions/DatabaseMentionConverter.cs` | `database` | `[<displayText>](buildin://<database_id>)` |
| `Conversion/Mentions/UserMentionConverter.cs` | `user` | `@<displayText>` |
| `Conversion/Mentions/DateMentionConverter.cs` | `date` | `<start>` or `<start> – <end>` |

### Fallback

`InlineRenderer` calls `MentionToMarkdownRegistry.Resolve(mention)`. On
`null`, it emits the rich-text item's `Content` verbatim (per spec FR-005b
§ "Any other mention type"). No placeholder comment is emitted inline because
that would visually corrupt prose.

## Future-direction note (markdown → blocks)

When the writing feature lands, it will introduce a parallel family:

```text
public interface IMarkdownToBlockConverter
{
    string BlockType { get; }                // pairs with the -ToMarkdown sibling
    bool CanConsume(MarkdownAstNode node);
    Block Build(MarkdownAstNode node, IMarkdownToBlockContext ctx);
}
```

The pairing is by `BlockType` string. A round-trip test fixture will
instantiate both registries, render a block to Markdown via this feature's
converter, then parse the Markdown back via the future converter, and
compare. The interfaces deliberately do **not** share a base type — each
direction has its own dispatch concerns — but the `-ToMarkdown` /
`-ToBlock` naming makes the symmetry visible.

This document should be updated when the writing feature lands; for now the
forward-looking design is captured here as guardrails so today's
implementation does not paint future work into a corner.

## Test obligations (per-converter)

Each block converter has a dedicated test class at
`tests/Buildout.UnitTests/Markdown/Blocks/<Type>ConverterTests.cs`. Required
tests per converter:

| Test name | Purpose |
|---|---|
| `WritesExpectedMarkdownForCanonicalBlock` | A representative input block produces the Markdown form recorded in `data-model.md`'s compatibility matrix. |
| `RecursesIntoChildrenWhenSupported` | (Only converters with `RecurseChildren = true`) given a block with two children, both children appear in the output indented per the converter's nesting rule. |
| `HonoursIndentLevelFromContext` | Output is correctly indented when invoked at `IndentLevel > 0`. |
| `HandlesEmptyRichTextContent` | A block with null/empty `RichTextContent` produces a sensible degenerate form (e.g. an empty paragraph emits a single blank line). |

Each mention converter has a dedicated test class at
`tests/Buildout.UnitTests/Markdown/Mentions/<Type>MentionConverterTests.cs`.
Required tests per converter:

| Test name | Purpose |
|---|---|
| `WritesExpectedFormForCanonicalMention` | Per the matrix in `data-model.md`. |
| `FallsBackToDisplayTextWhenSubFieldsMissing` | When the mention's expected fields are null (e.g. `DateMention.Start == null`), emit `displayText` verbatim. |

Registry tests (`BlockToMarkdownRegistryTests`,
`MentionToMarkdownRegistryTests`) verify:

- Lookup by CLR type succeeds for every registered converter.
- Lookup returns `null` for unregistered types.
- Construction throws `InvalidOperationException` if two converters register
  the same `BlockClrType` / `MentionClrType` (catches accidental double-
  registration at startup, not at first dispatch).
