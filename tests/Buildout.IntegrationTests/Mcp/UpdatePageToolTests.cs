using System.IO.Pipelines;
using System.Text.Json;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Markdown.Editing;
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

public sealed class UpdatePageToolTests : IAsyncLifetime
{
    private readonly IPageEditor _editor = Substitute.For<IPageEditor>();
    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageEditor>(_editor);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithTools<UpdatePageToolHandler>();

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
    public async Task ServerAdvertisesUpdatePageTool()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("update_page", tools[0].Name);
        Assert.NotNull(tools[0].Description);
    }

    [Fact]
    public async Task HappyPath_SearchReplace_ReturnsReconciliationSummary()
    {
        var summary = new ReconciliationSummary
        {
            PreservedBlocks = 3,
            UpdatedBlocks = 1,
            NewBlocks = 0,
            DeletedBlocks = 0,
            AmbiguousMatches = 0,
            NewRevision = "rev-new-001",
        };

        _editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(summary);

        var result = await _client.CallToolAsync("update_page", new Dictionary<string, object?>
        {
            ["page_id"] = "page-1",
            ["revision"] = "rev-old-001",
            ["operations"] = """[{"op":"search_replace","old_str":"Hello","new_str":"World"}]""",
        });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        var doc = JsonDocument.Parse(text);

        Assert.Equal("rev-new-001", doc.RootElement.GetProperty("NewRevision").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("UpdatedBlocks").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("PreservedBlocks").GetInt32());
    }

    [Fact]
    public async Task DryRun_ReturnsPostEditMarkdown_WithoutCommitting()
    {
        var summary = new ReconciliationSummary
        {
            PreservedBlocks = 2,
            UpdatedBlocks = 1,
            NewBlocks = 0,
            DeletedBlocks = 0,
            AmbiguousMatches = 0,
            NewRevision = "rev-dry-preview",
            PostEditMarkdown = "# Title\n\nWorld.",
        };

        _editor.UpdateAsync(
            Arg.Is<UpdatePageInput>(i => i.DryRun),
            Arg.Any<CancellationToken>())
            .Returns(summary);

        var result = await _client.CallToolAsync("update_page", new Dictionary<string, object?>
        {
            ["page_id"] = "page-dry",
            ["revision"] = "rev-dry-001",
            ["operations"] = """[{"op":"search_replace","old_str":"Hello","new_str":"World"}]""",
            ["dry_run"] = true,
        });

        var text = result.Content.OfType<TextContentBlock>().First().Text;
        var doc = JsonDocument.Parse(text);

        Assert.Equal("# Title\n\nWorld.", doc.RootElement.GetProperty("PostEditMarkdown").GetString());
    }

    [Fact]
    public async Task StaleRevision_ReturnsInvalidParams()
    {
        _editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<ReconciliationSummary>>(_ => throw new StaleRevisionException("rev-current-999"));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("update_page", new Dictionary<string, object?>
            {
                ["page_id"] = "page-stale",
                ["revision"] = "rev-old",
                ["operations"] = """[{"op":"search_replace","old_str":"Hello","new_str":"World"}]""",
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task LargeDelete_ReturnsInvalidParams()
    {
        _editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<ReconciliationSummary>>(_ => throw new LargeDeleteException(5000, 1000));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("update_page", new Dictionary<string, object?>
            {
                ["page_id"] = "page-largedelete",
                ["revision"] = "rev-001",
                ["operations"] = """[{"op":"search_replace","old_str":"Hello","new_str":""}]""",
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task PartialPatch_ReturnsInternalError()
    {
        _editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<ReconciliationSummary>>(_ =>
                throw new PartialPatchException("rev-partial", 1, new InvalidOperationException("buildin error")));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("update_page", new Dictionary<string, object?>
            {
                ["page_id"] = "page-partial",
                ["revision"] = "rev-001",
                ["operations"] = """[{"op":"search_replace","old_str":"A","new_str":"B"},{"op":"search_replace","old_str":"C","new_str":"D"}]""",
            }));

        Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);
    }

    [Fact]
    public async Task PageNotFound_ReturnsInvalidParams()
    {
        const string PageId = "nonexistent-page";

        _editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<ReconciliationSummary>>(_ =>
                throw new BuildinApiException(
                    new ApiError(404, "object_not_found", "Could not find page.", null)));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("update_page", new Dictionary<string, object?>
            {
                ["page_id"] = PageId,
                ["revision"] = "rev-001",
                ["operations"] = """[{"op":"search_replace","old_str":"Hello","new_str":"World"}]""",
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
        Assert.Contains(PageId, ex.Message);
    }

    [Fact]
    public async Task UnknownAnchor_ReturnsInvalidParams()
    {
        _editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<ReconciliationSummary>>(_ =>
                throw new UnknownAnchorException("block-xyz-123"));

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await _client.CallToolAsync("update_page", new Dictionary<string, object?>
            {
                ["page_id"] = "page-anchor",
                ["revision"] = "rev-001",
                ["operations"] = """[{"op":"replace_block","anchor":"block-xyz-123","new_markdown":"# New"}]""",
            }));

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
    }
}
