using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Buildout.UnitTests.Buildin;

public sealed class BuildinClientV2Tests
{
    private static readonly Uri BaseUri = new("https://api.buildin.ai/v2/");

    [Fact]
    public async Task GetVersionedPage_UsesV2RouteAndCapturesEtag()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{\"object\":\"page\",\"id\":\"11111111-1111-1111-1111-111111111111\",\"in_trash\":false}"));
        handler.ResponseHeaders.ETag = new EntityTagHeaderValue("\"page-v2\"");
        var client = CreateClient(handler);

        var result = await client.GetVersionedPageAsync("11111111-1111-1111-1111-111111111111");

        Assert.Equal("\"page-v2\"", result.ETag);
        Assert.Equal("/v2/pages/11111111-1111-1111-1111-111111111111", handler.Requests.Single().RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task CreatePage_RetryAfter429_ReusesIdempotencyKeyAndBody()
    {
        var handlerResponseCount = 0;
        var handler = new RecordingHandler(request => handlerResponseCount++ == 0
            ? Json(HttpStatusCode.TooManyRequests, "{\"code\":\"rate_limited\",\"message\":\"retry\"}")
            : Json(HttpStatusCode.OK, "{\"object\":\"page\",\"id\":\"11111111-1111-1111-1111-111111111111\",\"in_trash\":false}"));
        var client = CreateClient(handler);

        await client.CreatePageAsync(new CreatePageRequest
        {
            Parent = new ParentWorkspace(null),
            Properties = new Dictionary<string, PropertyValue>()
        });

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.StartsWith("/v2/pages", request.RequestUri!.AbsolutePath));
        Assert.Equal(handler.Requests[0].Headers.GetValues("Idempotency-Key").Single(), handler.Requests[1].Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
    }

    [Fact]
    public async Task BlockChildren_InvalidPageSizeFailsBeforeDispatch()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{\"results\":[]}"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetBlockChildrenAsync(
            "11111111-1111-1111-1111-111111111111", new BlockChildrenQuery { PageSize = 101 }));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task QueryDatabase_MapsRowsAndPaginationFromV2Response()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            {
              "results": [
                {
                  "id": "22222222-2222-2222-2222-222222222222",
                  "url": "https://app.buildin.ai/page/22222222-2222-2222-2222-222222222222",
                  "properties": {
                    "Name": {
                      "type": "title",
                      "title": [{ "type": "text", "plain_text": "Alice Chen" }]
                    },
                    "Department": {
                      "type": "select",
                      "select": { "name": "Engineering" }
                    }
                  }
                }
              ],
              "has_more": true,
              "next_cursor": "next-page"
            }
            """));
        var client = CreateClient(handler);

        var result = await client.QueryDatabaseAsync(
            "11111111-1111-1111-1111-111111111111", new QueryDatabaseRequest());

        var row = Assert.Single(result.Results);
        var name = Assert.IsType<TitlePropertyValue>(row["Name"]);
        var department = Assert.IsType<SelectPropertyValue>(row["Department"]);
        var page = Assert.Single(result.Pages);
        Assert.Equal("Alice Chen", Assert.Single(name.Title!).Content);
        Assert.Equal("Engineering", department.Select!.Name);
        Assert.Equal("22222222-2222-2222-2222-222222222222", page.Id);
        Assert.Equal("https://app.buildin.ai/page/22222222-2222-2222-2222-222222222222", page.Url);
        Assert.True(result.HasMore);
        Assert.Equal("next-page", result.NextCursor);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.Equal("/v2/databases/11111111-1111-1111-1111-111111111111/query", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetDatabase_MapsTitleAndPropertySchemasFromV2Response()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "title": [{ "type": "text", "plain_text": "Employee Directory" }],
              "properties": {
                "Name": { "type": "title", "title": {} },
                "Department": {
                  "type": "select",
                  "select": { "options": [{ "name": "Engineering" }] }
                }
              }
            }
            """));
        var client = CreateClient(handler);

        var database = await client.GetDatabaseAsync("11111111-1111-1111-1111-111111111111");

        Assert.Equal("Employee Directory", Assert.Single(database.Title!).Content);
        Assert.IsType<TitlePropertySchema>(database.Properties!["Name"]);
        var department = Assert.IsType<SelectPropertySchema>(database.Properties["Department"]);
        Assert.Equal("Engineering", Assert.Single(department.Options!).Name);
        Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
        Assert.Equal("/v2/databases/11111111-1111-1111-1111-111111111111", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    private static BuildinClient CreateClient(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        var options = Options.Create(new BuildinClientOptions { BaseUrl = BaseUri, AccessToken = "test-token" });
        return new BuildinClient(httpClient, new AccessTokenResolver(options, NullLogger<AccessTokenResolver>.Instance), options, NullLogger<BuildinClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public HttpResponseHeaders ResponseHeaders { get; } = new HttpResponseMessage().Headers;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            var response = _respond(request);
            if (ResponseHeaders.ETag is not null) response.Headers.ETag = ResponseHeaders.ETag;
            return response;
        }
    }
}
