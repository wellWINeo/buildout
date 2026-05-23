using Buildout.IntegrationTests.Buildin;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Buildout.IntegrationTests.Llm;

internal static class McpSkBridge
{
    public static async Task<McpTestHarness> CreateHarnessAsync(BuildinWireMockFixture fixture)
    {
        var mcpExe = FindMcpServerExecutable();

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = mcpExe,
            Arguments = [],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["Buildin__BaseUrl"] = fixture.BaseUrl,
                ["Buildin__BotToken"] = "test-token",
                ["Buildin__Http__UnsafeAllowInsecure"] = "true",
            },
            ShutdownTimeout = TimeSpan.FromSeconds(5),
        });

        var lf = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug));
        var client = await McpClient.CreateAsync(transport, new McpClientOptions(), lf);

        return new McpTestHarness(client);
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
                "Fetch the full Markdown content of a Buildin page by its UUID. Given a URL like https://buildin.ai/abc123-..., extract the UUID and pass it here. Call this after search to read a page.",
                [new KernelParameterMetadata("pageId")
                {
                    Description = "Page UUID. From a Buildin URL https://buildin.ai/<uuid>, pass the <uuid> part.",
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
                modelId: "mistralai/mistral-small-3.2-24b-instruct",
                apiKey: apiKey,
                httpClient: httpClient)
            .Build();
    }

    private static string FindMcpServerExecutable()
    {
        var assemblyDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        var candidates = new[]
        {
            Path.Combine(repoRoot, "src", "Buildout.Mcp", "bin", "Release", "net10.0", "Buildout.Mcp"),
            Path.Combine(repoRoot, "src", "Buildout.Mcp", "bin", "Debug", "net10.0", "Buildout.Mcp"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            $"MCP server executable not found. Searched:\n{string.Join("\n", candidates)}\n" +
            "Build the MCP project first: dotnet build src/Buildout.Mcp -c Release");
    }
}

internal sealed class McpTestHarness : IAsyncDisposable
{
    public McpClient Client { get; }

    public McpTestHarness(McpClient client)
    {
        Client = client;
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
    }
}
