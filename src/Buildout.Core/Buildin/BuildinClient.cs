using System.Net.Http.Json;
using System.Text.Json;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Buildout.Core.Buildin;

/// <summary>Version-neutral Buildout facade backed exclusively by Buildin Developer API V2.</summary>
public sealed class BuildinClient : IBuildinClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly HttpClient _httpClient;
    private readonly ILogger<BuildinClient> _logger;

    public BuildinClient(HttpClient httpClient, AccessTokenResolver tokenResolver, IOptions<BuildinClientOptions> options, ILogger<BuildinClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = options.Value.BaseUrl;
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResolver.Resolve());
        _logger = logger;
    }

    public async Task<UserMe> GetMeAsync(CancellationToken cancellationToken = default)
        => MapUser(await SendAsync("users/me", HttpMethod.Get, null, cancellationToken));

    public async Task<Page> GetPageAsync(string pageId, CancellationToken cancellationToken = default)
        => MapPage(await SendAsync($"pages/{ValidateId(pageId)}", HttpMethod.Get, null, cancellationToken));

    public async Task<VersionedPage> GetVersionedPageAsync(string pageId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"pages/{ValidateId(pageId)}"), cancellationToken);
        var etag = response.Headers.ETag?.Tag;
        var page = MapPage(await ReadJsonAsync(response, cancellationToken));
        return new VersionedPage { Page = page, ETag = etag };
    }

    public async Task<Page> CreatePageAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "pages");
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        message.Content = JsonContent.Create(new { parent = MapParent(request.Parent), properties = request.Properties }, options: JsonOptions);
        using var response = await SendAsync(message, cancellationToken);
        return MapPage(await ReadJsonAsync(response, cancellationToken));
    }

    public async Task<Block> GetBlockAsync(string blockId, CancellationToken cancellationToken = default)
        => MapBlock((await SendAsync($"blocks/{ValidateId(blockId)}", HttpMethod.Get, null, cancellationToken)).RootElement);

    public async Task<Block> UpdateBlockAsync(string blockId, UpdateBlockRequest request, CancellationToken cancellationToken = default)
        => MapBlock((await SendAsync($"blocks/{ValidateId(blockId)}", HttpMethod.Patch, request, cancellationToken)).RootElement);

    public async Task DeleteBlockAsync(string blockId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"blocks/{ValidateId(blockId)}"), cancellationToken);
    }

    public async Task<PaginatedList<Block>> GetBlockChildrenAsync(string blockId, BlockChildrenQuery? query = null, CancellationToken cancellationToken = default)
    {
        var suffix = query is null ? string.Empty : $"?page_size={query.PageSize}&start_cursor={Uri.EscapeDataString(query.StartCursor ?? string.Empty)}";
        var json = await SendAsync($"blocks/{ValidateId(blockId)}/children{suffix}", HttpMethod.Get, null, cancellationToken);
        return MapBlocks(json);
    }

    public async Task<AppendBlockChildrenResult> AppendBlockChildrenAsync(string blockId, AppendBlockChildrenRequest request, CancellationToken cancellationToken = default)
    {
        var json = await SendAsync($"blocks/{ValidateId(blockId)}/children", HttpMethod.Patch, request, cancellationToken);
        return new AppendBlockChildrenResult { Results = MapBlocks(json).Results };
    }

    public async Task<Database> CreateDatabaseAsync(CreateDatabaseRequest request, CancellationToken cancellationToken = default)
        => MapDatabase(await SendAsync("databases", HttpMethod.Post, request, cancellationToken));

    public async Task<Database> GetDatabaseAsync(string databaseId, CancellationToken cancellationToken = default)
        => MapDatabase(await SendAsync($"databases/{ValidateId(databaseId)}", HttpMethod.Get, null, cancellationToken));

    public async Task<Database> UpdateDatabaseAsync(string databaseId, UpdateDatabaseRequest request, CancellationToken cancellationToken = default)
        => MapDatabase(await SendAsync($"databases/{ValidateId(databaseId)}", HttpMethod.Patch, request, cancellationToken));

    public async Task<QueryDatabaseResult> QueryDatabaseAsync(string databaseId, QueryDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        var json = await SendAsync($"databases/{ValidateId(databaseId)}/query", HttpMethod.Post, request, cancellationToken);
        return new QueryDatabaseResult
        {
            HasMore = Bool(json, "has_more") ?? false,
            NextCursor = String(json, "next_cursor")
        };
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var json = await SendAsync("search", HttpMethod.Post, request, cancellationToken);
        return new SearchResults { Results = json.RootElement.TryGetProperty("results", out var results) ? results.EnumerateArray().Select(x => x.Clone()).Cast<object>().ToArray() : [] };
    }

    public async Task<PageSearchResults> SearchPagesAsync(PageSearchRequest request, CancellationToken cancellationToken = default)
    {
        var json = await SendAsync("search", HttpMethod.Post, request, cancellationToken);
        var pages = json.RootElement.TryGetProperty("results", out var results)
            ? results.EnumerateArray().Where(x => !x.TryGetProperty("object", out var type) || type.GetString() == "page").Select(x => MapPage(x)).ToArray()
            : [];
        return new PageSearchResults
        {
            Results = pages,
            HasMore = json.RootElement.TryGetProperty("has_more", out var more) && more.GetBoolean(),
            NextCursor = json.RootElement.TryGetProperty("next_cursor", out var cursor) && cursor.ValueKind != JsonValueKind.Null ? cursor.GetString() : null
        };
    }

    private async Task<JsonDocument> SendAsync(string path, HttpMethod method, object? body, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null) message.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await SendAsync(message, cancellationToken);
        return await ReadJsonAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return response;
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BuildinApiException(new ApiError((int)response.StatusCode, null, raw, response.Headers.TryGetValues("X-Request-Id", out var ids) ? ids.FirstOrDefault() : null));
        }
        catch (BuildinApiException) { throw; }
        catch (HttpRequestException ex) { throw new BuildinApiException(new TransportError(ex), ex); }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string ValidateId(string value)
    {
        if (!Guid.TryParse(value, out var id)) throw new ArgumentException("Resource identifiers must be UUIDs.", nameof(value));
        return id.ToString();
    }

    private static object MapParent(Parent parent) => parent switch
    {
        ParentDatabase x => new { type = "database_id", database_id = x.Id },
        ParentPage x => new { type = "page_id", page_id = x.Id },
        ParentBlock x => new { type = "block_id", block_id = x.Id },
        ParentWorkspace => new { type = "workspace" },
        _ => throw new ArgumentException("Unsupported parent type.", nameof(parent))
    };

    private static UserMe MapUser(JsonDocument json) => new()
    {
        Id = String(json, "id") ?? string.Empty, Name = String(json, "name"), AvatarUrl = String(json, "avatar_url"),
        Type = String(json, "type") ?? "user", Email = json.RootElement.TryGetProperty("person", out var person) && person.TryGetProperty("email", out var email) ? email.GetString() : null
    };

    private static Page MapPage(JsonDocument json) => new()
    {
        Id = String(json, "id") ?? string.Empty, CreatedAt = Date(json, "created_at", "created_time"), LastEditedAt = Date(json, "last_edited_at", "last_edited_time"),
        InTrash = Bool(json, "in_trash") ?? Bool(json, "archived") ?? false, Url = String(json, "url"), ObjectType = String(json, "object")
    };

    private static Page MapPage(JsonElement json) => new()
    {
        Id = String(json, "id") ?? string.Empty, CreatedAt = Date(json, "created_at", "created_time"), LastEditedAt = Date(json, "last_edited_at", "last_edited_time"),
        InTrash = Bool(json, "in_trash") ?? Bool(json, "archived") ?? false, Url = String(json, "url"), ObjectType = String(json, "object")
    };

    private static Database MapDatabase(JsonDocument json) => new()
    {
        Id = String(json, "id") ?? string.Empty, CreatedAt = Date(json, "created_at", "created_time"), LastEditedAt = Date(json, "last_edited_at", "last_edited_time"),
        InTrash = Bool(json, "in_trash") ?? Bool(json, "archived") ?? false, Url = String(json, "url")
    };

    private static PaginatedList<Block> MapBlocks(JsonDocument json)
    {
        var values = json.RootElement.TryGetProperty("results", out var results) ? results.EnumerateArray().Select(MapBlock).ToArray() : [];
        return new PaginatedList<Block> { Results = values, HasMore = Bool(json, "has_more") ?? false, NextCursor = String(json, "next_cursor") };
    }

    private static Block MapBlock(JsonElement value)
    {
        var type = String(value, "type") ?? "unsupported";
        Block block = type switch
        {
            "paragraph" => new ParagraphBlock { RichTextContent = MapRichText(value, "paragraph") },
            "heading_1" => new Heading1Block { RichTextContent = MapRichText(value, "heading_1") },
            "heading_2" => new Heading2Block { RichTextContent = MapRichText(value, "heading_2") },
            "heading_3" => new Heading3Block { RichTextContent = MapRichText(value, "heading_3") },
            "to_do" => new ToDoBlock { RichTextContent = MapRichText(value, "to_do"), Checked = value.TryGetProperty(type, out var todo) && todo.TryGetProperty("checked", out var checkedValue) ? checkedValue.GetBoolean() : null },
            "divider" => new DividerBlock(),
            _ => new UnsupportedBlock()
        };
        return block with { Id = String(value, "id") ?? string.Empty, HasChildren = Bool(value, "has_children") ?? false, InTrash = Bool(value, "in_trash") ?? false };
    }

    private static RichText[] MapRichText(JsonElement value, string type)
        => value.TryGetProperty(type, out var content) && content.TryGetProperty("rich_text", out var texts)
            ? texts.EnumerateArray().Select(x => new RichText { Type = String(x, "type") ?? "text", Content = x.TryGetProperty("plain_text", out var plain) ? plain.GetString() ?? string.Empty : string.Empty }).ToArray()
            : [];

    private static string? String(JsonDocument value, string name, string? alternate = null) => String(value.RootElement, name, alternate);
    private static string? String(JsonElement value, string name, string? alternate = null)
        => value.TryGetProperty(name, out var result) && result.ValueKind == JsonValueKind.String ? result.GetString() : alternate is not null ? String(value, alternate) : null;
    private static bool? Bool(JsonDocument value, string name) => Bool(value.RootElement, name);
    private static bool? Bool(JsonElement value, string name) => value.TryGetProperty(name, out var result) && result.ValueKind is JsonValueKind.True or JsonValueKind.False ? result.GetBoolean() : null;
    private static DateTimeOffset? Date(JsonDocument value, string name, string alternate) => Date(value.RootElement, name, alternate);
    private static DateTimeOffset? Date(JsonElement value, string name, string alternate) => DateTimeOffset.TryParse(String(value, name, alternate), out var result) ? result : null;
}
