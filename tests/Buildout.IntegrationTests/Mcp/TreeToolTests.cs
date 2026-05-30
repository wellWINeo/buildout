using System.IO.Pipelines;
using System.Text.Json;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.PageTree;
using Buildout.Core.PageTree.Errors;
using Buildout.Core.PageTree.Rendering;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

public sealed class TreeToolTests : IAsyncLifetime
{
    private readonly IPageTreeService _service = Substitute.For<IPageTreeService>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var asciiRenderer = new AsciiTreeRenderer();
        var jsonRenderer = new JsonTreeRenderer();
        var rendererDict = new Dictionary<TreeFormat, ITreeRenderer>
        {
            [TreeFormat.Ascii] = asciiRenderer,
            [TreeFormat.Json] = jsonRenderer,
        };

        var services = new ServiceCollection();
        services.AddSingleton(_service);
        services.AddSingleton<IReadOnlyDictionary<TreeFormat, ITreeRenderer>>(rendererDict);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithTools<TreeToolHandler>();

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

    private static TreeNode MakeTree() => new("Root", "https://r.com", [
        new TreeNode("Child A", "https://ca.com", []),
        new TreeNode("Child B", "https://cb.com", []),
    ]);

    [Fact]
    public async Task ServerAdvertisesTreeTool()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("tree", tools[0].Name);
    }

    [Fact]
    public async Task ToolDescription_ContainsAsciiAndJson()
    {
        var tools = await _client.ListToolsAsync();
        var desc = tools[0].Description;

        Assert.Contains("ascii", desc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("json", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToolDescription_ContainsDepthRange()
    {
        var tools = await _client.ListToolsAsync();
        var desc = tools[0].Description;

        Assert.Contains("1", desc);
        Assert.Contains("7", desc);
    }

    [Fact]
    public async Task AsciiFormat_ReturnsExpectedTree()
    {
        _service.BuildAsync("page-1", 3, Arg.Any<CancellationToken>()).Returns(MakeTree());

        var result = await _client.CallToolAsync("tree", new Dictionary<string, object?>
        {
            ["page_id"] = "page-1",
            ["format"] = "ascii",
            ["depth"] = 3,
        });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.Contains("Root", text);
        Assert.Contains("Child A", text);
        Assert.Contains("Child B", text);
        Assert.Contains("├──", text);
        Assert.Contains("└──", text);
    }

    [Fact]
    public async Task JsonFormat_ParsesCorrectly()
    {
        _service.BuildAsync("page-1", 3, Arg.Any<CancellationToken>()).Returns(MakeTree());

        var result = await _client.CallToolAsync("tree", new Dictionary<string, object?>
        {
            ["page_id"] = "page-1",
            ["format"] = "json",
            ["depth"] = 3,
        });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        var doc = JsonDocument.Parse(text);

        Assert.Equal("Root", doc.RootElement.GetProperty("name").GetString());
        var children = doc.RootElement.GetProperty("children");
        Assert.Equal(2, children.GetArrayLength());
    }

    [Fact]
    public async Task JsonLeafNodes_HaveEmptyChildrenArray()
    {
        _service.BuildAsync("page-1", 3, Arg.Any<CancellationToken>()).Returns(MakeTree());

        var result = await _client.CallToolAsync("tree", new Dictionary<string, object?>
        {
            ["page_id"] = "page-1",
            ["format"] = "json",
            ["depth"] = 3,
        });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        var doc = JsonDocument.Parse(text);

        var firstChild = doc.RootElement.GetProperty("children").EnumerateArray().First();
        Assert.True(firstChild.TryGetProperty("children", out var childrenEl));
        Assert.Equal(JsonValueKind.Array, childrenEl.ValueKind);
        Assert.Empty(childrenEl.EnumerateArray());
    }

    [Fact]
    public async Task OutOfRangeDepth_ThrowsInvalidParams()
    {
        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("tree", new Dictionary<string, object?>
            {
                ["page_id"] = "page-1",
                ["format"] = "ascii",
                ["depth"] = 0,
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
        Assert.Contains("depth", ex.Message);
    }

    [Fact]
    public async Task InvalidFormat_ThrowsInvalidParams()
    {
        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("tree", new Dictionary<string, object?>
            {
                ["page_id"] = "page-1",
                ["format"] = "xml",
                ["depth"] = 3,
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task RootNotFound_ThrowsResourceNotFound()
    {
        _service.BuildAsync("missing", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new TreeRootNotFoundException("missing", new BuildinApiException(new ApiError(404, "not_found", "Not found", null))));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("tree", new Dictionary<string, object?>
            {
                ["page_id"] = "missing",
                ["format"] = "ascii",
                ["depth"] = 3,
            }));

        Assert.Equal(McpErrorCode.ResourceNotFound, ex.ErrorCode);
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task CycleDetected_ThrowsInternalError()
    {
        _service.BuildAsync("cycle-page", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new TreeCycleDetectedException("cycle-node"));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("tree", new Dictionary<string, object?>
            {
                ["page_id"] = "cycle-page",
                ["format"] = "ascii",
                ["depth"] = 3,
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }

    [Fact]
    public async Task AuthFailure_ThrowsInternalError()
    {
        _service.BuildAsync("page-1", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new ApiError(401, "unauthorized", "Unauthorized", null)));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("tree", new Dictionary<string, object?>
            {
                ["page_id"] = "page-1",
                ["format"] = "ascii",
                ["depth"] = 3,
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }

    [Fact]
    public async Task TransportFailure_ThrowsInternalError()
    {
        _service.BuildAsync("page-1", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new BuildinApiException(new TransportError(new HttpRequestException("Connection refused"))));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("tree", new Dictionary<string, object?>
            {
                ["page_id"] = "page-1",
                ["format"] = "ascii",
                ["depth"] = 3,
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }
}
