using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Authoring;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Markdown.Authoring;

public class ParentKindProbeTests
{
    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly ParentKindProbe _sut;

    public ParentKindProbeTests()
    {
        _sut = new ParentKindProbe(_client);
    }

    [Fact]
    public async Task PageFound_ReturnsPage()
    {
        _client.GetPageAsync("p1", default).Returns(new Page { Id = "p1" });

        var result = await _sut.ProbeAsync("p1");

        var page = Assert.IsType<ParentKind.Page>(result);
        Assert.Equal("p1", page.PageId);
    }

    [Fact]
    public async Task Page404_DbFound_ReturnsDatabase()
    {
        _client.GetPageAsync("d1", default).Returns<Task<Page>>(_ => throw new BuildinApiException(new ApiError(404, null, "not found", null)));
        _client.GetDatabaseAsync("d1", default).Returns(new Database { Id = "d1", Properties = new Dictionary<string, PropertySchema>() });

        var result = await _sut.ProbeAsync("d1");

        var db = Assert.IsType<ParentKind.DatabaseParent>(result);
        Assert.Equal("d1", db.Schema.Id);
    }

    [Fact]
    public async Task Both404_ReturnsNotFound()
    {
        _client.GetPageAsync("x", default).Returns<Task<Page>>(_ => throw new BuildinApiException(new ApiError(404, null, "not found", null)));
        _client.GetDatabaseAsync("x", default).Returns<Task<Database>>(_ => throw new BuildinApiException(new ApiError(404, null, "not found", null)));

        var result = await _sut.ProbeAsync("x");

        Assert.IsType<ParentKind.NotFound>(result);
    }

    [Fact]
    public async Task ProbeOrder_PageFirst()
    {
        _client.GetPageAsync("p1", default).Returns(new Page { Id = "p1" });

        await _sut.ProbeAsync("p1");

        await _client.Received(1).GetPageAsync("p1", default);
        await _client.DidNotReceive().GetDatabaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
