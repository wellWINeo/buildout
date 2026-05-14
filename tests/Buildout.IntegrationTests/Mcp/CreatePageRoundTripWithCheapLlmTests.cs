using System.IO.Pipelines;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.IntegrationTests.Buildin;
using Buildout.IntegrationTests.Llm;
using Buildout.Mcp.Resources;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

[Collection("BuildinWireMock")]
public sealed class CreatePageRoundTripWithCheapLlmTests : IAsyncLifetime
{
    private readonly BuildinWireMockFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const string ParentId = "00000000-0000-0000-0000-000000000001";
    private const string NewPageId = "00000000-0000-0000-0000-000000000002";

    private ServiceProvider _sp = null!;
    private McpServer _server = null!;
    private McpClient _mcpClient = null!;
    private Pipe _c2s = null!;
    private Pipe _s2c = null!;

    public CreatePageRoundTripWithCheapLlmTests(ITestOutputHelper output, BuildinWireMockFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _fixture.Reset();

        // 1. Parent page probe — exact path, takes priority over regex-based RegisterGetPage
        BuildinStubs.RegisterPageProbe(_fixture.Server, ParentId, new
        {
            id = ParentId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{ParentId}",
            properties = new { title = new { type = "title", title = Array.Empty<object>() } }
        });

        // 2. Create page response
        BuildinStubs.RegisterCreatePage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new
            {
                title = new
                {
                    type = "title",
                    title = new[] { new { type = "text", plain_text = "" } }
                }
            }
        });

        // 3. Get page (for resource read — regex-based, matches any page GET)
        //    Registered AFTER RegisterPageProbe so the exact-path stub wins for the parent.
        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = NewPageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = $"https://api.buildin.ai/pages/{NewPageId}",
            properties = new { title = new { type = "title", title = new[] { new { type = "text", plain_text = "My Page" } } } }
        });

        // 4. Get block children (for resource read)
        BuildinStubs.RegisterGetBlockChildren(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = "00000000-0000-0000-0000-000000000010",
                    type = "paragraph",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new
                    {
                        rich_text = new object[]
                        {
                            new
                            {
                                type = "text",
                                plain_text = "Hello world",
                                annotations = new { bold = false, italic = false, code = false }
                            }
                        }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    public async ValueTask InitializeAsync()
    {
        var client = _fixture.CreateClient();

        var services = new ServiceCollection();
        services.AddBuildoutCore();
        services.AddSingleton<IBuildinClient>(client);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMcpServer()
            .WithResources<PageResourceHandler>()
            .WithTools<CreatePageToolHandler>();

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

        _mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(_c2s.Writer.AsStream(), _s2c.Reader.AsStream()),
            new McpClientOptions(),
            _sp.GetRequiredService<ILoggerFactory>());
    }

    public async ValueTask DisposeAsync()
    {
        await _mcpClient.DisposeAsync();
        await _server.DisposeAsync();
        _c2s.Writer.Complete();
        _c2s.Reader.Complete();
        _s2c.Writer.Complete();
        _s2c.Reader.Complete();
        await _sp.DisposeAsync();
    }

    [Fact]
    public async Task CreatePage_ReadBack_ParagraphMatchesInput()
    {
        const string markdown = "# My Page\n\nHello world";

        var result = await _mcpClient.CallToolAsync("create_page", new Dictionary<string, object?>
        {
            ["parent_id"] = ParentId,
            ["markdown"] = markdown,
        });

        var link = Assert.IsType<ResourceLinkBlock>(Assert.Single(result.Content));
        Assert.StartsWith("buildin://", link.Uri);
        var newPageId = link.Uri["buildin://".Length..];

        var resource = await _mcpClient.ReadResourceAsync($"buildin://{newPageId}");
        var renderedMarkdown = resource.Contents.OfType<TextResourceContents>().First().Text;

        Assert.Contains("Hello world", renderedMarkdown);
    }

    [Fact]
    public async Task CreatePage_ResourceLinkUri_ContainsNewPageId()
    {
        const string markdown = "# My Page\n\nHello world";

        var result = await _mcpClient.CallToolAsync("create_page", new Dictionary<string, object?>
        {
            ["parent_id"] = ParentId,
            ["markdown"] = markdown,
        });

        var link = Assert.IsType<ResourceLinkBlock>(Assert.Single(result.Content));
        Assert.Equal($"buildin://{NewPageId}", link.Uri);
    }

    [Fact]
    public async Task LlmCanCreatePageAndReadItBack()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var kernel = McpSkBridge.CreateOpenRouterKernel(apiKey);
        var plugin = await McpSkBridge.CreatePluginFromMcpToolsAsync(_mcpClient);
        kernel.Plugins.Add(plugin);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await kernel.InvokePromptAsync(
            $"Create a new buildin page under parent ID {ParentId} with title 'My Page' and content '# My Page\n\nHello world.'. Then read the page back and tell me the body content.",
            new KernelArguments(settings));

        _output.WriteLine($"LLM response: {result}");
        Assert.Contains("Hello world", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
