using System.IO.Pipelines;
using System.Text.Json;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.Core.Markdown.Editing;
using Buildout.IntegrationTests.Buildin;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Buildout.IntegrationTests.Cross;

// Exercises a real (non-dry-run) update_page commit through the actual
// PageEditor -> BotBuildinClient -> WireMock stack for an append-style patch,
// i.e. the AppendBlockChildrenAsync write path.
//
// NOTE: these tests append into an existing top-level container block
// (via append_section) rather than inserting a brand-new top-level sibling
// (via insert_after_block). A brand-new top-level block has no anchor of its
// own, and AnchoredMarkdownParser never resolves such a block's parent to the
// synthetic "root" anchor for a *real* rendered page (the "<!-- buildin:root
// -->" marker is immediately followed by the first child's own anchor
// comment, so the pending root anchor is overwritten before it is ever
// consumed) -- Reconciler then requires a non-null parent anchor to emit a
// WriteOp.Append, so a top-level insert_after_block never reaches the write
// path at all. That is a separate, pre-existing bug outside the scope of
// this change. Appending into an existing, already-anchored container
// sidesteps it while still exercising the exact same AppendBlockChildrenAsync
// write path and error handling that insert_after_block would use.
[Collection("BuildinWireMock")]
public sealed class UpdatePageAppendCommitTests
{
    private readonly BuildinWireMockFixture _fixture;

    private const string PageId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string ContainerId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
    private const string NewBlockId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";

    public UpdatePageAppendCommitTests(BuildinWireMockFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private void RegisterPageWithSingleContainer()
    {
        _fixture.Server.Reset();

        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = PageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{PageId[..8]}",
            properties = new
            {
                title = new
                {
                    type = "title",
                    title = new[] { new { type = "text", plain_text = "Test Page" } }
                }
            }
        });

        BuildinStubs.RegisterGetBlockChildren(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = ContainerId,
                    type = "bulleted_list_item",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new { rich_text = new[] { new { type = "text", plain_text = "Container" } } }
                }
            },
            has_more = false
        });
    }

    private static (ServiceProvider ServiceProvider, IPageEditor Editor) BuildRealEditor(IBuildinClient client)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddBuildoutCore(configuration);
        services.AddSingleton(client);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<IPageEditor>());
    }

    private static async Task<(McpClient Client, McpServer Server, ServiceProvider ServiceProvider, Pipe C2S, Pipe S2C)> StartUpdatePageMcpServerAsync(IPageEditor editor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(editor);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer().WithTools<UpdatePageToolHandler>();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var c2s = new Pipe();
        var s2c = new Pipe();

        var server = McpServer.Create(
            new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream()),
            options,
            sp.GetRequiredService<ILoggerFactory>(),
            sp);

        _ = server.RunAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            sp.GetRequiredService<ILoggerFactory>());

        return (client, server, sp, c2s, s2c);
    }

    private static async Task StopAsync((McpClient Client, McpServer Server, ServiceProvider ServiceProvider, Pipe C2S, Pipe S2C) mcp)
    {
        await mcp.Client.DisposeAsync();
        await mcp.Server.DisposeAsync();
        mcp.C2S.Writer.Complete();
        mcp.C2S.Reader.Complete();
        mcp.S2C.Writer.Complete();
        mcp.S2C.Reader.Complete();
        await mcp.ServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task AppendSection_RealCommit_AppendsNestedChildUsingMappedBlockId()
    {
        RegisterPageWithSingleContainer();

        // Top-level append returns a real, mapped block (with an id) instead of an empty result.
        BuildinStubs.RegisterAppendBlockChildren(_fixture.Server, ContainerId, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = NewBlockId,
                    type = "bulleted_list_item",
                    created_time = "2025-01-17T00:00:00Z",
                    has_children = true,
                    data = new { rich_text = new[] { new { type = "text", plain_text = "New item" } } }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });

        // Nested child append, only reachable if the parent's mapped id above was used to recurse.
        BuildinStubs.RegisterAppendBlockChildren(_fixture.Server, NewBlockId, new
        {
            @object = "list",
            results = Array.Empty<object>(),
            has_more = false,
            next_cursor = (string?)null
        });

        var client = _fixture.CreateClient();
        var (editorSp, editor) = BuildRealEditor(client);
        try
        {
            var snapshot = await editor.FetchForEditAsync(PageId);

            var mcp = await StartUpdatePageMcpServerAsync(editor);
            try
            {
                var result = await mcp.Client.CallToolAsync("update_page", new Dictionary<string, object?>
                {
                    ["page_id"] = PageId,
                    ["revision"] = snapshot.Revision,
                    ["operations"] = $$"""[{"op":"append_section","anchor":"{{ContainerId}}","markdown":"- New item\n  - Nested child"}]""",
                });

                var text = result.Content.OfType<TextContentBlock>().First().Text;
                var doc = JsonDocument.Parse(text);

                Assert.Equal(1, doc.RootElement.GetProperty("NewBlocks").GetInt32());
            }
            finally
            {
                await StopAsync(mcp);
            }
        }
        finally
        {
            await editorSp.DisposeAsync();
        }

        // The nested child could only have been appended to the new block if AppendBlockChildrenAsync
        // mapped the real API response (Bug 3) instead of returning an always-empty result.
        var appendPaths = _fixture.Server.LogEntries
            .Where(e => string.Equals(e.RequestMessage?.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.RequestMessage?.Path ?? "")
            .ToList();

        Assert.Contains($"/v1/blocks/{ContainerId}/children", appendPaths);
        Assert.Contains($"/v1/blocks/{NewBlockId}/children", appendPaths);
    }

    [Fact]
    public async Task AppendSection_RealCommitFails_SurfacesRealApiErrorDetail()
    {
        RegisterPageWithSingleContainer();
        BuildinStubs.RegisterAppendBlockChildrenFailure(_fixture.Server, ContainerId, 500);

        var client = _fixture.CreateClient();
        var (editorSp, editor) = BuildRealEditor(client);
        try
        {
            var snapshot = await editor.FetchForEditAsync(PageId);

            var mcp = await StartUpdatePageMcpServerAsync(editor);
            try
            {
                var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
                    await mcp.Client.CallToolAsync("update_page", new Dictionary<string, object?>
                    {
                        ["page_id"] = PageId,
                        ["revision"] = snapshot.Revision,
                        ["operations"] = $$"""[{"op":"append_section","anchor":"{{ContainerId}}","markdown":"New paragraph."}]""",
                    }));

                Assert.Equal(McpErrorCode.InternalError, ex.ErrorCode);

                // The real backend failure detail (the actual HTTP status code) must be visible,
                // not just the bare operation count.
                Assert.Contains("Underlying error:", ex.Message, StringComparison.Ordinal);
                Assert.Contains("500", ex.Message, StringComparison.Ordinal);

                // The "Patch partially applied" prefix must not be doubled.
                var firstIndex = ex.Message.IndexOf("Patch partially applied", StringComparison.Ordinal);
                var lastIndex = ex.Message.LastIndexOf("Patch partially applied", StringComparison.Ordinal);
                Assert.Equal(firstIndex, lastIndex);
            }
            finally
            {
                await StopAsync(mcp);
            }
        }
        finally
        {
            await editorSp.DisposeAsync();
        }
    }
}
