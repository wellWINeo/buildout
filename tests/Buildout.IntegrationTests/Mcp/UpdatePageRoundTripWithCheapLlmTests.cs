using System.IO.Pipelines;
using System.Text.Json;
using Buildout.Core.Markdown.Editing;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

// FR-025 / SC-010: Simulate an LLM doing get_page_markdown -> update_page -> get_page_markdown
// and verify anchor IDs are preserved for unchanged blocks.
public sealed class UpdatePageRoundTripWithCheapLlmTests : IAsyncLifetime
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
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer()
            .WithTools<GetPageMarkdownToolHandler>()
            .WithTools<UpdatePageToolHandler>();

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
    public async Task LlmRoundTrip_PreservesUnchangedAnchorIds()
    {
        const string PageId = "page-roundtrip-001";

        var firstSnapshot = new AnchoredPageSnapshot
        {
            Markdown = "<!-- buildin:root -->\n\n<!-- buildin:block:b1 -->\n# Title\n\n<!-- buildin:block:b2 -->\nHello world\n\n<!-- buildin:block:b3 -->\nFooter text\n",
            Revision = "rev001",
            UnknownBlockIds = [],
        };

        var secondSnapshot = new AnchoredPageSnapshot
        {
            Markdown = "<!-- buildin:root -->\n\n<!-- buildin:block:b1 -->\n# Title\n\n<!-- buildin:block:b2 -->\nNew content\n\n<!-- buildin:block:b3 -->\nFooter text\n",
            Revision = "rev002",
            UnknownBlockIds = [],
        };

        var updateSummary = new ReconciliationSummary
        {
            PreservedBlocks = 0,
            UpdatedBlocks = 1,
            NewBlocks = 0,
            DeletedBlocks = 0,
            AmbiguousMatches = 0,
            NewRevision = "rev002",
            PostEditMarkdown = "<!-- buildin:root -->\n\n<!-- buildin:block:b1 -->\n# Title\n\n<!-- buildin:block:b2 -->\nNew content\n\n<!-- buildin:block:b3 -->\nFooter text\n",
        };

        _editor.FetchForEditAsync(PageId, Arg.Any<CancellationToken>())
            .Returns(firstSnapshot, secondSnapshot);

        _editor.UpdateAsync(Arg.Any<UpdatePageInput>(), Arg.Any<CancellationToken>())
            .Returns(updateSummary);

        // LLM step 1: fetch the page
        var getResult1 = await _client.CallToolAsync("get_page_markdown", new Dictionary<string, object?>
        {
            ["page_id"] = PageId,
        });

        var getText1 = getResult1.Content.OfType<TextContentBlock>().First().Text;
        var getDoc1 = JsonDocument.Parse(getText1);
        var preMarkdown = getDoc1.RootElement.GetProperty("Markdown").GetString()!;
        var revision = getDoc1.RootElement.GetProperty("Revision").GetString()!;

        Assert.Equal("rev001", revision);

        // Verify all three anchor IDs appear in the pre-edit markdown
        Assert.Contains("<!-- buildin:block:b1 -->", preMarkdown);
        Assert.Contains("<!-- buildin:block:b2 -->", preMarkdown);
        Assert.Contains("<!-- buildin:block:b3 -->", preMarkdown);

        // LLM step 2: simulate the LLM deriving a search_replace operation from the snapshot
        // The "cheap LLM" sees "Hello world" in block b2 and replaces it with "New content"
        var derivedOps = """[{"op":"search_replace","old_str":"Hello world","new_str":"New content"}]""";

        var updateResult = await _client.CallToolAsync("update_page", new Dictionary<string, object?>
        {
            ["page_id"] = PageId,
            ["revision"] = revision,
            ["operations"] = derivedOps,
        });

        var updateText = updateResult.Content.OfType<TextContentBlock>().First().Text;
        var updateDoc = JsonDocument.Parse(updateText);

        Assert.Equal(1, updateDoc.RootElement.GetProperty("UpdatedBlocks").GetInt32());
        Assert.Equal("rev002", updateDoc.RootElement.GetProperty("NewRevision").GetString());

        // LLM step 3: fetch the page again after the update
        var getResult2 = await _client.CallToolAsync("get_page_markdown", new Dictionary<string, object?>
        {
            ["page_id"] = PageId,
        });

        var getText2 = getResult2.Content.OfType<TextContentBlock>().First().Text;
        var getDoc2 = JsonDocument.Parse(getText2);
        var postMarkdown = getDoc2.RootElement.GetProperty("Markdown").GetString()!;
        var postRevision = getDoc2.RootElement.GetProperty("Revision").GetString()!;

        // The targeted block should now contain the new content
        Assert.Contains("New content", postMarkdown);
        Assert.DoesNotContain("Hello world", postMarkdown);

        // Anchor IDs b1, b2, b3 are all preserved in the post-edit markdown
        Assert.Contains("<!-- buildin:block:b1 -->", postMarkdown);
        Assert.Contains("<!-- buildin:block:b2 -->", postMarkdown);
        Assert.Contains("<!-- buildin:block:b3 -->", postMarkdown);

        Assert.Equal("rev002", postRevision);
    }
}
