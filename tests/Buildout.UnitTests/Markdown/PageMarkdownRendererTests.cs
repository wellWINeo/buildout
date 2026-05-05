using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

public class PageMarkdownRendererTests
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

    [Fact]
    public async Task RenderAsync_TitlePresent_PrependsH1()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = [new RichText { Type = "text", Content = "My Page" }] });
        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [], HasMore = false });

        var sut = CreateRenderer(client);
        var result = await sut.RenderAsync("page-1");

        Assert.StartsWith("# My Page" + Environment.NewLine + Environment.NewLine, result);
    }

    [Fact]
    public async Task RenderAsync_NullTitle_OmitsH1()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = null });
        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [], HasMore = false });

        var sut = CreateRenderer(client);
        var result = await sut.RenderAsync("page-1");

        Assert.DoesNotContain("# ", result);
    }

    [Fact]
    public async Task RenderAsync_EmptyTitle_OmitsH1()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = [] });
        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [], HasMore = false });

        var sut = CreateRenderer(client);
        var result = await sut.RenderAsync("page-1");

        Assert.DoesNotContain("# ", result);
    }

    [Fact]
    public async Task RenderAsync_HasMoreTrue_DrainsAllPages()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = null });

        var block1 = new ParagraphBlock
        {
            Id = "b1",
            RichTextContent = [new RichText { Type = "text", Content = "First" }]
        };
        var block2 = new ParagraphBlock
        {
            Id = "b2",
            RichTextContent = [new RichText { Type = "text", Content = "Second" }]
        };

        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(
                new PaginatedList<Block> { Results = [block1], HasMore = true, NextCursor = "cursor1" },
                new PaginatedList<Block> { Results = [block2], HasMore = false }
            );

        var sut = CreateRenderer(client);
        var result = await sut.RenderAsync("page-1");

        Assert.Contains("First", result);
        Assert.Contains("Second", result);
    }

    [Fact]
    public async Task RenderAsync_RecursiveConverter_FetchesChildren()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = null });

        var parent = new BulletedListItemBlock
        {
            Id = "parent-1",
            HasChildren = true,
            RichTextContent = [new RichText { Type = "text", Content = "Parent item" }]
        };
        var child = new ParagraphBlock
        {
            Id = "child-1",
            RichTextContent = [new RichText { Type = "text", Content = "Child paragraph" }]
        };

        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [parent], HasMore = false });
        client.GetBlockChildrenAsync("parent-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [child], HasMore = false });

        var sut = CreateRenderer(client);
        var result = await sut.RenderAsync("page-1");

        Assert.Contains("- Parent item", result);
        Assert.Contains("Child paragraph", result);
    }

    [Fact]
    public async Task RenderAsync_UnsupportedBlock_DoesNotRecurseChildren()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = null });

        var image = new ImageBlock
        {
            Id = "img-1",
            HasChildren = true,
            Url = "https://example.com/image.png"
        };

        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [image], HasMore = false });

        var sut = CreateRenderer(client);
        var result = await sut.RenderAsync("page-1");

        Assert.Contains("<!-- unsupported block: image -->", result);
        _ = client.DidNotReceive()
            .GetBlockChildrenAsync("img-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAsync_TwoCalls_ProduceIdenticalOutput()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = [new RichText { Type = "text", Content = "Title" }] });

        var block = new ParagraphBlock
        {
            Id = "b1",
            RichTextContent = [new RichText { Type = "text", Content = "Hello" }]
        };
        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [block], HasMore = false });

        var sut = CreateRenderer(client);
        var result1 = await sut.RenderAsync("page-1");
        var result2 = await sut.RenderAsync("page-1");

        Assert.Equal(result1, result2);
    }

    [Fact]
    public async Task RenderAsync_CancellationToken_PropagatesToClient()
    {
        var client = Substitute.For<IBuildinClient>();
        client.GetPageAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "page-1", Title = null });
        client.GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Block> { Results = [], HasMore = false });

        var sut = CreateRenderer(client);
        using var cts = new CancellationTokenSource();
        await sut.RenderAsync("page-1", cts.Token);

        await client.Received(1).GetPageAsync("page-1", cts.Token);
        await client.Received(1)
            .GetBlockChildrenAsync("page-1", Arg.Any<BlockChildrenQuery?>(), cts.Token);
    }
}
