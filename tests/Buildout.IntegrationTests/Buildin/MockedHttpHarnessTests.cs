using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Buildout.IntegrationTests.Buildin;
using Xunit;

namespace Buildout.IntegrationTests.Buildin;

[Collection("BuildinWireMock")]
public sealed class MockedHttpHarnessTests
{
    private readonly BuildinWireMockFixture _fixture;

    public MockedHttpHarnessTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task GetMeAsync_DeserializesJsonResponse()
    {
        BuildinStubs.RegisterGetMe(_fixture.Server, new
        {
            id = "11111111-1111-1111-1111-111111111111",
            name = "Bot User",
            avatar_url = "https://example.com/avatar.png",
            type = "bot",
            person = new { email = "bot@example.com" }
        });

        var client = _fixture.CreateClient();
        var result = await client.GetMeAsync();

        Assert.Equal("11111111-1111-1111-1111-111111111111", result.Id);
        Assert.Equal("Bot User", result.Name);
        Assert.Equal("https://example.com/avatar.png", result.AvatarUrl);
        Assert.Equal("bot", result.Type);
        Assert.Equal("bot@example.com", result.Email);
    }

    [Fact]
    public async Task GetPageAsync_DeserializesJsonResponse()
    {
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = "22222222-2222-2222-2222-222222222222",
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = "https://api.buildin.ai/pages/22222222",
            cover = new { type = "external", external = new { url = "https://example.com/cover.png" } }
        });

        var client = _fixture.CreateClient();
        var result = await client.GetPageAsync("22222222-2222-2222-2222-222222222222");

        Assert.Equal("22222222-2222-2222-2222-222222222222", result.Id);
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero), result.CreatedAt);
        Assert.False(result.Archived);
        Assert.Equal("https://api.buildin.ai/pages/22222222", result.Url);
        Assert.Equal("https://example.com/cover.png", result.Cover);
    }

    [Fact]
    public async Task SearchPagesAsync_DeserializesJsonResponse()
    {
        BuildinStubs.RegisterSearchPages(_fixture.Server, new
        {
            @object = "list",
            results = new[]
            {
                new
                {
                    id = "44444444-4444-4444-4444-444444444444",
                    archived = false,
                    created_time = "2025-03-01T12:00:00Z",
                    last_edited_time = "2025-03-02T12:00:00Z",
                    properties = new
                    {
                        title = new
                        {
                            title = new[]
                            {
                                new { type = "text", plain_text = "Found Page" }
                            }
                        }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });

        var client = _fixture.CreateClient();
        var request = new PageSearchRequest { Query = "Found" };
        var result = await client.SearchPagesAsync(request);

        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal("44444444-4444-4444-4444-444444444444", result.Results[0].Id);
        Assert.False(result.HasMore);
    }
}
