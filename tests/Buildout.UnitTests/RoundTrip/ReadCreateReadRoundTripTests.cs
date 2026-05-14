using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.RoundTrip;

public sealed class ReadCreateReadRoundTripTests
{
    private static PageMarkdownRenderer CreateRenderer(IBuildinClient client)
    {
        var blockConverters = new IBlockToMarkdownConverter[]
        {
            new ParagraphConverter(),
            new Heading1Converter(),
            new Heading2Converter(),
            new Heading3Converter(),
            new BulletedListItemConverter(),
            new NumberedListItemConverter(),
            new ToDoConverter(),
            new CodeConverter(),
            new QuoteConverter(),
            new DividerConverter()
        };
        var mentionConverters = new IMentionToMarkdownConverter[]
        {
            new PageMentionConverter(),
            new DatabaseMentionConverter(),
            new UserMentionConverter(),
            new DateMentionConverter()
        };
        var blockRegistry = new BlockToMarkdownRegistry(blockConverters);
        var mentionRegistry = new MentionToMarkdownRegistry(mentionConverters);
        var inlineRenderer = new InlineRenderer(mentionRegistry);
        return new PageMarkdownRenderer(client, blockRegistry, inlineRenderer);
    }

    private static MarkdownToBlocksParser CreateParser() => new();

    /// <summary>
    /// Renders <paramref name="block"/> using a fresh renderer against page <paramref name="pageId"/>.
    /// </summary>
    private static async Task<string> RenderBlockAsync(Block block, string pageId)
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(new Page { Id = pageId, Title = null });
        client.GetBlockChildrenAsync(pageId, Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [block], HasMore = false });
        return await CreateRenderer(client).RenderAsync(pageId);
    }

    // -------------------------------------------------------------------------
    // Lossless block types: markdown1 == markdown2
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Paragraph_IsLossless()
    {
        var block = new ParagraphBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "Hello world" }]
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task Heading3Block_IsStable_OutputIdenticalAfterRoundTrip()
    {
        // Heading3Block renders as "#### text\n\n".
        // "#### text" is H4 in markdown, which the parser falls through to ParagraphBlock
        // with Content = "#### My H3". That paragraph renders as "#### My H3\n\n" — same output.
        var block = new Heading3Block
        {
            RichTextContent = [new RichText { Type = "text", Content = "My H3" }]
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task BulletedListItem_IsLossless()
    {
        var block = new BulletedListItemBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "Item" }]
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task NumberedListItem_IsLossless()
    {
        var block = new NumberedListItemBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "Item" }]
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task ToDoBlock_Unchecked_IsLossless()
    {
        var block = new ToDoBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "Task" }],
            Checked = false
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task ToDoBlock_Checked_IsLossless()
    {
        var block = new ToDoBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "Task" }],
            Checked = true
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task CodeBlock_WithLanguage_IsLossless()
    {
        var block = new CodeBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "code here" }],
            Language = "csharp"
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task CodeBlock_WithoutLanguage_IsLossless()
    {
        var block = new CodeBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "code here" }],
            Language = null
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task QuoteBlock_IsLossless()
    {
        var block = new QuoteBlock
        {
            RichTextContent = [new RichText { Type = "text", Content = "Quoted text" }]
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task DividerBlock_IsLossless()
    {
        var block = new DividerBlock();
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    [Fact]
    public async Task UnsupportedBlock_PlaceholderIsStable()
    {
        // ImageBlock has no converter, so BlockToMarkdownRegistry falls through to
        // UnsupportedBlockHandler which emits "<!-- unsupported block: image -->".
        // The parser recovers that HTML comment as a ParagraphBlock whose Content
        // is the comment text. Rendering that paragraph produces the same string again.
        var block = new ImageBlock { Url = "https://example.com/image.png" };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.Equal(markdown1, markdown2);
    }

    // -------------------------------------------------------------------------
    // Lossy heading types: heading level shifts on each round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Heading1Block_ShiftsToHeading2_DocumentedLoss()
    {
        // Heading1Block renders as "## My H1\n\n" (two hashes).
        // Parsing "## My H1" produces Heading2Block.
        // Rendering Heading2Block gives "### My H1\n\n" (three hashes).
        // The shift is intentional: Buildin heading_1 is page-level, not body-level.
        var block = new Heading1Block
        {
            RichTextContent = [new RichText { Type = "text", Content = "My H1" }]
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.NotEqual(markdown1, markdown2);
        Assert.Contains("My H1", markdown1);
        Assert.Contains("My H1", markdown2);
        Assert.Contains("## ", markdown1);
        Assert.Contains("### ", markdown2);
    }

    [Fact]
    public async Task Heading2Block_ShiftsToHeading3_DocumentedLoss()
    {
        // Heading2Block renders as "### My H2\n\n" (three hashes).
        // Parsing "### My H2" produces Heading3Block.
        // Rendering Heading3Block gives "#### My H2\n\n" (four hashes).
        var block = new Heading2Block
        {
            RichTextContent = [new RichText { Type = "text", Content = "My H2" }]
        };
        var markdown1 = await RenderBlockAsync(block, "page-1");

        var parser = CreateParser();
        var parsed = parser.Parse(markdown1).Body[0].Block;

        var markdown2 = await RenderBlockAsync(parsed, "page-2");

        Assert.NotEqual(markdown1, markdown2);
        Assert.Contains("My H2", markdown1);
        Assert.Contains("My H2", markdown2);
        Assert.Contains("### ", markdown1);
        Assert.Contains("#### ", markdown2);
    }
}
