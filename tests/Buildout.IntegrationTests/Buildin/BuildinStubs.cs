using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Buildout.IntegrationTests.Buildin;

public static class BuildinStubs
{
    public static void RegisterAll(WireMockServer server)
    {
        RegisterGetMe(server);
        RegisterGetPage(server);
        RegisterGetBlockChildren(server);
        RegisterSearchPages(server);
    }

    public static void RegisterGetMe(WireMockServer server, object? responseBody = null, int statusCode = 200)
    {
        server
            .Given(Request.Create().WithPath("/v1/users/me").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody ?? new
                {
                    id = "11111111-1111-1111-1111-111111111111",
                    name = "Test Bot",
                    avatar_url = "https://example.com/avatar.png",
                    type = "bot",
                    person = new { email = "bot@example.com" }
                }));
    }

    public static void RegisterGetPage(WireMockServer server, object? responseBody = null, int statusCode = 200)
    {
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher("^/v1/pages/[0-9a-f-]+$"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody ?? new
                {
                    id = "00000000-0000-0000-0000-000000000000",
                    created_time = "2025-01-15T10:30:00Z",
                    last_edited_time = "2025-01-16T14:00:00Z",
                    archived = false,
                    url = "https://api.buildin.ai/pages/00000000",
                    properties = new
                    {
                        title = new
                        {
                            type = "title",
                            title = Array.Empty<object>()
                        }
                    }
                }));
    }

    public static void RegisterGetBlockChildren(WireMockServer server, object? responseBody = null, int statusCode = 200)
    {
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher("^/v1/blocks/[0-9a-f-]+/children$"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody ?? new
                {
                    @object = "list",
                    results = Array.Empty<object>(),
                    has_more = false,
                    next_cursor = (string?)null
                }));
    }

    public static void RegisterSearchPages(WireMockServer server, object? responseBody = null, int statusCode = 200)
    {
        server
            .Given(Request.Create()
                .WithPath("/v1/search")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody ?? new
                {
                    @object = "list",
                    results = Array.Empty<object>(),
                    has_more = false,
                    next_cursor = (string?)null
                }));
    }
}
