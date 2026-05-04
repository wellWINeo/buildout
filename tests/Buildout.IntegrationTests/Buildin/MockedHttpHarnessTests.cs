using System.Net;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Authentication;
using Buildout.Core.Buildin.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Buildout.IntegrationTests.Buildin;

public sealed class MockedHttpHarnessTests
{
    private static BotBuildinClient CreateClient(MockHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.buildin.ai") };
        var authProvider = new BotTokenAuthenticationProvider("test-token");
        var options = Options.Create(new BuildinClientOptions());
        var logger = new TestLogger<BotBuildinClient>();
        return new BotBuildinClient(httpClient, authProvider, options, logger);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    [Fact]
    public async Task GetMeAsync_DeserializesJsonResponse()
    {
        var json = """
        {
            "id": "11111111-1111-1111-1111-111111111111",
            "name": "Bot User",
            "avatar_url": "https://example.com/avatar.png",
            "type": "bot",
            "person": { "email": "bot@example.com" }
        }
        """;

        using var handler = new MockHttpHandler(json, HttpStatusCode.OK);
        var client = CreateClient(handler);

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
        var json = """
        {
            "id": "22222222-2222-2222-2222-222222222222",
            "created_time": "2025-01-15T10:30:00Z",
            "last_edited_time": "2025-01-16T14:00:00Z",
            "archived": false,
            "url": "https://api.buildin.ai/pages/22222222",
            "cover": { "type": "external", "external": { "url": "https://example.com/cover.png" } }
        }
        """;

        using var handler = new MockHttpHandler(json, HttpStatusCode.OK);
        var client = CreateClient(handler);

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
        var json = """
        {
            "object": "list",
            "results": [
                {
                    "id": "44444444-4444-4444-4444-444444444444",
                    "archived": false,
                    "created_time": "2025-03-01T12:00:00Z",
                    "last_edited_time": "2025-03-02T12:00:00Z",
                    "properties": { "title": { "title": [{ "type": "text", "plain_text": "Found Page" }] } }
                }
            ],
            "has_more": false,
            "next_cursor": null
        }
        """;

        using var handler = new MockHttpHandler(json, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var request = new PageSearchRequest { Query = "Found" };
        var result = await client.SearchPagesAsync(request);

        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal("44444444-4444-4444-4444-444444444444", result.Results[0].Id);
        Assert.False(result.HasMore);
    }

    private sealed class MockHttpHandler : DelegatingHandler
    {
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;

        public MockHttpHandler(string responseJson, HttpStatusCode statusCode)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
