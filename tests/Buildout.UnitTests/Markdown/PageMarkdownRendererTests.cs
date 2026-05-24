using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown;
using Buildout.Core.Markdown.Conversion;
using Buildout.Core.Markdown.Conversion.Blocks;
using Buildout.Core.Markdown.Conversion.Mentions;
using Buildout.Core.Markdown.Internal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown;

public class PageMarkdownRendererTests
{
    private static PageMarkdownRenderer CreateRenderer(IPageContentProvider contentProvider)
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
        return new PageMarkdownRenderer(contentProvider, blockRegistry, inlineRenderer, Substitute.For<ILogger<PageMarkdownRenderer>>());
    }

    [Fact]
    public async Task RenderAsync_TitlePresent_PrependsH1()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = [new RichText { Type = "text", Content = "My Page" }] },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        var result = await sut.RenderAsync("page-1");

        Assert.StartsWith("# My Page" + Environment.NewLine + Environment.NewLine, result);
    }

    [Fact]
    public async Task RenderAsync_NullTitle_OmitsH1()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = null },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        var result = await sut.RenderAsync("page-1");

        Assert.DoesNotContain("# ", result);
    }

    [Fact]
    public async Task RenderAsync_EmptyTitle_OmitsH1()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = [] },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        var result = await sut.RenderAsync("page-1");

        Assert.DoesNotContain("# ", result);
    }

    [Fact]
    public async Task RenderAsync_HasMoreTrue_DrainsAllPages()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = null },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        var result = await sut.RenderAsync("page-1");

        Assert.Empty(result);
    }

    [Fact]
    public async Task RenderAsync_RecursiveConverter_FetchesChildren()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = null },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        var result = await sut.RenderAsync("page-1");

        Assert.Empty(result);
    }

    [Fact]
    public async Task RenderAsync_UnsupportedBlock_DoesNotRecurseChildren()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = null },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        var result = await sut.RenderAsync("page-1");

        Assert.Empty(result);
    }

    [Fact]
    public async Task RenderAsync_TwoCalls_ProduceIdenticalOutput()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = [new RichText { Type = "text", Content = "Title" }] },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        var result1 = await sut.RenderAsync("page-1");
        var result2 = await sut.RenderAsync("page-1");

        Assert.Equal(result1, result2);
    }

    [Fact]
    public async Task RenderAsync_CancellationToken_PropagatesToClient()
    {
        var contentProvider = Substitute.For<IPageContentProvider>();
        contentProvider.FetchAsync("page-1", Arg.Any<CancellationToken>())
            .Returns(new PageContent
            {
                Page = new Page { Id = "page-1", Title = null },
                Blocks = []
            });

        var sut = CreateRenderer(contentProvider);
        using var cts = new CancellationTokenSource();
        await sut.RenderAsync("page-1", cts.Token);

        await contentProvider.Received(1).FetchAsync("page-1", cts.Token);
    }
}