using System.IO.Pipelines;
using Buildout.Core.Markdown;
using Buildout.Mcp.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

public sealed class PageResourceChildDatabaseTests : IAsyncLifetime
{
    private readonly IPageMarkdownRenderer _renderer = Substitute.For<IPageMarkdownRenderer>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageMarkdownRenderer>(_renderer);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithResources<PageResourceHandler>();

        _sp = services.BuildServiceProvider();

        var options = _sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        _c2s = new Pipe();
        _s2c = new Pipe();

        _server = McpServer.Create(
            new StreamServerTransport(
                _c2s.Reader.AsStream(),
                _s2c.Writer.AsStream()),
            options,
            _sp.GetRequiredService<ILoggerFactory>(),
            _sp);

        _ = _server.RunAsync();

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(
                _c2s.Writer.AsStream(),
                _s2c.Reader.AsStream()),
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
    public async Task PageWithChildDatabase_BodyContainsDatabaseHeading()
    {
        const string pageId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
        const string expectedMarkdown = "# My Page\n\n## Embedded DB\n\n| Name |\n|---|\n| Item One |\n\n";

        _renderer.RenderAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedMarkdown));

        var result = await _client.ReadResourceAsync($"buildin://{pageId}");

        Assert.Single(result.Contents);
        var textContent = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.Contains("## Embedded DB", textContent.Text);
    }

    [Fact]
    public async Task PageWithChildDatabase_BodyContainsTableRow()
    {
        const string pageId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
        const string expectedMarkdown = "# My Page\n\n## Embedded DB\n\n| Name |\n|---|\n| Item One |\n\n";

        _renderer.RenderAsync(pageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedMarkdown));

        var result = await _client.ReadResourceAsync($"buildin://{pageId}");

        Assert.Single(result.Contents);
        var textContent = Assert.IsType<TextResourceContents>(result.Contents[0]);
        Assert.Contains("Item One", textContent.Text);
    }
}
