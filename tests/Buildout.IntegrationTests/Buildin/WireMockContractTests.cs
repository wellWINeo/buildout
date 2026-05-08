using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.IntegrationTests.Buildin;
using Xunit;

namespace Buildout.IntegrationTests.Buildin;

[Collection("BuildinWireMock")]
public sealed class WireMockContractTests
{
    private readonly BuildinWireMockFixture _fixture;
    private readonly IBuildinClient _client;

    public WireMockContractTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _client = _fixture.CreateClient();
    }

    [Fact]
    public async Task GetMeAsync_ReturnsDefaultUser()
    {
        var user = await _client.GetMeAsync();

        Assert.Equal("11111111-1111-1111-1111-111111111111", user.Id);
        Assert.Equal("Test Bot", user.Name);
        Assert.Equal("https://example.com/avatar.png", user.AvatarUrl);
        Assert.Equal("bot", user.Type);
        Assert.Equal("bot@example.com", user.Email);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsDefaultPage()
    {
        var page = await _client.GetPageAsync("00000000-0000-0000-0000-000000000000");

        Assert.Equal("00000000-0000-0000-0000-000000000000", page.Id);
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero), page.CreatedAt);
        Assert.False(page.Archived);
        Assert.Equal("https://api.buildin.ai/pages/00000000", page.Url);
    }

    [Fact]
    public async Task GetBlockChildrenAsync_ReturnsEmptyList()
    {
        var result = await _client.GetBlockChildrenAsync("00000000-0000-0000-0000-000000000000");

        Assert.Empty(result.Results);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task SearchPagesAsync_ReturnsEmptyResults()
    {
        var result = await _client.SearchPagesAsync(new PageSearchRequest { Query = "test" });

        Assert.Empty(result.Results);
        Assert.False(result.HasMore);
    }
}
