using System.Text.Json;
using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Gen = Buildout.Core.Buildin.Generated.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using NSubstitute;
using Xunit;

namespace Buildout.UnitTests.Buildin;

public sealed class BotBuildinClientTests
{
    private readonly IRequestAdapter _adapter;
    private readonly IOptions<BuildinClientOptions> _options;
    private readonly ILogger<BotBuildinClient> _logger;
    private readonly BotBuildinClient _client;

    public BotBuildinClientTests()
    {
        _adapter = Substitute.For<IRequestAdapter>();
        _adapter.BaseUrl.Returns("https://api.buildin.ai");
        _options = Options.Create(new BuildinClientOptions());
        _logger = Substitute.For<ILogger<BotBuildinClient>>();
        _client = new BotBuildinClient(_adapter, _options, _logger);
    }

    [Fact]
    public async Task GetMeAsync_MapsGeneratedUserMe_ToHandWrittenUserMe()
    {
        var generated = new Gen.UserMe
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test User",
            AvatarUrl = "https://example.com/avatar.png",
            Type = "person",
            Person = new Gen.UserMe_person { Email = "test@example.com" }
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.UserMe>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.UserMe?>(generated));

        var result = await _client.GetMeAsync();

        Assert.NotNull(result);
        Assert.Equal("11111111-1111-1111-1111-111111111111", result.Id);
        Assert.Equal("Test User", result.Name);
        Assert.Equal("https://example.com/avatar.png", result.AvatarUrl);
        Assert.Equal("person", result.Type);
        Assert.Equal("test@example.com", result.Email);
    }

    [Fact]
    public async Task GetPageAsync_MapsGeneratedPage_ToHandWrittenPage()
    {
        var pageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var createdTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var generated = new Gen.Page
        {
            Id = pageId,
            CreatedTime = createdTime,
            Archived = false,
            Url = "https://api.buildin.ai/pages/22222222",
            Parent = new Gen.Parent
            {
                ParentPageId = new Gen.ParentPageId { PageId = Guid.Parse("33333333-3333-3333-3333-333333333333"), Type = "page_id" }
            },
            Icon = new Gen.Icon
            {
                IconEmoji = new Gen.IconEmoji { Emoji = "📝", Type = "emoji" }
            },
            Cover = new Gen.Cover
            {
                External = new Gen.Cover_external { Url = "https://example.com/cover.png" },
                Type = "external"
            }
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.Page>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.Page?>(generated));

        var result = await _client.GetPageAsync("22222222-2222-2222-2222-222222222222");

        Assert.NotNull(result);
        Assert.Equal("22222222-2222-2222-2222-222222222222", result.Id);
        Assert.Equal(createdTime, result.CreatedAt);
        Assert.False(result.Archived);
        Assert.Equal("https://api.buildin.ai/pages/22222222", result.Url);
        Assert.NotNull(result.Parent);
        Assert.IsType<ParentPage>(result.Parent);
        Assert.Equal("page_id", result.Parent!.Type);
        Assert.NotNull(result.Icon);
        Assert.IsType<IconEmoji>(result.Icon);
        Assert.Equal("📝", ((IconEmoji)result.Icon!).Emoji);
        Assert.Equal("https://example.com/cover.png", result.Cover);
    }

    [Fact]
    public async Task CreatePageAsync_MapsCreatePageResponse_ToHandWrittenPage()
    {
        var generatedResponse = new Gen.CreatePageResponse
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Archived = false,
            CreatedAt = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero),
            Url = "https://api.buildin.ai/pages/44444444"
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.CreatePageResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.CreatePageResponse?>(generatedResponse));

        var request = new CreatePageRequest
        {
            Parent = new ParentPage("33333333-3333-3333-3333-333333333333"),
            Properties = new Dictionary<string, PropertyValue>
            {
                ["title"] = new TitlePropertyValue { Title = [new RichText { Type = "text", Content = "Test" }] }
            }
        };

        var result = await _client.CreatePageAsync(request);

        Assert.NotNull(result);
        Assert.Equal("44444444-4444-4444-4444-444444444444", result.Id);
        Assert.False(result.Archived);
        Assert.Equal("https://api.buildin.ai/pages/44444444", result.Url);
    }

    [Fact]
    public async Task SearchAsync_MapsSearchResult_ToSearchResults()
    {
        var generatedResult = new Gen.SearchResult
        {
            Success = true,
            Data = [new Gen.SearchResult_data()]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.SearchResult>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.SearchResult?>(generatedResult));

        var request = new SearchRequest { Query = "test" };
        var result = await _client.SearchAsync(request);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Results);
    }

    [Fact]
    public async Task GetBlockChildrenAsync_MapsPaginatedResponse_WithPagination()
    {
        var generatedResponse = new Gen.GetBlockChildrenResponse();
        generatedResponse.HasMore = true;
        generatedResponse.NextCursor = "cursor123";

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.GetBlockChildrenResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.GetBlockChildrenResponse?>(generatedResponse));

        var query = new BlockChildrenQuery { StartCursor = "cursor0", PageSize = 2 };
        var result = await _client.GetBlockChildrenAsync("55555555-5555-5555-5555-555555555551", query);

        Assert.NotNull(result);
        Assert.True(result.HasMore);
        Assert.Equal("cursor123", result.NextCursor);
    }

    [Fact]
    public async Task GetBlockAsync_MapsGeneratedBlock_ToParagraphBlock()
    {
        var generated = new Gen.Block
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Type = Gen.Block_type.Paragraph,
            HasChildren = true,
            Data = new Gen.BlockData
            {
                RichText = [new Gen.RichTextItem { PlainText = "Hello world", Type = Gen.RichTextItem_type.Text }]
            }
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.Block>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.Block?>(generated));

        var result = await _client.GetBlockAsync("66666666-6666-6666-6666-666666666666");

        Assert.NotNull(result);
        Assert.IsType<ParagraphBlock>(result);
        Assert.Equal("66666666-6666-6666-6666-666666666666", result.Id);
        Assert.Equal("paragraph", result.Type);
        Assert.True(result.HasChildren);
    }

    [Fact]
    public async Task DeleteBlockAsync_CompletesWithoutError()
    {
        var generatedResponse = new Gen.DeleteBlockResponse
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            Deleted = true
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.DeleteBlockResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.DeleteBlockResponse?>(generatedResponse));

        await _client.DeleteBlockAsync("77777777-7777-7777-7777-777777777777");
    }

    [Fact]
    public async Task GetDatabaseAsync_MapsGeneratedDatabase_ToHandWrittenDatabase()
    {
        var generated = new Gen.Database
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            Archived = false,
            IsInline = false,
            Url = "https://api.buildin.ai/databases/88888888",
            Title = [new Gen.RichTextItem { PlainText = "My Database", Type = Gen.RichTextItem_type.Text }]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.Database>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.Database?>(generated));

        var result = await _client.GetDatabaseAsync("88888888-8888-8888-8888-888888888888");

        Assert.NotNull(result);
        Assert.Equal("88888888-8888-8888-8888-888888888888", result.Id);
        Assert.False(result.Archived);
        Assert.Equal("https://api.buildin.ai/databases/88888888", result.Url);
        Assert.NotNull(result.Title);
    }

    [Fact]
    public async Task GetBlockChildrenAsync_MapsBlocks()
    {
        var json = """
        [
            {
                "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "type": "paragraph",
                "has_children": false,
                "data": {
                    "rich_text": [{"plain_text": "Hello world", "type": "text"}]
                }
            },
            {
                "id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                "type": "heading_1",
                "has_children": true,
                "data": {
                    "rich_text": [{"plain_text": "Heading", "type": "text"}]
                }
            }
        ]
        """;
        var doc = JsonDocument.Parse(json);
        var parseNode = new JsonParseNode(doc.RootElement);
        var resultsUntyped = parseNode.GetObjectValue(UntypedNode.CreateFromDiscriminatorValue);

        var response = new Gen.GetBlockChildrenResponse
        {
            HasMore = false,
            Results = resultsUntyped
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.GetBlockChildrenResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.GetBlockChildrenResponse?>(response));

        var result = await _client.GetBlockChildrenAsync("cccccccc-cccc-cccc-cccc-cccccccccccc");

        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);
        var para = Assert.IsType<ParagraphBlock>(result.Results[0]);
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", para.Id);
        Assert.Equal("paragraph", para.Type);
        var heading = Assert.IsType<Heading1Block>(result.Results[1]);
        Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", heading.Id);
        Assert.Equal("heading_1", heading.Type);
    }

    [Fact]
    public async Task GetPageAsync_PopulatesTitle()
    {
        var propJson = """
        {
            "Name": {
                "type": "title",
                "title": [{"plain_text": "My Page Title", "type": "text"}]
            },
            "Description": {
                "type": "rich_text",
                "rich_text": [{"plain_text": "other", "type": "text"}]
            }
        }
        """;
        var propDoc = JsonDocument.Parse(propJson);
        var propParseNode = new JsonParseNode(propDoc.RootElement);
        var properties = propParseNode.GetObjectValue(Gen.Page_properties.CreateFromDiscriminatorValue);

        var generated = new Gen.Page
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Archived = false,
            Url = "https://api.buildin.ai/pages/22222222",
            Properties = properties
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.Page>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.Page?>(generated));

        var result = await _client.GetPageAsync("22222222-2222-2222-2222-222222222222");

        Assert.NotNull(result);
        Assert.NotNull(result.Title);
        Assert.Single(result.Title);
        Assert.Equal("My Page Title", result.Title[0].Content);
    }

    [Fact]
    public async Task GetBlockAsync_MapsMentionRichText()
    {
        var pageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var generated = new Gen.Block
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Type = Gen.Block_type.Paragraph,
            HasChildren = false,
            Data = new Gen.BlockData
            {
                RichText =
                [
                    new Gen.RichTextItem
                    {
                        PlainText = "page ref",
                        Type = Gen.RichTextItem_type.Mention,
                        Mention = new Gen.RichTextItem_mention
                        {
                            Type = Gen.RichTextItem_mention_type.Page,
                            Page = new Gen.RichTextItem_mention_page { Id = pageId }
                        }
                    },
                    new Gen.RichTextItem
                    {
                        PlainText = "user ref",
                        Type = Gen.RichTextItem_type.Mention,
                        Mention = new Gen.RichTextItem_mention
                        {
                            Type = Gen.RichTextItem_mention_type.User,
                            User = new Gen.RichTextItem_mention_user { Id = userId }
                        }
                    },
                    new Gen.RichTextItem
                    {
                        PlainText = "date ref",
                        Type = Gen.RichTextItem_type.Mention,
                        Mention = new Gen.RichTextItem_mention
                        {
                            Type = Gen.RichTextItem_mention_type.Date,
                            Date = new Gen.RichTextItem_mention_date { Start = "2025-01-15", End = "2025-01-16" }
                        }
                    },
                    new Gen.RichTextItem
                    {
                        PlainText = "plain text",
                        Type = Gen.RichTextItem_type.Text
                    }
                ]
            }
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.Block>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.Block?>(generated));

        var result = await _client.GetBlockAsync("66666666-6666-6666-6666-666666666666");

        Assert.NotNull(result);
        var paragraph = Assert.IsType<ParagraphBlock>(result);
        Assert.NotNull(paragraph.RichTextContent);
        Assert.Equal(4, paragraph.RichTextContent.Count);

        var pageMention = Assert.IsType<PageMention>(paragraph.RichTextContent[0].Mention);
        Assert.Equal("11111111-1111-1111-1111-111111111111", pageMention.PageId);

        var userMention = Assert.IsType<UserMention>(paragraph.RichTextContent[1].Mention);
        Assert.Equal("22222222-2222-2222-2222-222222222222", userMention.UserId);

        var dateMention = Assert.IsType<DateMention>(paragraph.RichTextContent[2].Mention);
        Assert.Equal("2025-01-15", dateMention.Start);
        Assert.Equal("2025-01-16", dateMention.End);

        Assert.Null(paragraph.RichTextContent[3].Mention);
    }

    [Fact]
    public async Task SearchPagesAsync_MapsTitleFromProperties()
    {
        var response = new Gen.V1SearchResponse
        {
            Results =
            [
                new Gen.V1SearchPageResult
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    CreatedTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    LastEditedTime = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
                    Archived = false,
                    Object = "page",
                    Properties = new Gen.V1SearchPageResult_properties
                    {
                        Title = new Gen.V1SearchPageResult_properties_title
                        {
                            Title = [new Gen.RichTextItem { PlainText = "Hello Search", Type = Gen.RichTextItem_type.Text }]
                        }
                    }
                }
            ]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.V1SearchResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.V1SearchResponse?>(response));

        var result = await _client.SearchPagesAsync(new PageSearchRequest { Query = "test" });

        Assert.NotNull(result);
        var page = Assert.Single(result.Results);
        Assert.NotNull(page.Title);
        Assert.Single(page.Title);
        Assert.Equal("Hello Search", page.Title[0].Content);
    }

    [Fact]
    public async Task SearchPagesAsync_MapsParentPage()
    {
        var parent = new Gen.V1SearchPageResult.V1SearchPageResult_parent
        {
            ParentPageId = new Gen.ParentPageId { PageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Type = "page_id" }
        };

        var response = new Gen.V1SearchResponse
        {
            Results =
            [
                new Gen.V1SearchPageResult
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Archived = false,
                    Parent = parent
                }
            ]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.V1SearchResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.V1SearchResponse?>(response));

        var result = await _client.SearchPagesAsync(new PageSearchRequest { Query = "test" });

        Assert.NotNull(result);
        var page = Assert.Single(result.Results);
        Assert.NotNull(page.Parent);
        Assert.IsType<ParentPage>(page.Parent);
        Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", ((ParentPage)page.Parent!).Id);
    }

    [Fact]
    public async Task SearchPagesAsync_MapsObjectType()
    {
        var response = new Gen.V1SearchResponse
        {
            Results =
            [
                new Gen.V1SearchPageResult
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Archived = false,
                    Object = "page"
                }
            ]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.V1SearchResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.V1SearchResponse?>(response));

        var result = await _client.SearchPagesAsync(new PageSearchRequest { Query = "test" });

        Assert.NotNull(result);
        var page = Assert.Single(result.Results);
        Assert.Equal("page", page.ObjectType);
    }

    [Fact]
    public async Task SearchPagesAsync_NullOrEmptyTitle_NullOrEmptyResult()
    {
        var response = new Gen.V1SearchResponse
        {
            Results =
            [
                new Gen.V1SearchPageResult
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Archived = false,
                    Properties = new Gen.V1SearchPageResult_properties
                    {
                        Title = new Gen.V1SearchPageResult_properties_title { Title = [] }
                    }
                },
                new Gen.V1SearchPageResult
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Archived = false,
                    Properties = new Gen.V1SearchPageResult_properties()
                }
            ]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.V1SearchResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.V1SearchResponse?>(response));

        var result = await _client.SearchPagesAsync(new PageSearchRequest { Query = "test" });

        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);
        Assert.NotNull(result.Results[0].Title);
        Assert.Empty(result.Results[0].Title!);
        Assert.Null(result.Results[1].Title);
    }

    [Fact]
    public async Task SearchPagesAsync_MapsParentDatabase()
    {
        var parent = new Gen.V1SearchPageResult.V1SearchPageResult_parent
        {
            ParentDatabaseId = new Gen.ParentDatabaseId { DatabaseId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Type = "database_id" }
        };

        var response = new Gen.V1SearchResponse
        {
            Results =
            [
                new Gen.V1SearchPageResult
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Archived = false,
                    Parent = parent
                }
            ]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.V1SearchResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.V1SearchResponse?>(response));

        var result = await _client.SearchPagesAsync(new PageSearchRequest { Query = "test" });

        Assert.NotNull(result);
        var page = Assert.Single(result.Results);
        Assert.NotNull(page.Parent);
        Assert.IsType<ParentDatabase>(page.Parent);
        Assert.Equal("cccccccc-cccc-cccc-cccc-cccccccccccc", ((ParentDatabase)page.Parent!).Id);
    }

    [Fact]
    public async Task SearchPagesAsync_MapsExistingFields()
    {
        var created = new DateTimeOffset(2025, 3, 10, 8, 0, 0, TimeSpan.Zero);
        var edited = new DateTimeOffset(2025, 3, 11, 9, 0, 0, TimeSpan.Zero);

        var response = new Gen.V1SearchResponse
        {
            HasMore = true,
            NextCursor = "abc",
            Results =
            [
                new Gen.V1SearchPageResult
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    CreatedTime = created,
                    LastEditedTime = edited,
                    Archived = true
                }
            ]
        };

        _adapter.SendAsync(
                Arg.Any<RequestInformation>(),
                Arg.Any<ParsableFactory<Gen.V1SearchResponse>>(),
                Arg.Any<Dictionary<string, ParsableFactory<IParsable>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Gen.V1SearchResponse?>(response));

        var result = await _client.SearchPagesAsync(new PageSearchRequest { Query = "test" });

        Assert.NotNull(result);
        Assert.True(result.HasMore);
        Assert.Equal("abc", result.NextCursor);
        var page = Assert.Single(result.Results);
        Assert.Equal("dddddddd-dddd-dddd-dddd-dddddddddddd", page.Id);
        Assert.Equal(created, page.CreatedAt);
        Assert.Equal(edited, page.LastEditedAt);
        Assert.True(page.Archived);
    }
}
