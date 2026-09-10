using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Mapping;
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
        _httpClient.BaseAddress = GetV2BaseAddress(options.Value.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResolver.Resolve());
        _logger = logger;
    }

    public async Task<UserMe> GetMeAsync(CancellationToken cancellationToken = default)
        => MapUser(await SendAsync("users/me", HttpMethod.Get, null, cancellationToken));

    public async Task<Page> GetPageAsync(string pageId, CancellationToken cancellationToken = default)
        => PageMapper.Map((await SendAsync($"pages/{ValidateId(pageId)}", HttpMethod.Get, null, cancellationToken)).RootElement);

    public async Task<VersionedPage> GetVersionedPageAsync(string pageId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"pages/{ValidateId(pageId)}"), cancellationToken);
        var etag = response.Headers.ETag?.Tag;
        var page = PageMapper.Map((await ReadJsonAsync(response, cancellationToken)).RootElement);
        return new VersionedPage { Page = page, ETag = etag };
    }

    public async Task<Page> CreatePageAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        var key = Guid.NewGuid().ToString("N");
        var body = V2RequestMapper.CreatePage(request).ToJsonString(JsonOptions);
        using var response = await SendCreatePageAsync(key, body, cancellationToken);
        return PageMapper.Map((await ReadJsonAsync(response, cancellationToken)).RootElement);
    }

    public async Task<Block> GetBlockAsync(string blockId, CancellationToken cancellationToken = default)
        => BlockMapper.Map((await SendAsync($"blocks/{ValidateId(blockId)}", HttpMethod.Get, null, cancellationToken)).RootElement);

    public async Task<Block> UpdateBlockAsync(string blockId, UpdateBlockRequest request, CancellationToken cancellationToken = default)
        => BlockMapper.Map((await SendAsync($"blocks/{ValidateId(blockId)}", HttpMethod.Patch, V2RequestMapper.UpdateBlock(request), cancellationToken)).RootElement);

    public async Task DeleteBlockAsync(string blockId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"blocks/{ValidateId(blockId)}"), cancellationToken);
    }

    public async Task<PaginatedList<Block>> GetBlockChildrenAsync(string blockId, BlockChildrenQuery? query = null, CancellationToken cancellationToken = default)
    {
        ValidatePageSize(query?.PageSize);
        var suffix = query is null ? string.Empty : BuildCursorSuffix(query.PageSize, query.StartCursor);
        var json = await SendAsync($"blocks/{ValidateId(blockId)}/children{suffix}", HttpMethod.Get, null, cancellationToken);
        return MapBlocks(json);
    }

    public async Task<AppendBlockChildrenResult> AppendBlockChildrenAsync(string blockId, AppendBlockChildrenRequest request, CancellationToken cancellationToken = default)
    {
        var json = await SendAsync($"blocks/{ValidateId(blockId)}/children", HttpMethod.Patch, V2RequestMapper.AppendBlockChildren(request), cancellationToken);
        return new AppendBlockChildrenResult { Results = MapBlocks(json).Results };
    }

    public async Task<Database> CreateDatabaseAsync(CreateDatabaseRequest request, CancellationToken cancellationToken = default)
        => DatabaseMapper.Map((await SendAsync("databases", HttpMethod.Post, V2RequestMapper.CreateDatabase(request), cancellationToken)).RootElement);

    public async Task<Database> GetDatabaseAsync(string databaseId, CancellationToken cancellationToken = default)
        => DatabaseMapper.Map((await SendAsync($"databases/{ValidateId(databaseId)}", HttpMethod.Get, null, cancellationToken)).RootElement);

    public async Task<Database> UpdateDatabaseAsync(string databaseId, UpdateDatabaseRequest request, CancellationToken cancellationToken = default)
        => DatabaseMapper.Map((await SendAsync($"databases/{ValidateId(databaseId)}", HttpMethod.Patch, V2RequestMapper.UpdateDatabase(request), cancellationToken)).RootElement);

    public async Task<QueryDatabaseResult> QueryDatabaseAsync(string databaseId, QueryDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePageSize(request.PageSize);
        var json = await SendAsync($"databases/{ValidateId(databaseId)}/query", HttpMethod.Post, V2RequestMapper.QueryDatabase(request), cancellationToken);
        return DatabaseMapper.MapQueryResponse(json.RootElement);
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePageSize(request.PageSize);
        var json = await SendAsync("search", HttpMethod.Post, V2RequestMapper.Search(request), cancellationToken);
        var results = MapSearchResults(json.RootElement);
        return new SearchResults
        {
            Results = results,
            HasMore = Bool(json, "has_more") ?? false,
            NextCursor = String(json, "next_cursor")
        };
    }

    public async Task<PageSearchResults> SearchPagesAsync(PageSearchRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePageSize(request.PageSize);
        var json = await SendAsync("search", HttpMethod.Post, V2RequestMapper.Search(new SearchRequest
        {
            Query = request.Query,
            Filter = request.Filter,
            Sort = request.Sort,
            StartCursor = request.StartCursor,
            PageSize = request.PageSize
        }), cancellationToken);
        var pages = json.RootElement.TryGetProperty("results", out var results)
            ? results.EnumerateArray().Select(x => String(x, "object") == "database" ? PageMapper.MapDatabaseAsPage(x) : PageMapper.Map(x)).ToArray()
            : [];
        return new PageSearchResults
        {
            Results = pages,
            HasMore = json.RootElement.TryGetProperty("has_more", out var more) && more.GetBoolean(),
            NextCursor = json.RootElement.TryGetProperty("next_cursor", out var cursor) && cursor.ValueKind != JsonValueKind.Null ? cursor.GetString() : null
        };
    }

    private async Task<JsonDocument> SendAsync(string path, HttpMethod method, JsonNode? body, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null) message.Content = new StringContent(body.ToJsonString(JsonOptions), System.Text.Encoding.UTF8, "application/json");
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
            throw new BuildinApiException(ParseApiError(response.StatusCode, raw, response.Headers));
        }
        catch (BuildinApiException) { throw; }
        catch (HttpRequestException ex) { throw new BuildinApiException(new TransportError(ex), ex); }
    }

    private async Task<HttpResponseMessage> SendCreatePageAsync(string idempotencyKey, string body, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "pages");
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

            try
            {
                return await SendAsync(request, cancellationToken);
            }
            catch (BuildinApiException ex) when (attempt == 0 && IsRetryableCreateFailure(ex.Error))
            {
                if (ex.Error is ApiError { RetryAfter: { } retryAfter })
                    await Task.Delay(retryAfter, cancellationToken);
                else
                    await Task.Yield();
            }
        }
    }

    private static bool IsRetryableCreateFailure(BuildinError error)
        => error is TransportError || error is ApiError { StatusCode: (int)HttpStatusCode.TooManyRequests };

    private static ApiError ParseApiError(HttpStatusCode statusCode, string rawBody, System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        string? code = null;
        var message = rawBody;
        string? requestId = null;
        IReadOnlyList<ApiErrorDetail>? details = null;
        try
        {
            using var json = JsonDocument.Parse(rawBody);
            var root = json.RootElement;
            code = String(root, "code", "error");
            message = String(root, "message", "error_description") ?? rawBody;
            requestId = String(root, "request_id");
            if (root.TryGetProperty("details", out var detailArray))
            {
                if (detailArray.ValueKind == JsonValueKind.Array)
                    details = detailArray.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(MapErrorDetail).ToArray();
                else if (detailArray.ValueKind == JsonValueKind.Object)
                    details = [MapErrorDetail(detailArray)];
            }
        }
        catch (JsonException)
        {
            // Preserve non-JSON response bodies as opaque diagnostics.
        }

        TimeSpan? retryAfter = null;
        if (headers.RetryAfter?.Delta is { } delta)
            retryAfter = delta;
        else if (headers.RetryAfter?.Date is { } date)
            retryAfter = date - DateTimeOffset.UtcNow;

        return new ApiError((int)statusCode, code, message, rawBody)
        {
            RequestId = string.IsNullOrWhiteSpace(requestId)
                ? (headers.TryGetValues("X-Request-Id", out var ids) ? ids.FirstOrDefault() : null)
                : requestId,
            Details = details,
            RetryAfter = retryAfter is { } value && value > TimeSpan.Zero ? value : null
        };
    }

    private static ApiErrorDetail MapErrorDetail(JsonElement element)
    {
        var additional = element.EnumerateObject()
            .Where(property => property.Name is not ("path" or "reason" or "limit" or "actual") && !SensitiveField(property.Name))
            .ToDictionary(property => property.Name, property => property.Value.ToString(), StringComparer.Ordinal);
        return new ApiErrorDetail(
            String(element, "path"),
            String(element, "reason"),
            Int(element, "limit"),
            Int(element, "actual"),
            additional.Count == 0 ? null : additional);
    }

    private static bool SensitiveField(string name)
        => name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("authorization", StringComparison.OrdinalIgnoreCase);

    private static string BuildCursorSuffix(int? pageSize, string? cursor)
    {
        var values = new List<string>();
        if (pageSize is not null) values.Add($"page_size={pageSize.Value}");
        if (cursor is not null) values.Add($"start_cursor={Uri.EscapeDataString(cursor)}");
        return values.Count == 0 ? string.Empty : "?" + string.Join("&", values);
    }

    private static void ValidatePageSize(int? pageSize)
    {
        if (pageSize is not null && (pageSize < 1 || pageSize > 100))
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
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

    private static Uri GetV2BaseAddress(Uri configuredBaseAddress)
    {
        var path = configuredBaseAddress.AbsolutePath.TrimEnd('/');
        return path.EndsWith("/v2", StringComparison.OrdinalIgnoreCase)
            ? new Uri(configuredBaseAddress.ToString().TrimEnd('/') + "/")
            : new Uri(configuredBaseAddress, "v2/");
    }

    private static UserMe MapUser(JsonDocument json) => new()
    {
        Id = String(json, "id") ?? string.Empty, Name = String(json, "name"), AvatarUrl = String(json, "avatar_url"),
        Type = String(json, "type") ?? "user", Email = json.RootElement.TryGetProperty("person", out var person) && person.TryGetProperty("email", out var email) ? email.GetString() : null
    };

    private static PaginatedList<Block> MapBlocks(JsonDocument json)
    {
        var values = json.RootElement.TryGetProperty("results", out var results) ? results.EnumerateArray().Select(BlockMapper.Map).ToArray() : [];
        return new PaginatedList<Block> { Results = values, HasMore = Bool(json, "has_more") ?? false, NextCursor = String(json, "next_cursor") };
    }

    private static object[] MapSearchResults(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return [];
        return results.EnumerateArray().Select(result => String(result, "object") switch
        {
            "database" => (object)DatabaseMapper.Map(result),
            "page" => PageMapper.Map(result),
            _ => result.Clone()
        }).ToArray();
    }

    private static string? String(JsonDocument value, string name, string? alternate = null) => String(value.RootElement, name, alternate);
    private static string? String(JsonElement value, string name, string? alternate = null)
        => value.TryGetProperty(name, out var result) && result.ValueKind == JsonValueKind.String ? result.GetString() : alternate is not null ? String(value, alternate) : null;
    private static bool? Bool(JsonDocument value, string name) => Bool(value.RootElement, name);
    private static bool? Bool(JsonElement value, string name) => value.TryGetProperty(name, out var result) && result.ValueKind is (JsonValueKind.True or JsonValueKind.False) ? result.GetBoolean() : null;
    private static int? Int(JsonElement value, string name) => value.TryGetProperty(name, out var result) && result.ValueKind == JsonValueKind.Number && result.TryGetInt32(out var number) ? number : null;
    private static DateTimeOffset? Date(JsonDocument value, string name, string alternate) => Date(value.RootElement, name, alternate);
    private static DateTimeOffset? Date(JsonElement value, string name, string alternate) => DateTimeOffset.TryParse(String(value, name, alternate), out var result) ? result : null;
}
