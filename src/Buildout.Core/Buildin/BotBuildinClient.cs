using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Mapping;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Http.HttpClientLibrary;
using KiotaApiException = Microsoft.Kiota.Abstractions.ApiException;

namespace Buildout.Core.Buildin;

public sealed class BotBuildinClient : IBuildinClient
{
    private readonly Generated.BuildinApiClient _apiClient;
    private readonly ILogger<BotBuildinClient> _logger;

    public BotBuildinClient(IRequestAdapter requestAdapter, IOptions<BuildinClientOptions> options, ILogger<BotBuildinClient> logger)
    {
        _apiClient = new Generated.BuildinApiClient(requestAdapter);
        _logger = logger;
    }

    public BotBuildinClient(HttpClient httpClient, IAuthenticationProvider authProvider, IOptions<BuildinClientOptions> options, ILogger<BotBuildinClient> logger)
    {
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
        _apiClient = new Generated.BuildinApiClient(adapter);
        _logger = logger;
    }

    public async Task<UserMe> GetMeAsync(CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var result = await _apiClient.V1.Users.Me.GetAsync(cancellationToken: cancellationToken);
            return UserMapper.Map(result ?? throw new InvalidOperationException("GetMe returned null"));
        });
    }

    public async Task<Page> GetPageAsync(string pageId, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(pageId);
            var result = await _apiClient.V1.Pages[guid].GetAsync(cancellationToken: cancellationToken);
            return PageMapper.Map(result ?? throw new InvalidOperationException("GetPage returned null"));
        });
    }

    public async Task<Page> CreatePageAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var body = new Gen.CreatePageRequest
            {
                Parent = MapParent(request.Parent),
                Properties = MapProperties(request.Properties),
            };
            var result = await _apiClient.V1.Pages.PostAsync(body, cancellationToken: cancellationToken);
            return PageMapper.Map(result ?? throw new InvalidOperationException("CreatePage returned null"));
        });
    }

    private static Gen.Parent MapParent(Parent parent)
    {
        return parent switch
        {
            ParentDatabase db => new Gen.Parent { ParentDatabaseId = new Gen.ParentDatabaseId { DatabaseId = Guid.Parse(db.Id), Type = "database_id" } },
            ParentPage page => new Gen.Parent { ParentPageId = new Gen.ParentPageId { PageId = Guid.Parse(page.Id), Type = "page_id" } },
            ParentBlock block => new Gen.Parent { ParentBlockId = new Gen.ParentBlockId { BlockId = Guid.Parse(block.Id), Type = "block_id" } },
            _ => throw new ArgumentException($"Unsupported parent type: {parent.GetType().Name}")
        };
    }

    private static Gen.CreatePageRequest_properties MapProperties(Dictionary<string, PropertyValue> properties)
    {
        var result = new Gen.CreatePageRequest_properties();
        foreach (var (key, value) in properties)
        {
            result.AdditionalData[key] = MapPropertyValue(value);
        }
        return result;
    }

    private static UntypedObject MapPropertyValue(PropertyValue value)
    {
        return value switch
        {
            TitlePropertyValue title => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("title"),
                ["title"] = new UntypedArray(
                    (title.Title ?? []).Select(rt => (UntypedNode)new UntypedObject(new Dictionary<string, UntypedNode>
                    {
                        ["type"] = new UntypedString("text"),
                        ["text"] = new UntypedObject(new Dictionary<string, UntypedNode> { ["content"] = new UntypedString(rt.Content) }),
                        ["plain_text"] = new UntypedString(rt.Content)
                    })).ToList())
            }),
            RichTextPropertyValue richText => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("rich_text"),
                ["rich_text"] = new UntypedArray(
                    (richText.RichText ?? []).Select(rt => (UntypedNode)new UntypedObject(new Dictionary<string, UntypedNode>
                    {
                        ["type"] = new UntypedString("text"),
                        ["text"] = new UntypedObject(new Dictionary<string, UntypedNode> { ["content"] = new UntypedString(rt.Content) }),
                        ["plain_text"] = new UntypedString(rt.Content)
                    })).ToList())
            }),
            NumberPropertyValue number => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("number"),
                ["number"] = number.Number.HasValue ? (UntypedNode)new UntypedDouble(number.Number.Value) : new UntypedNull()
            }),
            CheckboxPropertyValue checkbox => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("checkbox"),
                ["checkbox"] = new UntypedBoolean(checkbox.Checkbox ?? false)
            }),
            SelectPropertyValue select => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("select"),
                ["select"] = select.Select is not null
                    ? (UntypedNode)new UntypedObject(new Dictionary<string, UntypedNode> { ["name"] = new UntypedString(select.Select.Name) })
                    : new UntypedNull()
            }),
            MultiSelectPropertyValue multiSelect => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("multi_select"),
                ["multi_select"] = new UntypedArray(
                    (multiSelect.MultiSelect ?? []).Select(o => (UntypedNode)new UntypedObject(new Dictionary<string, UntypedNode> { ["name"] = new UntypedString(o.Name) })).ToList())
            }),
            UrlPropertyValue url => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("url"),
                ["url"] = url.Url is not null ? (UntypedNode)new UntypedString(url.Url) : new UntypedNull()
            }),
            DatePropertyValue date => new UntypedObject(new Dictionary<string, UntypedNode>
            {
                ["type"] = new UntypedString("date"),
                ["date"] = date.Date is not null
                    ? (UntypedNode)new UntypedObject(new Dictionary<string, UntypedNode>
                    {
                        ["start"] = new UntypedString(date.Date.Start ?? string.Empty),
                        ["end"] = date.Date.End is not null ? (UntypedNode)new UntypedString(date.Date.End) : new UntypedNull()
                    })
                    : new UntypedNull()
            }),
            _ => new UntypedObject(new Dictionary<string, UntypedNode> { ["type"] = new UntypedString(value.Type) })
        };
    }

    public async Task<Page> UpdatePageAsync(string pageId, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(pageId);
            var body = new Gen.UpdatePageRequest
            {
                Archived = request.Archived,
            };
            var result = await _apiClient.V1.Pages[guid].PatchAsync(body, cancellationToken: cancellationToken);
            return PageMapper.Map(result ?? throw new InvalidOperationException("UpdatePage returned null"));
        });
    }

    public async Task<Block> GetBlockAsync(string blockId, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(blockId);
            var result = await _apiClient.V1.Blocks[guid].GetAsync(cancellationToken: cancellationToken);
            return BlockMapper.Map(result ?? throw new InvalidOperationException("GetBlock returned null"));
        });
    }

    public async Task<Block> UpdateBlockAsync(string blockId, UpdateBlockRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(blockId);
            var body = new Gen.UpdateBlockRequest();
            var result = await _apiClient.V1.Blocks[guid].PatchAsync(body, cancellationToken: cancellationToken);
            return BlockMapper.Map(result ?? throw new InvalidOperationException("UpdateBlock returned null"));
        });
    }

    public async Task DeleteBlockAsync(string blockId, CancellationToken cancellationToken = default)
    {
        await WrapAsync(async () =>
        {
            var guid = Guid.Parse(blockId);
            await _apiClient.V1.Blocks[guid].DeleteAsync(cancellationToken: cancellationToken);
            return true;
        });
    }

    public async Task<PaginatedList<Block>> GetBlockChildrenAsync(string blockId, BlockChildrenQuery? query = null, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(blockId);
            Action<RequestConfiguration<Generated.V1.Blocks.Item.Children.ChildrenRequestBuilder.ChildrenRequestBuilderGetQueryParameters>>? config = null;
            if (query is not null)
            {
                config = q =>
                {
                    if (query.StartCursor is not null) q.QueryParameters.StartCursor = query.StartCursor;
                    if (query.PageSize is not null) q.QueryParameters.PageSize = query.PageSize;
                };
            }

            var result = await _apiClient.V1.Blocks[guid].Children.GetAsync(config, cancellationToken);
            return BlockMapper.MapChildrenResponse(result);
        });
    }

    public async Task<AppendBlockChildrenResult> AppendBlockChildrenAsync(string blockId, AppendBlockChildrenRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(blockId);
            var body = new Gen.AppendBlockChildrenRequest();
            var result = await _apiClient.V1.Blocks[guid].Children.PatchAsync(body, cancellationToken: cancellationToken);
            return new AppendBlockChildrenResult();
        });
    }

    public async Task<Database> CreateDatabaseAsync(CreateDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var body = new Gen.CreateDatabaseRequest();
            var result = await _apiClient.V1.Databases.PostAsync(body, cancellationToken: cancellationToken);
            return DatabaseMapper.Map(result ?? throw new InvalidOperationException("CreateDatabase returned null"));
        });
    }

    public async Task<Database> GetDatabaseAsync(string databaseId, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(databaseId);
            var result = await _apiClient.V1.Databases[guid].GetAsync(cancellationToken: cancellationToken);
            return DatabaseMapper.Map(result ?? throw new InvalidOperationException("GetDatabase returned null"));
        });
    }

    public async Task<Database> UpdateDatabaseAsync(string databaseId, UpdateDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(databaseId);
            var body = new Gen.UpdateDatabaseRequest();
            var result = await _apiClient.V1.Databases[guid].PatchAsync(body, cancellationToken: cancellationToken);
            return DatabaseMapper.Map(result ?? throw new InvalidOperationException("UpdateDatabase returned null"));
        });
    }

    public async Task<QueryDatabaseResult> QueryDatabaseAsync(string databaseId, QueryDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(databaseId);
            var body = new Gen.QueryDatabaseRequest
            {
                StartCursor = request.StartCursor,
                PageSize = request.PageSize
            };
            var result = await _apiClient.V1.Databases[guid].Query.PostAsync(body, cancellationToken: cancellationToken);
            return DatabaseMapper.MapQueryResponse(result);
        });
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var body = new Gen.SearchRequest { Query = request.Query };
            var result = await _apiClient.V1.Pages.Search.PostAsync(body, cancellationToken: cancellationToken);
            return SearchMapper.Map(result);
        });
    }

    public async Task<PageSearchResults> SearchPagesAsync(PageSearchRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var body = new Gen.V1SearchRequest
            {
                Query = request.Query,
                StartCursor = request.StartCursor,
                PageSize = request.PageSize
            };
            var result = await _apiClient.V1.Search.PostAsync(body, cancellationToken: cancellationToken);
            return SearchMapper.Map(result);
        });
    }

    private async Task<T> WrapAsync<T>(Func<Task<T>> action, [CallerMemberName] string methodName = "")
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            sw.Stop();
            BotBuildinClientLog.ApiCallCompleted(_logger, methodName, "success");
            BuildoutMeter.ApiCallsTotal.Add(1, new TagList { { "method", methodName }, { "outcome", "success" } });
            BuildoutMeter.ApiCallDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "method", methodName }, { "outcome", "success" } });
            return result;
        }
        catch (BuildinApiException ex)
        {
            sw.Stop();
            var errorType = ex.Error switch
            {
                ApiError => "api",
                TransportError => "transport",
                _ => "unknown"
            };
            LogAndRecordApiFailure(methodName, sw, errorType);
            throw;
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            LogAndRecordApiFailure(methodName, sw, "transport");
            throw new BuildinApiException(new TransportError(ex), ex);
        }
        catch (KiotaApiException ex)
        {
            sw.Stop();
            var error = ex as Gen.Error;
            var buildinError = new ApiError(
                error?.Status ?? ex.ResponseStatusCode,
                error?.Code is not null ? MappingHelpers.GetEnumValue(error.Code) : null,
                error?.Message ?? ex.Message,
                string.Empty);
            LogAndRecordApiFailure(methodName, sw, "api");
            throw new BuildinApiException(buildinError, ex);
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (ex.InnerException is KiotaApiException apiEx)
            {
                var error = apiEx as Gen.Error;
                var buildinError = new ApiError(
                    error?.Status ?? apiEx.ResponseStatusCode,
                    error?.Code is not null ? MappingHelpers.GetEnumValue(error.Code) : null,
                    error?.Message ?? apiEx.Message,
                    string.Empty);
                LogAndRecordApiFailure(methodName, sw, "api");
                throw new BuildinApiException(buildinError, apiEx);
            }
            if (ex.InnerException is HttpRequestException httpEx)
            {
                LogAndRecordApiFailure(methodName, sw, "transport");
                throw new BuildinApiException(new TransportError(httpEx), httpEx);
            }
            LogAndRecordApiFailure(methodName, sw, "unknown");
            throw new BuildinApiException(new UnknownError(0, string.Empty), ex);
        }
    }

    private void LogAndRecordApiFailure(string methodName, Stopwatch sw, string errorType)
    {
        BotBuildinClientLog.ApiCallFailed(_logger, methodName, "failure", errorType);
        var tags = new TagList { { "method", methodName }, { "outcome", "failure" }, { "error_type", errorType } };
        BuildoutMeter.ApiCallsTotal.Add(1, tags);
        BuildoutMeter.ApiCallDuration.Record(sw.Elapsed.TotalSeconds, tags);
    }

}
