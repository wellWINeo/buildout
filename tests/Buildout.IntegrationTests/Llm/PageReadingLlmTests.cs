using System.IO.Pipelines;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Buildout.IntegrationTests.Buildin;
using Buildout.Mcp.Resources;
using Buildout.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Buildout.IntegrationTests.Llm;

[Collection("BuildinWireMock")]
public sealed class PageReadingLlmTests
{
    private readonly ITestOutputHelper _output;
    private readonly BuildinWireMockFixture _fixture;

    public PageReadingLlmTests(ITestOutputHelper output, BuildinWireMockFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task LlmCanAnswerQuestionsAboutRenderedPage()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var markdown = """
                        # Quarterly Revenue Report

                        Total revenue for Q3 2025 was **$4.2 million**, up 12% from Q2.
                        The largest contributor was the Enterprise segment at $2.8 million.
                        """;

        var prompt = $"""
                      You are reading a rendered Markdown page. Answer the following question
                      based ONLY on the content below.

                      --- PAGE START ---
                      {markdown}
                      --- PAGE END ---

                      Question: What was the total revenue for Q3 2025, and which segment contributed the most?
                      Answer in a single sentence.
                      """;

        var httpClient = new HttpClient { BaseAddress = new Uri("https://openrouter.ai/api/v1") };
        httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/buildout");

        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
                apiKey: apiKey,
                httpClient: httpClient)
            .Build();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(prompt);
        var response = await chatService.GetChatMessageContentAsync(history);

        _output.WriteLine($"LLM response: {response.Content}");

        Assert.Contains("4.2", response.Content);
        Assert.Contains("Enterprise", response.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LlmCanFindAndReadPage()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        BuildinStubs.RegisterSearchPages(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = "33333333-3333-3333-3333-333333333333",
                    archived = false,
                    created_time = "2025-03-01T12:00:00Z",
                    last_edited_time = "2025-03-02T12:00:00Z",
                    properties = new { title = new { title = new[] { new { type = "text", plain_text = "Q3 Revenue Report" } } } }
                },
                new
                {
                    id = "44444444-4444-4444-4444-444444444444",
                    archived = false,
                    created_time = "2025-03-01T12:00:00Z",
                    last_edited_time = "2025-03-02T12:00:00Z",
                    properties = new { title = new { title = new[] { new { type = "text", plain_text = "Marketing Plan" } } } }
                }
            },
            has_more = false
        });

        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = "33333333-3333-3333-3333-333333333333",
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = "https://api.buildin.ai/pages/33333333",
            properties = new { title = new { title = new[] { new { type = "text", plain_text = "Quarterly Revenue Report" } } } }
        });

        BuildinStubs.RegisterGetBlockChildren(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = "block-1",
                    type = "paragraph",
                    created_time = "2025-01-01T00:00:00Z",
                    has_children = false,
                    data = new
                    {
                        rich_text = new object[]
                        {
                            new { type = "text", plain_text = "Total revenue for Q3 2025 was " },
                            new { type = "text", plain_text = "$4.2 million", annotations = new { bold = true } },
                            new { type = "text", plain_text = ", up 12% from Q2. The largest contributor was the Enterprise segment at $2.8 million." }
                        }
                    }
                }
            },
            has_more = false
        });

        var buildinClient = _fixture.CreateClient();
        var services = new ServiceCollection();
        services.AddSingleton<IBuildinClient>(buildinClient);
        services.AddBuildoutCore();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddMcpServer().WithResources<PageResourceHandler>().WithTools<SearchToolHandler>();

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

        var mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            sp.GetRequiredService<ILoggerFactory>());

        var openRouterHttp = new HttpClient { BaseAddress = new Uri("https://openrouter.ai/api/v1") };
        openRouterHttp.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/buildout");

        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
                apiKey: apiKey,
                httpClient: openRouterHttp)
            .Build();

        kernel.Plugins.AddFromFunctions("buildin",
        [
            KernelFunctionFactory.CreateFromMethod(
                async (string query) =>
                {
                    var result = await mcpClient.CallToolAsync("search",
                        new Dictionary<string, object?> { ["query"] = query });
                    return result.Content.OfType<TextContentBlock>().First().Text;
                },
                "search",
                "Search buildin pages by query. Returns matches with page IDs and titles.",
                [new KernelParameterMetadata("query") { Description = "Search query", IsRequired = true }]),

            KernelFunctionFactory.CreateFromMethod(
                async (string pageId) =>
                {
                    var result = await mcpClient.ReadResourceAsync($"buildin://{pageId}");
                    return result.Contents.OfType<TextResourceContents>().First().Text;
                },
                "read_buildin_page",
                "Read a buildin page and return its rendered Markdown content.",
                [new KernelParameterMetadata("pageId") { Description = "Buildin page ID", IsRequired = true }])
        ]);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await kernel.InvokePromptAsync(
            "Which page describes Q3 revenue, and what was the total?",
            new KernelArguments(settings));

        _output.WriteLine($"LLM response: {result}");

        Assert.Contains("4.2", result.ToString());

        await mcpClient.DisposeAsync();
        await server.DisposeAsync();
        c2s.Writer.Complete();
        c2s.Reader.Complete();
        s2c.Writer.Complete();
        s2c.Reader.Complete();
        await sp.DisposeAsync();
    }
}
