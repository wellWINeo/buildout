using System.Text.Json;
using System.Text.Json.Nodes;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

namespace Buildout.IntegrationTests.Buildin;

public static class BuildinStubs
{
    public static readonly string[] ThreePageCursors = ["cursor-1", "cursor-2"];

    public static object PageResponse(string id, bool inTrash = false) => new
    {
        id, @object = "page", created_at = "2025-01-15T10:30:00Z", last_edited_at = "2025-01-16T14:00:00Z",
        in_trash = inTrash, url = $"https://api.buildin.ai/pages/{id}", properties = new { }
    };

    public static object CursorResponse(IEnumerable<object> results, bool hasMore, string? nextCursor, string? etag = null)
        => new { @object = "list", results = results.ToArray(), has_more = hasMore, next_cursor = nextCursor, etag };

    public static void RegisterV2PageWithEtag(WireMockServer server, string pageId, string etag, object? body = null)
    {
        server.Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithHeader("ETag", etag).WithBodyAsJson(body ?? PageResponse(pageId)));
    }

    public static IReadOnlyList<string> RequestJournal(WireMockServer server)
        => server.LogEntries.Select(entry => entry.RequestMessage?.Path ?? string.Empty).ToArray();

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
            .Given(Request.Create().WithPath("/v2/users/me").UsingGet())
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

    public static void RegisterGetPage(WireMockServer server, object? responseBody = null, int statusCode = 200, string etag = "\"fixture-etag\"")
    {
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher("^/v2/pages/[0-9a-f-]+$"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("ETag", etag)
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
                .WithPath(new RegexMatcher("^/v2/blocks/[0-9a-f-]+/children$"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(NormalizeBlockResponse(responseBody ?? new
                {
                    @object = "list",
                    results = Array.Empty<object>(),
                    has_more = false,
                    next_cursor = (string?)null
                })));
    }

    public static void RegisterSearchPages(WireMockServer server, object? responseBody = null, int statusCode = 200)
    {
        server
            .Given(Request.Create()
                .WithPath("/v2/search")
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

    public static void RegisterGetDatabase(WireMockServer server, string databaseId, object responseBody, int statusCode = 200)
    {
        server
            .Given(Request.Create()
                .WithPath($"/v2/databases/{databaseId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody));
    }

    public static void RegisterQueryDatabase(WireMockServer server, string databaseId, object responseBody)
    {
        server
            .Given(Request.Create()
                .WithPath($"/v2/databases/{databaseId}/query")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody));
    }

    public static void RegisterPageProbe(WireMockServer server, string pageId, object responseBody)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody));
    }

    public static void RegisterPageProbeNotFound(WireMockServer server, string pageId)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Page not found" }));
    }

    public static void RegisterDatabaseProbeNotFound(WireMockServer server, string databaseId)
    {
        server
            .Given(Request.Create().WithPath($"/v2/databases/{databaseId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Database not found" }));
    }

    public static void RegisterCreatePage(WireMockServer server, Func<object, object> respond)
    {
        server
            .Given(Request.Create().WithPath("/v2/pages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithCallback(request =>
                {
                    var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(request.Body ?? "{}");
                    var result = respond(json);
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        BodyData = new BodyData
                        {
                            BodyAsString = System.Text.Json.JsonSerializer.Serialize(result),
                            DetectedBodyType = BodyType.String
                        }
                    };
                }));
    }

    public static void RegisterCreatePage(WireMockServer server, object responseBody)
    {
        server
            .Given(Request.Create().WithPath("/v2/pages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(responseBody));
    }

    public static void RegisterAppendBlockChildren(WireMockServer server, string parentBlockId, object responseBody)
    {
        server
            .Given(Request.Create()
                .WithPath($"/v2/blocks/{parentBlockId}/children")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(NormalizeBlockResponse(responseBody)));
    }

    public static void RegisterAppendBlockChildrenFailure(WireMockServer server, string parentBlockId, int statusCode)
    {
        server
            .Given(Request.Create()
                .WithPath($"/v2/blocks/{parentBlockId}/children")
                .UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Internal server error" }));
    }

    public static void RegisterUpdateBlock(WireMockServer server, string blockId, object updatedBlock)
    {
        server
            .Given(Request.Create().WithPath($"/v2/blocks/{blockId}").UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(NormalizeBlockResponse(updatedBlock)));
    }

    public static void RegisterDeleteBlock(WireMockServer server, string blockId)
    {
        server
            .Given(Request.Create().WithPath($"/v2/blocks/{blockId}").UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { id = blockId }));
    }

    public static void RegisterUpdateBlockFailure(WireMockServer server, string blockId, int statusCode)
    {
        server
            .Given(Request.Create().WithPath($"/v2/blocks/{blockId}").UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { object_value = "error", message = "Update failed" }));
    }

    public static void RegisterGetPageArchived(WireMockServer server, string pageId, bool archived)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = pageId,
                    created_time = "2025-01-15T10:30:00Z",
                    last_edited_time = "2025-01-16T14:00:00Z",
                    archived,
                    url = $"https://api.buildin.ai/pages/{pageId}",
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

    public static void RegisterUpdatePageToggleArchived(WireMockServer server, string pageId)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithCallback(request =>
                {
                    var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(request.Body ?? "{}");
                    var archived = json.TryGetProperty("archived", out var archProp) && archProp.GetBoolean();
                    var result = new
                    {
                        id = pageId,
                        created_time = "2025-01-15T10:30:00Z",
                        last_edited_time = "2025-01-16T14:00:00Z",
                        archived,
                        url = $"https://api.buildin.ai/pages/{pageId}",
                        properties = new
                        {
                            title = new
                            {
                                type = "title",
                                title = Array.Empty<object>()
                            }
                        }
                    };
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        BodyData = new BodyData
                        {
                            BodyAsString = System.Text.Json.JsonSerializer.Serialize(result),
                            DetectedBodyType = BodyType.String
                        }
                    };
                }));
    }

    public static void RegisterGetPageNotFound(WireMockServer server, string pageId)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Page not found" }));
    }

    public static void RegisterPatchPageNotFound(WireMockServer server, string pageId)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Page not found" }));
    }

    public static void RegisterPatchPageAuthFailure(WireMockServer server, string pageId, int statusCode = 401)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Unauthorized" }));
    }

    public static void RegisterPatchPageServerError(WireMockServer server, string pageId, int statusCode = 500)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Internal server error" }));
    }

    public static void RegisterGetPageAuthFailure(WireMockServer server, string pageId, int statusCode = 401)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Unauthorized" }));
    }

    public static void RegisterGetPageServerError(WireMockServer server, string pageId, int statusCode = 500)
    {
        server
            .Given(Request.Create().WithPath($"/v2/pages/{pageId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { message = "Internal server error" }));
    }

    private static JsonElement NormalizeBlockResponse(object responseBody)
    {
        var node = JsonSerializer.SerializeToNode(responseBody)!;
        if (node is JsonObject response && response["results"] is JsonArray results)
        {
            foreach (var result in results)
                AddTypedBlockPayload(result);
        }
        else
        {
            AddTypedBlockPayload(node);
        }

        return JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
    }

    private static void AddTypedBlockPayload(JsonNode? node)
    {
        if (node is not JsonObject block || block["type"] is not JsonValue typeValue || block["data"] is not JsonObject data)
            return;

        var type = typeValue.GetValue<string>();
        if (!block.ContainsKey(type))
            block[type] = JsonNode.Parse(data.ToJsonString());
    }
}
