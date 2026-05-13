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
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Buildout.IntegrationTests.Llm;

internal static class McpSkBridge
{
    public static async Task<McpTestHarness> CreateHarnessAsync(
        BuildinWireMockFixture fixture,
        bool includeDatabaseView = false)
    {
        var buildinClient = fixture.CreateClient();
        var services = new ServiceCollection();
        services.AddSingleton<IBuildinClient>(buildinClient);
        services.AddBuildoutCore();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));

        var mcpBuilder = services.AddMcpServer()
            .WithResources<PageResourceHandler>()
            .WithTools<SearchToolHandler>();

        if (includeDatabaseView)
            mcpBuilder.WithTools<DatabaseViewToolHandler>();

        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var lf = sp.GetRequiredService<ILoggerFactory>();

        var c2s = new Pipe();
        var s2c = new Pipe();

        var server = McpServer.Create(
            new StreamServerTransport(c2s.Reader.AsStream(), s2c.Writer.AsStream()),
            options,
            lf,
            sp);

        _ = server.RunAsync();

        var mcpClient = await McpClient.CreateAsync(
            new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream()),
            new McpClientOptions(),
            lf);

        return new McpTestHarness(sp, server, mcpClient, c2s, s2c);
    }

    public static async Task<KernelPlugin> CreatePluginFromMcpToolsAsync(
        McpClient mcpClient, string pluginName = "buildin")
    {
        var tools = await mcpClient.ListToolsAsync();

        var functions = new List<KernelFunction>();
        functions.AddRange(tools.Select(t => t.AsKernelFunction()));

        var resourceTemplates = await mcpClient.ListResourceTemplatesAsync();
        if (resourceTemplates.Any(rt => rt.UriTemplate.StartsWith("buildin://", StringComparison.Ordinal)))
        {
            functions.Add(KernelFunctionFactory.CreateFromMethod(
                async (string pageId) =>
                {
                    var result = await mcpClient.ReadResourceAsync($"buildin://{pageId}");
                    return result.Contents.OfType<TextResourceContents>().First().Text;
                },
                "read_buildin_page",
                "Read a buildin page and return its rendered Markdown content.",
                [new KernelParameterMetadata("pageId")
                {
                    Description = "Buildin page UUID",
                    IsRequired = true
                }]));
        }

        return KernelPluginFactory.CreateFromFunctions(pluginName, functions);
    }

    public static Kernel CreateOpenRouterKernel(string apiKey)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://openrouter.ai/api/v1") };
        httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/buildout");

        return Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
                apiKey: apiKey,
                httpClient: httpClient)
            .Build();
    }
}

internal sealed class McpTestHarness : IAsyncDisposable
{
    public ServiceProvider ServiceProvider { get; }
    public McpServer Server { get; }
    public McpClient Client { get; }

    private readonly Pipe _c2s;
    private readonly Pipe _s2c;

    public McpTestHarness(ServiceProvider sp, McpServer server, McpClient client, Pipe c2s, Pipe s2c)
    {
        ServiceProvider = sp;
        Server = server;
        Client = client;
        _c2s = c2s;
        _s2c = s2c;
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await Server.DisposeAsync();
        _c2s.Writer.Complete();
        _c2s.Reader.Complete();
        _s2c.Writer.Complete();
        _s2c.Reader.Complete();
        await ServiceProvider.DisposeAsync();
    }
}
