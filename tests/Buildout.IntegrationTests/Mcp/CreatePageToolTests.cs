using System.IO.Pipelines;
using Buildout.Core.Markdown.Authoring;
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

public sealed class CreatePageToolTests : IAsyncLifetime
{
    private readonly IPageCreator _creator = Substitute.For<IPageCreator>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageCreator>(_creator);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithTools<CreatePageToolHandler>();

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
    public async Task ServerAdvertisesCreatePageTool()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("create_page", tools[0].Name);
        Assert.NotNull(tools[0].Description);
    }

    [Fact]
    public async Task HappyPath_ReturnsResourceLinkBlock()
    {
        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome { NewPageId = "page-abc", ResolvedTitle = "My Page" });

        var result = await _client.CallToolAsync("create_page", new Dictionary<string, object?>
        {
            ["parent_id"] = "parent-123",
            ["markdown"] = "# My Page\n\nHello world.",
            ["title"] = "My Page",
        });

        var link = Assert.IsType<ResourceLinkBlock>(Assert.Single(result.Content));
        Assert.Equal("buildin://page-abc", link.Uri);
        Assert.Equal("My Page", link.Name);

        await _creator.Received(1).CreateAsync(
            Arg.Is<CreatePageInput>(i =>
                i.ParentId == "parent-123" &&
                i.Markdown == "# My Page\n\nHello world." &&
                i.Title == "My Page"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_TitleFromLeadingH1_WhenNoTitleParam()
    {
        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome { NewPageId = "page-xyz", ResolvedTitle = "Extracted Title" });

        var result = await _client.CallToolAsync("create_page", new Dictionary<string, object?>
        {
            ["parent_id"] = "parent-123",
            ["markdown"] = "# Extracted Title\n\nContent here.",
        });

        var link = Assert.IsType<ResourceLinkBlock>(Assert.Single(result.Content));
        Assert.Equal("buildin://page-xyz", link.Uri);
        Assert.Equal("Extracted Title", link.Name);
    }

    [Fact]
    public async Task ValidationError_ThrowsMcpInvalidParams()
    {
        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome
            {
                NewPageId = "",
                FailureClass = FailureClass.Validation,
                UnderlyingException = new ArgumentException("parent_id must not be empty"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = "",
                ["markdown"] = "# Page",
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task NotFound_ThrowsMcpResourceNotFound()
    {
        const string ParentId = "missing-parent-id";

        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome
            {
                NewPageId = "",
                FailureClass = FailureClass.NotFound,
                UnderlyingException = new InvalidOperationException("not found"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = ParentId,
                ["markdown"] = "# Page",
            }));

        Assert.Equal(McpErrorCode.ResourceNotFound, ex.ErrorCode);
        Assert.Contains($"Parent '{ParentId}'", ex.Message);
        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task AuthFailure_ThrowsMcpInternalError()
    {
        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome
            {
                NewPageId = "",
                FailureClass = FailureClass.Auth,
                UnderlyingException = new UnauthorizedAccessException("Invalid token"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = "parent-123",
                ["markdown"] = "# Page",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains("Authentication error", ex.Message);
    }

    [Fact]
    public async Task TransportFailure_ThrowsMcpInternalError()
    {
        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome
            {
                NewPageId = "",
                FailureClass = FailureClass.Transport,
                UnderlyingException = new HttpRequestException("Connection refused"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = "parent-123",
                ["markdown"] = "# Page",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains("Transport error", ex.Message);
    }

    [Fact]
    public async Task UnexpectedError_ThrowsMcpInternalError()
    {
        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePageOutcome
            {
                NewPageId = "",
                FailureClass = FailureClass.Unexpected,
                UnderlyingException = new InvalidOperationException("Something went wrong"),
            });

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = "parent-123",
                ["markdown"] = "# Page",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains("Unexpected error", ex.Message);
    }

    [Fact]
    public async Task PartialFailure_ThrowsMcpInternalErrorWithPartialPageId()
    {
        const string PartialPageId = "partial-page-id";

        _creator.CreateAsync(Arg.Any<CreatePageInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<CreatePageOutcome>>(_ =>
                throw new PartialCreationException(PartialPageId, 1, 3, new InvalidOperationException("append failed")));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("create_page", new Dictionary<string, object?>
            {
                ["parent_id"] = "parent-123",
                ["markdown"] = "# Page\n\nBlock 1\n\nBlock 2\n\nBlock 3",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains($"page {PartialPageId}", ex.Message);
        Assert.Contains("Partial creation", ex.Message);
    }
}
