using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Search;
using Buildout.Core.Search.Internal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Search;

public sealed class AncestorScopeFilterTests
{
    private readonly IBuildinClient _client;
    private readonly ILogger<AncestorScopeFilter> _logger;
    private readonly AncestorScopeFilter _filter;

    public AncestorScopeFilterTests()
    {
        _client = Substitute.For<IBuildinClient>();
        _logger = Substitute.For<ILogger<AncestorScopeFilter>>();
        _filter = new AncestorScopeFilter(_client, _logger);
    }

    private static SearchMatch Match(string pageId, Parent? parent = null) => new()
    {
        PageId = pageId,
        ObjectType = SearchObjectType.Page,
        DisplayTitle = pageId,
        Parent = parent
    };

    [Fact]
    public async Task DirectMatch_IsInScope()
    {
        var scopePageId = "scope-1";
        var match = Match(scopePageId);
        var lookup = new Dictionary<string, Parent?>();

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task OneHopParent_IsInScope()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentPage(scopePageId));
        var lookup = new Dictionary<string, Parent?>();

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TwoHopAncestor_ViaSeededLookup_IsInScope()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentPage("intermediate"));
        var lookup = new Dictionary<string, Parent?>
        {
            ["intermediate"] = new ParentPage(scopePageId)
        };

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task MissingAncestor_FetchedOnDemand()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentPage("intermediate"));
        var lookup = new Dictionary<string, Parent?>();

        _client.GetPageAsync("intermediate", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Page
            {
                Id = "intermediate",
                Parent = new ParentPage(scopePageId)
            }));

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.True(result);
        Assert.True(lookup.ContainsKey("intermediate"));
        Assert.IsType<ParentPage>(lookup["intermediate"]);
    }

    [Fact]
    public async Task ParentWorkspace_TerminatesFalse()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentWorkspace(null));
        var lookup = new Dictionary<string, Parent?>();

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ParentDatabase_TerminatesFalse()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentDatabase("db-1"));
        var lookup = new Dictionary<string, Parent?>();

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task NullParent_TerminatesFalse()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", null);
        var lookup = new Dictionary<string, Parent?>();

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task NotFound_TreatedOutOfScope()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentPage("missing"));
        var lookup = new Dictionary<string, Parent?>();

        _client.GetPageAsync("missing", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Page>(
                new BuildinApiException(new ApiError(404, "not_found", "Not found", null))));

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Forbidden_TreatedOutOfScope()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentPage("forbidden"));
        var lookup = new Dictionary<string, Parent?>();

        _client.GetPageAsync("forbidden", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Page>(
                new BuildinApiException(new ApiError(403, "forbidden", "Forbidden", null))));

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TransportError_Bubbles()
    {
        var scopePageId = "scope-1";
        var match = Match("child-1", new ParentPage("transport-fail"));
        var lookup = new Dictionary<string, Parent?>();

        _client.GetPageAsync("transport-fail", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Page>(
                new BuildinApiException(new TransportError(new HttpRequestException("connection reset")))));

        await Assert.ThrowsAsync<BuildinApiException>(
            () => _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Cycle_DetectedAndReturnsFalse()
    {
        var scopePageId = "scope-1";
        var match = Match("a", new ParentPage("b"));
        var lookup = new Dictionary<string, Parent?>
        {
            ["b"] = new ParentPage("a")
        };

        var result = await _filter.IsInScopeAsync(match, scopePageId, lookup, CancellationToken.None);

        Assert.False(result);
    }
}
