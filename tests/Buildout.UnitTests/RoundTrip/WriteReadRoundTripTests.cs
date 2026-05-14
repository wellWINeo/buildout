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

public class WriteReadRoundTripTests
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

    private static IBuildinClient MakeClient(IReadOnlyList<Block> blocks, IReadOnlyList<RichText>? title = null)
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-id", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-id", Title = title });
        client.GetBlockChildrenAsync("page-id", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = blocks, HasMore = false });
        return client;
    }

    [Fact]
    public async Task Paragraph_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("Hello world");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("Hello world" + Environment.NewLine + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task Heading2_ShiftsToHeading3OnRead()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("## Section");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        // Heading2Block renders as ### (one-level shift)
        Assert.Contains("### Section", rendered);
        // The original source was ##; it must not survive unchanged — the rendered form is ###
        // (DoesNotContain "## Section\r\n" or "## Section\n" — but ### starts with ##, so we
        // verify the rendered prefix is exactly ### by checking it does not start with "## ")
        Assert.DoesNotContain(Environment.NewLine + "## Section" + Environment.NewLine, rendered);
        Assert.StartsWith("### Section", rendered.TrimStart());
    }

    [Fact]
    public async Task Heading3_ShiftsToHeading4OnRead()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("### Subsection");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        // Heading3Block renders as #### (one-level shift)
        Assert.Contains("#### Subsection", rendered);
    }

    [Fact]
    public async Task LeadingH1_ExtractedAsTitle_RenderedWithH1Prefix()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("# My Page\n\nSome content");

        // Title is extracted by the parser
        Assert.Equal("My Page", doc.Title);

        var bodyBlocks = doc.Body.Select(s => s.Block).ToList();
        var title = new RichText[] { new() { Type = "text", Content = doc.Title! } };

        var client = MakeClient(bodyBlocks, title);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.StartsWith("# My Page", rendered);
        Assert.Contains("Some content", rendered);
    }

    [Fact]
    public async Task NonLeadingH1_BecomesHeading1Block_RendersAsH2()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("Some paragraph\n\n# Not a title");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        // ParagraphBlock renders verbatim
        Assert.Contains("Some paragraph", rendered);
        // Heading1Block renders as ## (Heading1Converter uses "## ")
        Assert.Contains("## Not a title", rendered);
    }

    [Fact]
    public async Task BulletedListItem_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("- Item text");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("- Item text" + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task NumberedListItem_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("1. First item");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("1. First item" + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task ToDoUnchecked_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("- [ ] Task");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("- [ ] Task" + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task ToDoChecked_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("- [x] Done");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("- [x] Done" + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task CodeBlockWithLanguage_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("```csharp\nvar x = 1;\n```");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        var expected = "```csharp" + Environment.NewLine
                     + "var x = 1;" + Environment.NewLine
                     + "```" + Environment.NewLine
                     + Environment.NewLine;
        Assert.Equal(expected, rendered);
    }

    [Fact]
    public async Task Quote_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("> Quoted text");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("> Quoted text" + Environment.NewLine + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task Divider_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("---");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("---" + Environment.NewLine + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task UnsupportedBlockPlaceholder_PassesThroughStably()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("<!-- unsupported block: image -->");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Contains("<!-- unsupported block: image -->", rendered);
    }

    [Fact]
    public async Task BoldInline_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("**bold text**");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("**bold text**" + Environment.NewLine + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task ItalicInline_IsLossless()
    {
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("*italic text*");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Equal("*italic text*" + Environment.NewLine + Environment.NewLine, rendered);
    }

    [Fact]
    public async Task PageMentionLink_RecoveredAsBuildinMention()
    {
        // "[My Page](buildin://page-abc)" is parsed as a PageMention RichText
        // and renders back as the same "[My Page](buildin://page-abc)" link.
        var parser = new MarkdownToBlocksParser();
        var doc = parser.Parse("[My Page](buildin://page-abc)");
        var blocks = doc.Body.Select(s => s.Block).ToList();

        var client = MakeClient(blocks);
        var renderer = CreateRenderer(client);
        var rendered = await renderer.RenderAsync("page-id");

        Assert.Contains("[My Page](buildin://page-abc)", rendered);
    }
}
