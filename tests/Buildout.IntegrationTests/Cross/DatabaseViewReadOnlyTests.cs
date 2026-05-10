using Buildout.Core.Buildin;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

[Collection("BuildinWireMock")]
public sealed class DatabaseViewReadOnlyTests
{
    private readonly BuildinWireMockFixture _fixture;
    private const string DatabaseId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    public DatabaseViewReadOnlyTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private void SetupReadOnlyStubs()
    {
        _fixture.Server.Reset();

        _fixture.Server
            .Given(Request.Create()
                .WithPath($"/v1/databases/{DatabaseId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = DatabaseId,
                    created_time = "2025-01-15T10:30:00Z",
                    last_edited_time = "2025-01-16T14:00:00Z",
                    title = new[]
                    {
                        new { type = "text", plain_text = "Readonly Check" }
                    },
                    properties = new
                    {
                        Name = new { type = "title", title = new { } }
                    }
                }));

        _fixture.Server
            .Given(Request.Create()
                .WithPath($"/v1/databases/{DatabaseId}/query")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    results = new object[]
                    {
                        new
                        {
                            properties = new
                            {
                                Name = new
                                {
                                    type = "title",
                                    title = new[] { new { type = "text", plain_text = "Only Row" } }
                                }
                            }
                        }
                    },
                    has_more = false,
                    next_cursor = (string?)null
                }));

        _fixture.Server
            .Given(Request.Create()
                .WithPath(new RegexMatcher("^/v1/.*"))
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { status = 500, code = "unexpected_request", message = "Unexpected endpoint" }));
    }

    [Fact]
    public async Task ViewDatabase_OnlyHitsReadEndpoints()
    {
        SetupReadOnlyStubs();

        var client = _fixture.CreateClient();
        var renderer = BuildRenderer(client);

        await renderer.RenderAsync(
            new DatabaseViewRequest(DatabaseId, DatabaseViewStyle.Table, null, null));

        var logEntries = _fixture.Server.LogEntries;
        Assert.All(logEntries, entry =>
        {
            var method = entry.RequestMessage?.Method ?? "";
            var path = entry.RequestMessage?.Path ?? "";
            var unexpected = path != $"/v1/databases/{DatabaseId}" &&
                             path != $"/v1/databases/{DatabaseId}/query";
            Assert.False(unexpected,
                $"Unexpected {method} request to {path} — only GET database and POST query are allowed");
        });
    }

    [Fact]
    public async Task ViewInline_OnlyHitsReadEndpoints()
    {
        SetupReadOnlyStubs();

        var client = _fixture.CreateClient();
        var renderer = BuildRenderer(client);

        await renderer.RenderInlineAsync(DatabaseId);

        var logEntries = _fixture.Server.LogEntries;
        Assert.All(logEntries, entry =>
        {
            var path = entry.RequestMessage?.Path ?? "";
            var unexpected = path != $"/v1/databases/{DatabaseId}" &&
                             path != $"/v1/databases/{DatabaseId}/query";
            Assert.False(unexpected,
                $"Unexpected request to {path} — only GET database and POST query are allowed");
        });
    }

    [Fact]
    public async Task ViewDatabase_NoWriteMethodsUsed()
    {
        SetupReadOnlyStubs();

        var client = _fixture.CreateClient();
        var renderer = BuildRenderer(client);

        await renderer.RenderAsync(
            new DatabaseViewRequest(DatabaseId, DatabaseViewStyle.Table, null, null));

        var logEntries = _fixture.Server.LogEntries;
        Assert.All(logEntries, entry =>
        {
            var method = entry.RequestMessage?.Method ?? "";
            var path = entry.RequestMessage?.Path ?? "";
            Assert.False(method == "POST" && path != $"/v1/databases/{DatabaseId}/query",
                $"Unexpected POST to {path}");
            Assert.False(method is "PUT" or "PATCH" or "DELETE",
                $"Unexpected write method {method} to {path}");
        });
    }

    private static IDatabaseViewRenderer BuildRenderer(IBuildinClient client)
    {
        var services = new ServiceCollection();
        services.AddSingleton(client);
        services.AddSingleton<IPropertyValueFormatter, PropertyValueFormatter>();
        services.AddSingleton<CellBudget>(static _ => new CellBudget(24, "…"));
        services.AddSingleton<IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>>(
            static _ => new Dictionary<DatabaseViewStyle, IDatabaseViewStyle>
            {
                [DatabaseViewStyle.Table] = new TableViewStyle()
            });
        services.AddSingleton<IDatabaseViewRenderer, DatabaseViewRenderer>();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDatabaseViewRenderer>();
    }
}
