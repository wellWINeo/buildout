using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Buildout.UnitTests.PageTree;

public sealed class PageTreeServiceTests
{
    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private PageTreeService CreateSut() => new PageTreeService(_client, NullLogger<PageTreeService>.Instance);

    private static RichText MakeRichText(string content) => new() { Type = "text", Content = content };

    private static Page MakePage(string id, string title, string url = "https://example.com/page") =>
        new() { Id = id, Title = [MakeRichText(title)], Url = url };

    private static Page MakeUntitledPage(string id) =>
        new() { Id = id, Title = [], Url = "https://example.com/page" };

    private static Database MakeDatabase(string id, string title, string url = "https://example.com/db") =>
        new() { Id = id, Title = [MakeRichText(title)], Url = url };

    private static PaginatedList<Block> MakeBlocks(params Block[] blocks) =>
        new() { Results = blocks.ToList(), HasMore = false };

    private static QueryDatabaseResult MakeDbResult(params (string id, string title, string url)[] pages) =>
        new()
        {
            Results = [],
            Pages = pages.Select(p => new QueryDatabasePage { Id = p.id, Title = p.title, Url = p.url }).ToList(),
            HasMore = false
        };

    [Fact]
    public async Task BuildAsync_InvalidArgument_ThrowsForEmptyId()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.BuildAsync("", 3));
    }

    [Fact]
    public async Task BuildAsync_DepthZero_ThrowsBeforeNetworkCall()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<TreeDepthOutOfRangeException>(() => sut.BuildAsync("root-id", 0));
        await _client.DidNotReceiveWithAnyArgs().GetPageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_DepthEight_ThrowsBeforeNetworkCall()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<TreeDepthOutOfRangeException>(() => sut.BuildAsync("root-id", 8));
        await _client.DidNotReceiveWithAnyArgs().GetPageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_RootNotFound_ThrowsTreeRootNotFoundException()
    {
        _client.GetPageAsync("root-id", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(404, "not_found", "Not found", null)));
        _client.GetDatabaseAsync("root-id", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(404, "not_found", "Not found", null)));

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<TreeRootNotFoundException>(() => sut.BuildAsync("root-id", 3));
        Assert.Contains("root-id", ex.Message);
    }

    [Fact]
    public async Task BuildAsync_Depth1_ReturnsRootPlusDirectChildrenOnly()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root", "https://x.com/root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "child1", Title = "Child 1" }
        ));
        _client.GetPageAsync("child1", Arg.Any<CancellationToken>()).Returns(MakePage("child1", "Child 1", "https://x.com/c1"));
        _client.GetBlockChildrenAsync("child1", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "grandchild1", Title = "GC" }
        ));

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 1);

        Assert.Equal("Root", root.Name);
        Assert.Single(root.Children);
        Assert.Equal("Child 1", root.Children[0].Name);
        Assert.Empty(root.Children[0].Children);
    }

    [Fact]
    public async Task BuildAsync_Depth3_TraversesThreeLevels()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "level1", Title = "L1" }
        ));
        _client.GetPageAsync("level1", Arg.Any<CancellationToken>()).Returns(MakePage("level1", "Level1"));
        _client.GetBlockChildrenAsync("level1", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "level2", Title = "L2" }
        ));
        _client.GetPageAsync("level2", Arg.Any<CancellationToken>()).Returns(MakePage("level2", "Level2"));
        _client.GetBlockChildrenAsync("level2", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "level3", Title = "L3" }
        ));
        _client.GetPageAsync("level3", Arg.Any<CancellationToken>()).Returns(MakePage("level3", "Level3"));

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 3);

        Assert.Single(root.Children);
        Assert.Single(root.Children[0].Children);
        Assert.Single(root.Children[0].Children[0].Children);
        Assert.Empty(root.Children[0].Children[0].Children[0].Children);
    }

    [Fact]
    public async Task BuildAsync_Depth7_TraversesSevenLevels()
    {
        var ids = Enumerable.Range(0, 8).Select(i => $"level{i}").ToArray();
        for (var i = 0; i < 7; i++)
        {
            var id = ids[i];
            var nextId = ids[i + 1];
            _client.GetPageAsync(id, Arg.Any<CancellationToken>()).Returns(MakePage(id, id));
            _client.GetBlockChildrenAsync(id, null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
                new ChildPageBlock { Id = nextId, Title = nextId }
            ));
        }
        _client.GetPageAsync(ids[7], Arg.Any<CancellationToken>()).Returns(MakePage(ids[7], ids[7]));

        var sut = CreateSut();
        var root = await sut.BuildAsync(ids[0], 7);

        var current = root;
        for (var i = 0; i < 7; i++)
        {
            Assert.Single(current.Children);
            current = current.Children[0];
        }
        Assert.Empty(current.Children);
    }

    [Fact]
    public async Task BuildAsync_MixedPageAndDatabaseChildren_IncludesBoth()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "page-child", Title = "Page Child" },
            new ChildDatabaseBlock { Id = "db-child", Title = "DB Child" }
        ));
        _client.GetPageAsync("page-child", Arg.Any<CancellationToken>()).Returns(MakePage("page-child", "Page Child"));
        _client.GetBlockChildrenAsync("page-child", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());
        _client.GetDatabaseAsync("db-child", Arg.Any<CancellationToken>()).Returns(MakeDatabase("db-child", "DB Child"));
        _client.QueryDatabaseAsync("db-child", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeDbResult());

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 3);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Page Child", root.Children[0].Name);
        Assert.Equal("DB Child", root.Children[1].Name);
    }

    [Fact]
    public async Task BuildAsync_DatabaseRoot_UsesDatabaseTitleAndUrl()
    {
        _client.GetPageAsync("db-root", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(404, "not_found", "Not found", null)));
        _client.GetDatabaseAsync("db-root", Arg.Any<CancellationToken>())
            .Returns(MakeDatabase("db-root", "My Database", "https://x.com/mydb"));
        _client.QueryDatabaseAsync("db-root", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeDbResult());

        var sut = CreateSut();
        var root = await sut.BuildAsync("db-root", 3);

        Assert.Equal("My Database", root.Name);
        Assert.Equal("https://x.com/mydb", root.Uri);
        Assert.Empty(root.Children);
    }

    [Fact]
    public async Task BuildAsync_DatabaseRoot_ListsRecordsAsChildren()
    {
        _client.GetPageAsync("db-root", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(404, "not_found", "Not found", null)));
        _client.GetDatabaseAsync("db-root", Arg.Any<CancellationToken>())
            .Returns(MakeDatabase("db-root", "DB"));
        _client.QueryDatabaseAsync("db-root", Arg.Any<QueryDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeDbResult(("rec1", "Record One", "https://x.com/rec1"), ("rec2", "Record Two", "https://x.com/rec2")));
        _client.GetBlockChildrenAsync("rec1", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());
        _client.GetBlockChildrenAsync("rec2", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("db-root", 3);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Record One", root.Children[0].Name);
        Assert.Equal("Record Two", root.Children[1].Name);
    }

    [Fact]
    public async Task BuildAsync_EmptyTitle_UsesUntitledPlaceholder()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakeUntitledPage("root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 1);

        Assert.Equal("(untitled)", root.Name);
    }

    [Fact]
    public async Task BuildAsync_WhitespaceOnlyTitle_UsesUntitledPlaceholder()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>())
            .Returns(new Page { Id = "root", Title = [new RichText { Type = "text", Content = "   " }], Url = "https://x.com" });
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 1);

        Assert.Equal("(untitled)", root.Name);
    }

    [Fact]
    public async Task BuildAsync_DescendantReadFailure_RendersAsUnavailableAndContinues()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "good-child" },
            new ChildPageBlock { Id = "bad-child" }
        ));
        _client.GetPageAsync("good-child", Arg.Any<CancellationToken>())
            .Returns(MakePage("good-child", "Good"));
        _client.GetBlockChildrenAsync("good-child", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());
        _client.GetPageAsync("bad-child", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(500, "internal", "Internal error", null)));

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 3);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Good", root.Children[0].Name);
        Assert.Equal("(unavailable)", root.Children[1].Name);
        Assert.Empty(root.Children[1].Children);
    }

    [Fact]
    public async Task BuildAsync_CycleDetected_ThrowsTreeCycleDetectedException()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "root", Title = "Cycle" }
        ));

        var sut = CreateSut();
        await Assert.ThrowsAsync<TreeCycleDetectedException>(() => sut.BuildAsync("root", 3));
    }

    [Fact]
    public async Task BuildAsync_SiblingOrderMatchesApiOrder()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ChildPageBlock { Id = "first", Title = "First" },
            new ChildPageBlock { Id = "second", Title = "Second" },
            new ChildPageBlock { Id = "third", Title = "Third" }
        ));
        _client.GetPageAsync("first", Arg.Any<CancellationToken>()).Returns(MakePage("first", "First"));
        _client.GetPageAsync("second", Arg.Any<CancellationToken>()).Returns(MakePage("second", "Second"));
        _client.GetPageAsync("third", Arg.Any<CancellationToken>()).Returns(MakePage("third", "Third"));
        _client.GetBlockChildrenAsync("first", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());
        _client.GetBlockChildrenAsync("second", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());
        _client.GetBlockChildrenAsync("third", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 3);

        Assert.Equal(3, root.Children.Count);
        Assert.Equal("First", root.Children[0].Name);
        Assert.Equal("Second", root.Children[1].Name);
        Assert.Equal("Third", root.Children[2].Name);
    }

    [Fact]
    public async Task BuildAsync_NonChildBlockTypes_AreExcluded()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
            new ParagraphBlock { Id = "para" },
            new Heading1Block { Id = "h1" },
            new BulletedListItemBlock { Id = "bullet" },
            new ChildPageBlock { Id = "included-child", Title = "Included" }
        ));
        _client.GetPageAsync("included-child", Arg.Any<CancellationToken>())
            .Returns(MakePage("included-child", "Included"));
        _client.GetBlockChildrenAsync("included-child", null, Arg.Any<CancellationToken>())
            .Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 3);

        Assert.Single(root.Children);
        Assert.Equal("Included", root.Children[0].Name);
    }

    [Fact]
    public async Task BuildAsync_RootAuthFailure_PropagatesException()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(401, "unauthorized", "Unauthorized", null)));

        var sut = CreateSut();
        await Assert.ThrowsAsync<BuildinApiException>(() => sut.BuildAsync("root", 3));
    }

    [Fact]
    public async Task BuildAsync_ChildrenIsNeverNull()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 3);

        Assert.NotNull(root.Children);
    }

    [Fact]
    public async Task BuildAsync_PaginatedPageChildren_CollectsAllPages()
    {
        _client.GetPageAsync("root", Arg.Any<CancellationToken>()).Returns(MakePage("root", "Root"));

        var firstPage = new PaginatedList<Block>
        {
            Results = new List<Block> { new ChildPageBlock { Id = "child1", Title = "Child 1" } },
            HasMore = true,
            NextCursor = "cursor-abc"
        };
        _client.GetBlockChildrenAsync("root", null, Arg.Any<CancellationToken>()).Returns(firstPage);

        var secondPage = new PaginatedList<Block>
        {
            Results = new List<Block> { new ChildPageBlock { Id = "child2", Title = "Child 2" } },
            HasMore = false
        };
        _client.GetBlockChildrenAsync("root",
            Arg.Is<BlockChildrenQuery?>(q => q != null && q.StartCursor == "cursor-abc"),
            Arg.Any<CancellationToken>()).Returns(secondPage);

        _client.GetPageAsync("child1", Arg.Any<CancellationToken>()).Returns(MakePage("child1", "Child 1"));
        _client.GetPageAsync("child2", Arg.Any<CancellationToken>()).Returns(MakePage("child2", "Child 2"));
        _client.GetBlockChildrenAsync("child1", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());
        _client.GetBlockChildrenAsync("child2", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("root", 1);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Child 1", root.Children[0].Name);
        Assert.Equal("Child 2", root.Children[1].Name);
    }

    [Fact]
    public async Task BuildAsync_PaginatedDatabaseChildren_CollectsAllPages()
    {
        _client.GetPageAsync("db-root", Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(404, "not_found", "Not found", null)));
        _client.GetDatabaseAsync("db-root", Arg.Any<CancellationToken>())
            .Returns(MakeDatabase("db-root", "DB Root"));

        var firstResult = new QueryDatabaseResult
        {
            Results = [],
            Pages = new List<QueryDatabasePage> { new() { Id = "rec1", Title = "Record 1", Url = "https://x.com/rec1" } },
            HasMore = true,
            NextCursor = "cursor-xyz"
        };
        _client.QueryDatabaseAsync("db-root",
            Arg.Is<QueryDatabaseRequest>(r => r.StartCursor == null),
            Arg.Any<CancellationToken>()).Returns(firstResult);

        var secondResult = new QueryDatabaseResult
        {
            Results = [],
            Pages = new List<QueryDatabasePage> { new() { Id = "rec2", Title = "Record 2", Url = "https://x.com/rec2" } },
            HasMore = false
        };
        _client.QueryDatabaseAsync("db-root",
            Arg.Is<QueryDatabaseRequest>(r => r.StartCursor == "cursor-xyz"),
            Arg.Any<CancellationToken>()).Returns(secondResult);

        _client.GetBlockChildrenAsync("rec1", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());
        _client.GetBlockChildrenAsync("rec2", null, Arg.Any<CancellationToken>()).Returns(MakeBlocks());

        var sut = CreateSut();
        var root = await sut.BuildAsync("db-root", 3);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Record 1", root.Children[0].Name);
        Assert.Equal("Record 2", root.Children[1].Name);
    }

    [Fact]
    public async Task BuildAsync_Depth3OnFiveLevelHierarchy_ReturnsExactlyThreeLevels()
    {
        var ids = new[] { "r", "l1", "l2", "l3", "l4" };
        for (var i = 0; i < 4; i++)
        {
            var id = ids[i];
            var nextId = ids[i + 1];
            _client.GetPageAsync(id, Arg.Any<CancellationToken>()).Returns(MakePage(id, id));
            _client.GetBlockChildrenAsync(id, null, Arg.Any<CancellationToken>()).Returns(MakeBlocks(
                new ChildPageBlock { Id = nextId }
            ));
        }
        _client.GetPageAsync(ids[4], Arg.Any<CancellationToken>()).Returns(MakePage(ids[4], ids[4]));

        var sut = CreateSut();
        var root = await sut.BuildAsync(ids[0], 3);

        Assert.Single(root.Children);
        Assert.Single(root.Children[0].Children);
        Assert.Single(root.Children[0].Children[0].Children);
        Assert.Empty(root.Children[0].Children[0].Children[0].Children);
    }
}
