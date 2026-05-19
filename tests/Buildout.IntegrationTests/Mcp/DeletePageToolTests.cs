using System.IO.Pipelines;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.PageLifecycle;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

public sealed class DeletePageToolTests : IAsyncLifetime
{
    private readonly IPageLifecycle _lifecycle = Substitute.For<IPageLifecycle>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageLifecycle>(_lifecycle);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithTools<DeletePageToolHandler>();

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

    [Fact]
    public async Task ServerAdvertisesDeletePageTool()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("delete_page", tools[0].Name);
        Assert.NotNull(tools[0].Description);
    }

    [Fact]
    public async Task HappyPath_ReturnsResourceLinkAndTextBlock()
    {
        _lifecycle.DeleteAsync("page-abc", Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = "page-abc",
                Archived = true,
                Changed = true,
            });

        var result = await _client.CallToolAsync("delete_page", new Dictionary<string, object?>
        {
            ["page_id"] = "page-abc",
        });

        Assert.Equal(2, result.Content.Count);

        var link = Assert.IsType<ResourceLinkBlock>(result.Content[0]);
        Assert.Equal("buildin://page-abc", link.Uri);

        var text = Assert.IsType<TextContentBlock>(result.Content[1]);
        Assert.Contains("\"page_id\":\"page-abc\"", text.Text);
        Assert.Contains("\"archived\":true", text.Text);
        Assert.Contains("\"changed\":true", text.Text);

        await _lifecycle.Received(1).DeleteAsync("page-abc", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_NoOp_ReturnsChangedFalse()
    {
        _lifecycle.DeleteAsync("page-already-deleted", Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = "page-already-deleted",
                Archived = true,
                Changed = false,
            });

        var result = await _client.CallToolAsync("delete_page", new Dictionary<string, object?>
        {
            ["page_id"] = "page-already-deleted",
        });

        var text = Assert.IsType<TextContentBlock>(result.Content[1]);
        Assert.Contains("\"changed\":false", text.Text);
    }

    [Fact]
    public async Task NotFound_ThrowsResourceNotFound()
    {
        _lifecycle.DeleteAsync("missing-page", Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = "missing-page",
                Changed = false,
                FailureClass = FailureClass.NotFound,
                UnderlyingException = new InvalidOperationException("not found"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("delete_page", new Dictionary<string, object?>
            {
                ["page_id"] = "missing-page",
            }));

        Assert.Equal(McpErrorCode.ResourceNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task AuthFailure_ThrowsInternalError()
    {
        _lifecycle.DeleteAsync("page-auth", Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = "page-auth",
                Changed = false,
                FailureClass = FailureClass.Auth,
                UnderlyingException = new UnauthorizedAccessException("Invalid token"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("delete_page", new Dictionary<string, object?>
            {
                ["page_id"] = "page-auth",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains("Authentication error", ex.Message);
    }

    [Fact]
    public async Task TransportError_ThrowsInternalError()
    {
        _lifecycle.DeleteAsync("page-transport", Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = "page-transport",
                Changed = false,
                FailureClass = FailureClass.Transport,
                UnderlyingException = new HttpRequestException("Connection refused"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("delete_page", new Dictionary<string, object?>
            {
                ["page_id"] = "page-transport",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains("Transport error", ex.Message);
    }

    [Fact]
    public async Task UnexpectedError_ThrowsInternalError()
    {
        _lifecycle.DeleteAsync("page-unexpected", Arg.Any<CancellationToken>())
            .Returns(new PageLifecycleOutcome
            {
                PageId = "page-unexpected",
                Changed = false,
                FailureClass = FailureClass.Unexpected,
                UnderlyingException = new InvalidOperationException("Something went wrong"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("delete_page", new Dictionary<string, object?>
            {
                ["page_id"] = "page-unexpected",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains("Unexpected error", ex.Message);
    }
}
