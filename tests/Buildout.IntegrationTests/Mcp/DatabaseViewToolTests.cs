using System.IO.Pipelines;
using Buildout.Core.Buildin;
using Buildout.Core.DatabaseViews;
using Buildout.Core.DatabaseViews.Properties;
using Buildout.Core.DatabaseViews.Rendering;
using Buildout.Core.DatabaseViews.Styles;
using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Buildout.Mcp.Tools;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

[Collection("BuildinWireMock")]
public sealed class DatabaseViewToolTests : IAsyncLifetime
{
    private readonly BuildinWireMockFixture _fixture;
    private const string DatabaseId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public DatabaseViewToolTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    public async ValueTask InitializeAsync()
    {
        var builtinClient = _fixture.CreateClient();

        var services = new ServiceCollection();
        services.AddSingleton<IBuildinClient>(builtinClient);
        services.AddSingleton<CellBudget>(static _ => new CellBudget(24, "…"));
        services.AddSingleton<IPropertyValueFormatter, PropertyValueFormatter>();
        services.AddSingleton<IReadOnlyDictionary<DatabaseViewStyle, IDatabaseViewStyle>>(
            static _ => new Dictionary<DatabaseViewStyle, IDatabaseViewStyle>
            {
                [DatabaseViewStyle.Table] = new TableViewStyle(),
                [DatabaseViewStyle.Board] = new BoardViewStyle(),
                [DatabaseViewStyle.Gallery] = new GalleryViewStyle(),
                [DatabaseViewStyle.List] = new ListViewStyle(),
                [DatabaseViewStyle.Calendar] = new CalendarViewStyle(),
                [DatabaseViewStyle.Timeline] = new TimelineViewStyle(),
            });
        services.AddSingleton<IDatabaseViewRenderer, DatabaseViewRenderer>();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));

        services.AddMcpServer().WithTools<DatabaseViewToolHandler>();

        _sp = services.BuildServiceProvider();

        var options = _sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
        _c2s = new Pipe();
        _s2c = new Pipe();

        _server = McpServer.Create(
            new StreamServerTransport(_c2s.Reader.AsStream(), _s2c.Writer.AsStream()),
            options,
            _sp.GetRequiredService<ILoggerFactory>(),
            _sp);

        _ = _server.RunAsync();

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(_c2s.Writer.AsStream(), _s2c.Reader.AsStream()),
            new McpClientOptions(),
            _sp.GetRequiredService<ILoggerFactory>());
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _server.DisposeAsync();
        _c2s.Writer.Complete();
        _c2s.Reader.Complete();
        _s2c.Writer.Complete();
        _s2c.Reader.Complete();
        await _sp.DisposeAsync();
    }

    private void SetupTableFixture()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            id = DatabaseId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            title = new[] { new { type = "text", plain_text = "Tool DB" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Status = new { type = "select", select = new { options = new[] { new { name = "Active" } } } }
            }
        });

        BuildinStubs.RegisterQueryDatabase(_fixture.Server, DatabaseId, new
        {
            results = new object[]
            {
                new
                {
                    properties = new
                    {
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Row A" } } },
                        Status = new { type = "select", select = new { name = "Active" } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    [Fact]
    public async Task ServerAdvertisesDatabaseViewTool()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Contains(tools, t => t.Name == "database_view");
    }

    [Fact]
    public async Task TableStyle_ReturnsRenderedOutput()
    {
        SetupTableFixture();

        var result = await _client.CallToolAsync("database_view",
            new Dictionary<string, object?> { ["database_id"] = DatabaseId });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.StartsWith("# Tool DB — table view", text);
    }

    [Fact]
    public async Task ListStyle_ReturnsRenderedOutput()
    {
        SetupTableFixture();

        var result = await _client.CallToolAsync("database_view",
            new Dictionary<string, object?>
            {
                ["database_id"] = DatabaseId,
                ["style"] = "list"
            });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.StartsWith("# Tool DB — list view", text);
    }

    [Fact]
    public async Task UnknownStyle_ThrowsInvalidParams()
    {
        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("database_view",
                new Dictionary<string, object?>
                {
                    ["database_id"] = DatabaseId,
                    ["style"] = "nonsense"
                }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task MissingDatabaseId_ThrowsInvalidParams()
    {
        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("database_view",
                new Dictionary<string, object?> { ["database_id"] = "" }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task NotFound_ThrowsResourceNotFound()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            status = 404,
            code = "not_found",
            message = "Not found",
            @object = "error"
        }, statusCode: 404);

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("database_view",
                new Dictionary<string, object?> { ["database_id"] = DatabaseId }));

        Assert.Equal(McpErrorCode.ResourceNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task AuthFailure_ThrowsInternalError()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DatabaseId, new
        {
            status = 401,
            code = "unauthorized",
            message = "Unauthorized",
            @object = "error"
        }, statusCode: 401);

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("database_view",
                new Dictionary<string, object?> { ["database_id"] = DatabaseId }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }
}
