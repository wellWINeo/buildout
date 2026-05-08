using System.Text.Json;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Serialization.Json;
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
            return MapUserMe(result ?? throw new InvalidOperationException("GetMe returned null"));
        });
    }

    public async Task<Page> GetPageAsync(string pageId, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(pageId);
            var result = await _apiClient.V1.Pages[guid].GetAsync(cancellationToken: cancellationToken);
            return MapPage(result ?? throw new InvalidOperationException("GetPage returned null"));
        });
    }

    public async Task<Page> CreatePageAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var body = new Gen.CreatePageRequest();
            var result = await _apiClient.V1.Pages.PostAsync(body, cancellationToken: cancellationToken);
            return MapCreatePageResponse(result ?? throw new InvalidOperationException("CreatePage returned null"));
        });
    }

    public async Task<Page> UpdatePageAsync(string pageId, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(pageId);
            var body = new Gen.UpdatePageRequest();
            var result = await _apiClient.V1.Pages[guid].PatchAsync(body, cancellationToken: cancellationToken);
            return MapPage(result ?? throw new InvalidOperationException("UpdatePage returned null"));
        });
    }

    public async Task<Block> GetBlockAsync(string blockId, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(blockId);
            var result = await _apiClient.V1.Blocks[guid].GetAsync(cancellationToken: cancellationToken);
            return MapBlock(result ?? throw new InvalidOperationException("GetBlock returned null"));
        });
    }

    public async Task<Block> UpdateBlockAsync(string blockId, UpdateBlockRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(blockId);
            var body = new Gen.UpdateBlockRequest();
            var result = await _apiClient.V1.Blocks[guid].PatchAsync(body, cancellationToken: cancellationToken);
            return MapBlock(result ?? throw new InvalidOperationException("UpdateBlock returned null"));
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
            return MapBlockChildrenResponse(result);
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
            return MapDatabase(result ?? throw new InvalidOperationException("CreateDatabase returned null"));
        });
    }

    public async Task<Database> GetDatabaseAsync(string databaseId, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(databaseId);
            var result = await _apiClient.V1.Databases[guid].GetAsync(cancellationToken: cancellationToken);
            return MapDatabase(result ?? throw new InvalidOperationException("GetDatabase returned null"));
        });
    }

    public async Task<Database> UpdateDatabaseAsync(string databaseId, UpdateDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var guid = Guid.Parse(databaseId);
            var body = new Gen.UpdateDatabaseRequest();
            var result = await _apiClient.V1.Databases[guid].PatchAsync(body, cancellationToken: cancellationToken);
            return MapDatabase(result ?? throw new InvalidOperationException("UpdateDatabase returned null"));
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
            return new QueryDatabaseResult();
        });
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        return await WrapAsync(async () =>
        {
            var body = new Gen.SearchRequest { Query = request.Query };
            var result = await _apiClient.V1.Pages.Search.PostAsync(body, cancellationToken: cancellationToken);
            return MapSearchResult(result);
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
            return MapV1SearchResponse(result);
        });
    }

    private static async Task<T> WrapAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (BuildinApiException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new BuildinApiException(new TransportError(ex), ex);
        }
        catch (KiotaApiException ex)
        {
            var error = ex as Gen.Error;
            var buildinError = new ApiError(
                error?.Status ?? ex.ResponseStatusCode,
                error?.Code is not null ? GetEnumValue(error.Code) : null,
                error?.Message ?? ex.Message,
                string.Empty);
            throw new BuildinApiException(buildinError, ex);
        }
        catch (Exception ex)
        {
            if (ex.InnerException is KiotaApiException apiEx)
            {
                var error = apiEx as Gen.Error;
                var buildinError = new ApiError(
                    error?.Status ?? apiEx.ResponseStatusCode,
                    error?.Code is not null ? GetEnumValue(error.Code) : null,
                    error?.Message ?? apiEx.Message,
                    string.Empty);
                throw new BuildinApiException(buildinError, apiEx);
            }
            if (ex.InnerException is HttpRequestException httpEx)
            {
                throw new BuildinApiException(new TransportError(httpEx), httpEx);
            }
            throw new BuildinApiException(new UnknownError(0, string.Empty), ex);
        }
    }

    private static UserMe MapUserMe(Gen.UserMe gen)
    {
        return new UserMe
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            Name = gen.Name,
            AvatarUrl = gen.AvatarUrl,
            Type = gen.Type ?? "unknown",
            Email = gen.Person?.Email
        };
    }

    private static UserMe? MapUser(Gen.User? gen)
    {
        if (gen is null) return null;
        return new UserMe
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            Type = "unknown"
        };
    }

    private static Page MapPage(Gen.Page gen)
    {
        return new Page
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedTime,
            LastEditedAt = gen.LastEditedTime,
            CreatedBy = MapUser(gen.CreatedBy),
            LastEditedBy = MapUser(gen.LastEditedBy),
            Cover = gen.Cover?.External?.Url,
            Icon = MapIcon(gen.Icon),
            Parent = MapParent(gen.Parent),
            Archived = gen.Archived ?? false,
            Url = gen.Url,
            Title = ExtractTitle(gen.Properties)
        };
    }

    private static List<RichText>? ExtractTitle(Gen.Page_properties? properties)
    {
        if (properties is null) return null;

        using var writer = new JsonSerializationWriter();
        writer.WriteObjectValue(null, (IParsable)properties);
        using var stream = writer.GetSerializedContent();
        using var doc = JsonDocument.Parse(stream);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("type", out var typeEl)
                && typeEl.ValueKind == JsonValueKind.String
                && typeEl.GetString() == "title"
                && prop.Value.TryGetProperty("title", out var titleEl)
                && titleEl.ValueKind == JsonValueKind.Array)
            {
                var richTextItems = new List<RichText>();
                foreach (var item in titleEl.EnumerateArray())
                {
                    var node = new JsonParseNode(item);
                    var genItem = node.GetObjectValue(Gen.RichTextItem.CreateFromDiscriminatorValue);
                    if (genItem is not null)
                        richTextItems.Add(MapRichText(genItem));
                }
                return richTextItems;
            }
        }

        return null;
    }

    private static Page MapCreatePageResponse(Gen.CreatePageResponse gen)
    {
        return new Page
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedAt,
            LastEditedAt = gen.UpdatedAt,
            Archived = gen.Archived ?? false,
            Url = gen.Url
        };
    }

    private static string GetEnumValue<T>(T? value) where T : struct, Enum
    {
        if (value is null) return "unsupported";
        var name = value.Value.ToString();
        var field = typeof(T).GetField(name);
        if (field is null) return name;
        var attr = field.GetCustomAttributes(typeof(System.Runtime.Serialization.EnumMemberAttribute), false)
            .Cast<System.Runtime.Serialization.EnumMemberAttribute>()
            .FirstOrDefault();
        return attr?.Value ?? name;
    }

    private static Block MapBlock(Gen.Block gen)
    {
        var data = gen.Data;
        var richText = data?.RichText?.Select(MapRichText).ToList();

        var typeStr = GetEnumValue(gen.Type);

        Block block = typeStr switch
        {
            "paragraph" => new ParagraphBlock { RichTextContent = richText },
            "heading_1" => new Heading1Block { RichTextContent = richText },
            "heading_2" => new Heading2Block { RichTextContent = richText },
            "heading_3" => new Heading3Block { RichTextContent = richText },
            "bulleted_list_item" => new BulletedListItemBlock { RichTextContent = richText },
            "numbered_list_item" => new NumberedListItemBlock { RichTextContent = richText },
            "to_do" => new ToDoBlock { RichTextContent = richText, Checked = data?.Checked },
            "toggle" => new ToggleBlock { RichTextContent = richText },
            "code" => new CodeBlock { RichTextContent = richText, Language = data?.Language },
            "quote" => new QuoteBlock { RichTextContent = richText },
            "divider" => new DividerBlock(),
            "image" => new ImageBlock { Url = data?.Url, Caption = data?.Caption?.Select(MapRichText).ToList() },
            "embed" => new EmbedBlock { Url = data?.Url },
            "table" => new TableBlock { TableWidth = data?.TableWidth, HasColumnHeader = data?.HasColumnHeader, HasRowHeader = data?.HasRowHeader },
            "table_row" => new TableRowBlock(),
            "column_list" => new ColumnListBlock(),
            "column" => new ColumnBlock(),
            "child_page" => new ChildPageBlock { Title = data?.Title },
            "child_database" => new ChildDatabaseBlock { Title = data?.Title },
            "synced_block" => new SyncedBlock { SyncedFromId = data?.SyncedFrom?.BlockId?.ToString() },
            "link_preview" => new LinkPreviewBlock { Url = data?.Url },
            _ => new UnsupportedBlock()
        };

        return block with
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedTime,
            LastEditedAt = gen.LastEditedTime,
            HasChildren = gen.HasChildren ?? false,
            Parent = MapParent(gen.Parent)
        };
    }

    private static RichText MapRichText(Gen.RichTextItem gen)
    {
        return new RichText
        {
            Type = gen.Type?.ToString() ?? "text",
            Content = gen.PlainText ?? string.Empty,
            Href = gen.Href,
            Annotations = gen.Annotations is not null
                ? new Annotations
                {
                    Bold = gen.Annotations.Bold ?? false,
                    Italic = gen.Annotations.Italic ?? false,
                    Strikethrough = gen.Annotations.Strikethrough ?? false,
                    Underline = gen.Annotations.Underline ?? false,
                    Code = gen.Annotations.Code ?? false,
                    Color = gen.Annotations.Color?.ToString() ?? "default"
                }
                : null,
            Mention = MapMention(gen.Mention, gen.Type)
        };
    }

    private static Mention? MapMention(Gen.RichTextItem_mention? mention, Gen.RichTextItem_type? richTextType)
    {
        if (richTextType != Gen.RichTextItem_type.Mention || mention is null)
            return null;

        return mention.Type switch
        {
            Gen.RichTextItem_mention_type.Page => new PageMention
            {
                PageId = mention.Page?.Id?.ToString() ?? string.Empty
            },
            Gen.RichTextItem_mention_type.User => new UserMention
            {
                UserId = mention.User?.Id?.ToString() ?? string.Empty
            },
            Gen.RichTextItem_mention_type.Date => new DateMention
            {
                Start = mention.Date?.Start ?? string.Empty,
                End = mention.Date?.End
            },
            _ => null
        };
    }

    private static Parent? MapParent(Gen.Parent? gen)
    {
        if (gen is null) return null;
        if (gen.ParentDatabaseId is not null)
            return new ParentDatabase(gen.ParentDatabaseId.DatabaseId?.ToString() ?? string.Empty);
        if (gen.ParentPageId is not null)
            return new ParentPage(gen.ParentPageId.PageId?.ToString() ?? string.Empty);
        if (gen.ParentBlockId is not null)
            return new ParentBlock(gen.ParentBlockId.BlockId?.ToString() ?? string.Empty);
        if (gen.ParentSpaceId is not null)
            return new ParentWorkspace(gen.ParentSpaceId.Type);
        return null;
    }

    private static Icon? MapIcon(Gen.Icon? gen)
    {
        if (gen is null) return null;
        if (gen.IconEmoji is not null)
            return new IconEmoji(gen.IconEmoji.Emoji ?? string.Empty);
        if (gen.IconExternal is not null)
            return new IconExternal(gen.IconExternal.External?.Url ?? string.Empty);
        if (gen.IconFile is not null)
            return new IconFile(gen.IconFile.File?.Url ?? string.Empty);
        return null;
    }

    private static Database MapDatabase(Gen.Database gen)
    {
        return new Database
        {
            Id = gen.Id?.ToString() ?? string.Empty,
            CreatedAt = gen.CreatedTime,
            LastEditedAt = gen.LastEditedTime,
            CreatedBy = MapUser(gen.CreatedBy),
            LastEditedBy = MapUser(gen.LastEditedBy),
            Cover = gen.Cover?.External?.Url,
            Icon = MapIcon(gen.Icon),
            Parent = MapParent(gen.Parent),
            Title = gen.Title?.Select(MapRichText).ToList(),
            IsInline = gen.IsInline,
            Archived = gen.Archived ?? false,
            Url = gen.Url
        };
    }

    private static PaginatedList<Block> MapBlockChildrenResponse(Gen.GetBlockChildrenResponse? gen)
    {
        if (gen is null) return new PaginatedList<Block>();

        var blocks = new List<Block>();
        if (gen.Results is UntypedArray array)
        {
            foreach (var item in array.GetValue())
            {
                if (item is null) continue;

                using var writer = new JsonSerializationWriter();
                writer.WriteObjectValue(null, item);
                using var stream = writer.GetSerializedContent();
                using var doc = JsonDocument.Parse(stream);
                var node = new JsonParseNode(doc.RootElement);
                var genBlock = node.GetObjectValue(Gen.Block.CreateFromDiscriminatorValue);
                if (genBlock is not null)
                    blocks.Add(MapBlock(genBlock));
            }
        }

        return new PaginatedList<Block>
        {
            Results = blocks,
            HasMore = gen.HasMore ?? false,
            NextCursor = gen.NextCursor
        };
    }

    private static SearchResults MapSearchResult(Gen.SearchResult? gen)
    {
        if (gen is null) return new SearchResults();
        var results = new List<object>();
        if (gen.Data is not null) results.AddRange(gen.Data.Cast<object>());
        return new SearchResults { Results = results };
    }

    private static PageSearchResults MapV1SearchResponse(Gen.V1SearchResponse? gen)
    {
        if (gen is null) return new PageSearchResults();
        var pages = new List<Page>();
        if (gen.Results is not null)
        {
            foreach (var r in gen.Results)
            {
                pages.Add(new Page
                {
                    Id = r.Id?.ToString() ?? string.Empty,
                    CreatedAt = r.CreatedTime,
                    LastEditedAt = r.LastEditedTime,
                    Archived = r.Archived ?? false,
                    Title = r.Properties?.Title?.Title?.Select(MapRichText).ToList(),
                    Parent = MapSearchResultParent(r.Parent),
                    ObjectType = r.Object
                });
            }
        }
        return new PageSearchResults
        {
            Results = pages,
            HasMore = gen.HasMore ?? false,
            NextCursor = gen.NextCursor
        };
    }

    private static Parent? MapSearchResultParent(Gen.V1SearchPageResult.V1SearchPageResult_parent? gen)
    {
        if (gen is null) return null;
        if (gen.ParentDatabaseId is not null)
            return new ParentDatabase(gen.ParentDatabaseId.DatabaseId?.ToString() ?? string.Empty);
        if (gen.ParentPageId is not null)
            return new ParentPage(gen.ParentPageId.PageId?.ToString() ?? string.Empty);
        if (gen.ParentBlockId is not null)
            return new ParentBlock(gen.ParentBlockId.BlockId?.ToString() ?? string.Empty);
        if (gen.ParentSpaceId is not null)
            return new ParentWorkspace(gen.ParentSpaceId.Type);
        return null;
    }
}
