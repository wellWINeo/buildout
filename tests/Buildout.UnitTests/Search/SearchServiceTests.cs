using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Search;
using Buildout.Core.Search.Internal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Search;

public sealed class SearchServiceTests
{
    private readonly IBuildinClient _client;
    private readonly ITitleRenderer _titleRenderer;
    private readonly AncestorScopeFilter _scopeFilter;
    private readonly ILogger<SearchService> _logger;
    private readonly SearchService _service;

    public SearchServiceTests()
    {
        _client = Substitute.For<IBuildinClient>();
        _titleRenderer = Substitute.For<ITitleRenderer>();
        _scopeFilter = new AncestorScopeFilter(_client, Substitute.For<ILogger<AncestorScopeFilter>>());
        _logger = Substitute.For<ILogger<SearchService>>();
        _service = new SearchService(_client, _titleRenderer, _scopeFilter, _logger);
    }

    private static Page MakePage(
        string id,
        string? objectType = "page",
        bool archived = false,
        Parent? parent = null,
        IReadOnlyList<RichText>? title = null) => new()
    {
        Id = id,
        ObjectType = objectType,
        Archived = archived,
        Parent = parent,
        Title = title
    };

    private void SetupTitleRenderer(string output = "Title") =>
        _titleRenderer.RenderPlain(Arg.Any<IReadOnlyList<RichText>>()).Returns(output);

    [Fact]
    public async Task EmptyQuery_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SearchAsync("", null, CancellationToken.None));

        Assert.Equal("query", ex.ParamName);
        await _client.DidNotReceive()
            .SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhitespaceQuery_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SearchAsync("   ", null, CancellationToken.None));

        Assert.Equal("query", ex.ParamName);
    }

    [Fact]
    public async Task SinglePageResponse_ReturnsMatches()
    {
        var title = new List<RichText> { new() { Type = "text", Content = "My Page" } };
        var page = MakePage("p1", parent: new ParentWorkspace(null), title: title);

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [page] }));

        _titleRenderer.RenderPlain(title).Returns("My Page");

        var results = await _service.SearchAsync("test", null, CancellationToken.None);

        var match = Assert.Single(results);
        Assert.Equal("p1", match.PageId);
        Assert.Equal(SearchObjectType.Page, match.ObjectType);
        Assert.Equal("My Page", match.DisplayTitle);
    }

    [Fact]
    public async Task DatabaseObjectType_MappedCorrectly()
    {
        var dbPage = MakePage("db1", objectType: "database");
        var nullTypePage = MakePage("p1", objectType: null);

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [dbPage, nullTypePage] }));

        SetupTitleRenderer();

        var results = await _service.SearchAsync("test", null, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal(SearchObjectType.Database, results[0].ObjectType);
        Assert.Equal(SearchObjectType.Page, results[1].ObjectType);
    }

    [Fact]
    public async Task MultiPagePagination_DrainsAllPages()
    {
        var page1 = MakePage("p1");
        var page2 = MakePage("p2");
        var page3 = MakePage("p3");

        _client.SearchPagesAsync(Arg.Is<PageSearchRequest>(r => r.StartCursor == null), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults
            {
                Results = [page1],
                HasMore = true,
                NextCursor = "cursor1"
            }));

        _client.SearchPagesAsync(Arg.Is<PageSearchRequest>(r => r.StartCursor == "cursor1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [page2, page3] }));

        SetupTitleRenderer();

        var results = await _service.SearchAsync("test", null, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(["p1", "p2", "p3"], results.Select(m => m.PageId).ToList());
    }

    [Fact]
    public async Task ArchivedPages_ExcludedFromOutput()
    {
        var active = MakePage("p1", archived: false);
        var archived = MakePage("p2", archived: true);

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [active, archived] }));

        SetupTitleRenderer();

        var results = await _service.SearchAsync("test", null, CancellationToken.None);

        var match = Assert.Single(results);
        Assert.Equal("p1", match.PageId);
    }

    [Fact]
    public async Task NoPageId_NoFilterApplied()
    {
        var pages = new[] { MakePage("p1"), MakePage("p2"), MakePage("p3") };

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = pages }));

        SetupTitleRenderer();

        var results = await _service.SearchAsync("test", null, CancellationToken.None);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task PageIdProvided_FiltersToDescendants()
    {
        var scopeId = "scope-1";
        var pageA = MakePage(scopeId, parent: new ParentWorkspace(null));
        var pageB = MakePage("child-b", parent: new ParentPage(scopeId));
        var pageC = MakePage("child-c", parent: new ParentPage("child-b"));
        var pageD = MakePage("unrelated-d", parent: new ParentWorkspace(null));

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [pageA, pageB, pageC, pageD] }));

        SetupTitleRenderer();

        var results = await _service.SearchAsync("test", scopeId, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { "child-b", "child-c", scopeId }, results.Select(m => m.PageId).Order().ToArray());
    }

    [Fact]
    public async Task PageIdProvided_ScopeNotFound_ReturnsEmpty()
    {
        var pageA = MakePage("p-a", parent: new ParentPage("intermediate"));

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [pageA] }));

        _client.GetPageAsync("intermediate", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Page>(
                new BuildinApiException(new ApiError(404, "not_found", "Not found", null))));

        SetupTitleRenderer();

        var results = await _service.SearchAsync("test", "scope-1", CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Determinism_TwoCallsReturnEqualLists()
    {
        var page = MakePage("p1", parent: new ParentWorkspace(null));

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [page] }));

        SetupTitleRenderer();

        var first = await _service.SearchAsync("test", null, CancellationToken.None);
        var second = await _service.SearchAsync("test", null, CancellationToken.None);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
            Assert.Equal(first[i], second[i]);
    }

    [Fact]
    public async Task CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var page = MakePage("p1", parent: new ParentPage("intermediate"));

        _client.SearchPagesAsync(Arg.Any<PageSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PageSearchResults { Results = [page] }));

        _client.GetPageAsync("intermediate", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Page
            {
                Id = "intermediate",
                Parent = new ParentPage("scope")
            }));

        SetupTitleRenderer();

        await _service.SearchAsync("test", "scope", ct);

        await _client.Received(1).SearchPagesAsync(Arg.Any<PageSearchRequest>(), ct);
        await _client.Received(1).GetPageAsync("intermediate", ct);
    }
}
