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
