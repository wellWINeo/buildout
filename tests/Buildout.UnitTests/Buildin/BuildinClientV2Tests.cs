using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
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
    public async Task QueryDatabase_EmitsOnlyV2QueryFields()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{\"results\":[]}"));
        var client = CreateClient(handler);
        using var filterDocument = JsonDocument.Parse("{\"property\":\"Status\",\"checkbox\":{\"equals\":true}}");

        await client.QueryDatabaseAsync("11111111-1111-1111-1111-111111111111", new QueryDatabaseRequest
        {
            Filter = filterDocument.RootElement.Clone(),
            Sorts = [new Sort { Property = "Name", Direction = "ascending" }],
            StartCursor = "cursor",
            PageSize = 25
        });

        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal(new[] { "filter", "sorts", "start_cursor", "page_size" }, body.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Status", body.RootElement.GetProperty("filter").GetProperty("property").GetString());
        Assert.Equal("Name", body.RootElement.GetProperty("sorts")[0].GetProperty("property").GetString());
        Assert.DoesNotContain("Filter", handler.Bodies.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_EmitsV2FilterSortAndPaginationFields()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{\"results\":[]}"));
        var client = CreateClient(handler);

        await client.SearchAsync(new SearchRequest
        {
            Query = "buildout",
            Filter = new SearchFilter { Value = "page", Property = "object" },
            Sort = new SearchSort { Direction = "descending", Timestamp = "last_edited_time" },
            StartCursor = "cursor",
            PageSize = 10
        });

        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal(new[] { "query", "filter", "sort", "start_cursor", "page_size" }, body.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("page", body.RootElement.GetProperty("filter").GetProperty("value").GetString());
        Assert.Equal("last_edited_time", body.RootElement.GetProperty("sort").GetProperty("timestamp").GetString());
        Assert.DoesNotContain("PageSearchRequest", handler.Bodies.Single(), StringComparison.Ordinal);
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

    [Fact]
    public async Task CreateDatabase_EmitsStrictV2TitleParentAndSchemaShape()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"11111111-1111-1111-1111-111111111111\"}"));
        var client = CreateClient(handler);

        await client.CreateDatabaseAsync(new CreateDatabaseRequest
        {
            Parent = new ParentPage("22222222-2222-2222-2222-222222222222"),
            Title = [new RichText { Type = "text", Content = "Directory" }],
            Properties = new Dictionary<string, PropertySchema>
            {
                ["Name"] = new TitlePropertySchema(),
                ["Department"] = new SelectPropertySchema
                {
                    Options = [new SelectOption { Id = "eng", Name = "Engineering" }]
                }
            }
        });

        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal(new[] { "title", "properties", "parent" }, body.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("22222222-2222-2222-2222-222222222222", body.RootElement.GetProperty("parent").GetProperty("page_id").GetString());
        Assert.True(body.RootElement.GetProperty("properties").GetProperty("Name").TryGetProperty("title", out _));
        Assert.Equal("Engineering", body.RootElement.GetProperty("properties").GetProperty("Department").GetProperty("select").GetProperty("options")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetPage_MapsTitleParentPropertiesAndRichTextMetadata()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            {
              "object": "page",
              "id": "11111111-1111-1111-1111-111111111111",
              "parent": { "type": "page_id", "page_id": "22222222-2222-2222-2222-222222222222" },
              "properties": {
                "Title": {
                  "id": "title",
                  "type": "title",
                  "title": [{
                    "type": "text",
                    "text": { "content": "Linked title", "link": { "url": "https://example.com" } },
                    "plain_text": "Linked title",
                    "href": "https://example.com",
                    "annotations": { "bold": true, "italic": false, "color": "red", "background_color": "blue" }
                  }]
                },
                "Status": { "id": "status", "type": "checkbox", "checkbox": true }
              }
            }
            """));
        var client = CreateClient(handler);

        var page = await client.GetPageAsync("11111111-1111-1111-1111-111111111111");

        var title = Assert.Single(page.Title!);
        Assert.Equal("Linked title", title.Content);
        Assert.Equal("https://example.com", title.Href);
        Assert.True(title.Annotations!.Bold);
        Assert.Equal("blue", title.Annotations.BackgroundColor);
        Assert.Equal(new ParentPage("22222222-2222-2222-2222-222222222222"), page.Parent);
        Assert.IsType<CheckboxPropertyValue>(page.Properties!["Status"]);
    }

    [Fact]
    public async Task CreatePage_PageParent_EmitsOnlyV2ParentIdAndStrictPropertyShape()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            { "object": "page", "id": "33333333-3333-3333-3333-333333333333" }
            """));
        var client = CreateClient(handler);

        await client.CreatePageAsync(new CreatePageRequest
        {
            Parent = new ParentPage("11111111-1111-1111-1111-111111111111"),
            Properties = new Dictionary<string, PropertyValue>
            {
                ["Title"] = new TitlePropertyValue
                {
                    Title = [new RichText
                    {
                        Type = "text",
                        Content = "Hello",
                        Href = "https://example.com",
                        Annotations = new Annotations { Bold = true, Color = "red" }
                    }]
                }
            }
        });

        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var root = body.RootElement;
        Assert.Equal(new[] { "parent", "properties" }, root.EnumerateObject().Select(p => p.Name));
        var parent = root.GetProperty("parent");
        Assert.Equal(new[] { "page_id" }, parent.EnumerateObject().Select(p => p.Name));
        Assert.Equal("11111111-1111-1111-1111-111111111111", parent.GetProperty("page_id").GetString());
        Assert.Equal(new[] { "title" }, root.GetProperty("properties").GetProperty("Title").EnumerateObject().Select(p => p.Name));
        var text = root.GetProperty("properties").GetProperty("Title").GetProperty("title")[0];
        Assert.Equal("text", text.GetProperty("type").GetString());
        Assert.Equal("Hello", text.GetProperty("text").GetProperty("content").GetString());
        Assert.Equal("https://example.com", text.GetProperty("text").GetProperty("link").GetProperty("url").GetString());
        Assert.True(text.GetProperty("annotations").GetProperty("bold").GetBoolean());
        Assert.DoesNotContain("database_id", handler.Bodies.Single(), StringComparison.Ordinal);
        Assert.DoesNotContain("plain_text", text.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePage_WorkspaceRoot_OmitsParent()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            { "object": "page", "id": "33333333-3333-3333-3333-333333333333" }
            """));
        var client = CreateClient(handler);

        await client.CreatePageAsync(new CreatePageRequest
        {
            Parent = new ParentWorkspace(null),
            Properties = new Dictionary<string, PropertyValue>()
        });

        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal(new[] { "properties" }, body.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task AppendBlockChildren_EmitsV2RichTextMetadataAndOmitsUnsetFields()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{ "results": [] }"""));
        var client = CreateClient(handler);

        await client.AppendBlockChildrenAsync("11111111-1111-1111-1111-111111111111", new AppendBlockChildrenRequest
        {
            Children = [new ParagraphBlock
            {
                RichTextContent = [new RichText
                {
                    Type = "text",
                    Content = "Hello",
                    Href = "https://example.com",
                    Annotations = new Annotations { Italic = true }
                }]
            }]
        });

        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var child = body.RootElement.GetProperty("children")[0];
        Assert.Equal("block", child.GetProperty("object").GetString());
        Assert.Equal("paragraph", child.GetProperty("type").GetString());
        Assert.Equal("Hello", child.GetProperty("paragraph").GetProperty("rich_text")[0].GetProperty("text").GetProperty("content").GetString());
        Assert.Equal("https://example.com", child.GetProperty("paragraph").GetProperty("rich_text")[0].GetProperty("text").GetProperty("link").GetProperty("url").GetString());
        Assert.True(child.GetProperty("paragraph").GetProperty("rich_text")[0].GetProperty("annotations").GetProperty("italic").GetBoolean());
        Assert.DoesNotContain("children", child.GetProperty("paragraph").GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("checked", child.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBlockChildren_MapsAllRetainedBlockTypesAndMetadata()
    {
        var types = new[]
        {
            "paragraph", "heading_1", "heading_2", "heading_3", "bulleted_list_item",
            "numbered_list_item", "to_do", "toggle", "code", "quote", "divider", "image",
            "embed", "table", "table_row", "column_list", "column", "child_page", "child_database",
            "synced_block", "link_preview"
        };
        var blocks = types.Select((type, index) => $$"""
            {
              "id": "{{index + 1:D8}}-0000-0000-0000-000000000000",
              "type": "{{type}}",
              "has_children": false,
              "{{type}}": {
                "rich_text": [{ "type": "text", "plain_text": "{{type}}", "href": "https://example.com/{{type}}", "annotations": { "code": true } }],
                "checked": true,
                "language": "csharp",
                "url": "https://example.com/{{type}}",
                "title": "{{type}}",
                "synced_from": { "block_id": "22222222-2222-2222-2222-222222222222" }
              }
            }
            """);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, $"{{\"results\":[{string.Join(',', blocks)}]}}"));
        var client = CreateClient(handler);

        var result = await client.GetBlockChildrenAsync("11111111-1111-1111-1111-111111111111");

        Assert.Equal(types, result.Results.Select(block => block.Type));
        var code = Assert.IsType<CodeBlock>(result.Results.Single(block => block.Type == "code"));
        Assert.Equal("csharp", code.Language);
        Assert.Equal("https://example.com/code", code.RichTextContent![0].Href);
        Assert.True(code.RichTextContent[0].Annotations!.Code);
        Assert.Equal("child_page", Assert.IsType<ChildPageBlock>(result.Results.Single(block => block.Type == "child_page")).Type);
        Assert.Equal("child_database", Assert.IsType<ChildDatabaseBlock>(result.Results.Single(block => block.Type == "child_database")).Type);
    }

    [Fact]
    public async Task SearchPages_PreservesDatabaseUnionResultsAsDomainProjections()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """
            {
              "results": [
                { "object": "page", "id": "11111111-1111-1111-1111-111111111111", "properties": { "Title": { "type": "title", "title": [{ "type": "text", "plain_text": "Page" }] } } },
                { "object": "database", "id": "22222222-2222-2222-2222-222222222222", "title": [{ "type": "text", "plain_text": "Database" }], "parent": { "type": "workspace" } }
              ],
              "has_more": false
            }
            """));
        var client = CreateClient(handler);

        var result = await client.SearchPagesAsync(new PageSearchRequest { Query = "all" });

        Assert.Equal(2, result.Results.Count);
        Assert.Equal("page", result.Results[0].ObjectType);
        Assert.Equal("Page", result.Results[0].Title![0].Content);
        Assert.Equal("database", result.Results[1].ObjectType);
        Assert.Equal("Database", result.Results[1].Title![0].Content);
        Assert.Equal(new ParentWorkspace("workspace"), result.Results[1].Parent);

        var union = await client.SearchAsync(new SearchRequest { Query = "all" });
        Assert.Contains(union.Results, item => item is Database database && database.Id == "22222222-2222-2222-2222-222222222222");
    }

    [Fact]
    public async Task ErrorBody_RequestIdAndArrayDetailsTakePrecedenceOverHeaders()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = Json(HttpStatusCode.BadRequest, """
                {
                  "code": "validation_error",
                  "message": "Invalid request",
                  "request_id": "body-request",
                  "details": [{ "path": "properties.Title", "reason": "required", "limit": 1, "actual": 0 }]
                }
                """);
            response.Headers.Add("X-Request-Id", "header-request");
            return response;
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<BuildinApiException>(() => client.GetPageAsync("11111111-1111-1111-1111-111111111111"));
        var error = Assert.IsType<ApiError>(exception.Error);

        Assert.Equal("body-request", error.RequestId);
        var detail = Assert.Single(error.Details!);
        Assert.Equal("properties.Title", detail.Path);
        Assert.Equal("required", detail.Reason);
        Assert.Equal(1, detail.Limit);
        Assert.Equal(0, detail.Actual);
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
