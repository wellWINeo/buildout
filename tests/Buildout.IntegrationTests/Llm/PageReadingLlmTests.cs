using Buildout.IntegrationTests.Buildin;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xunit;

namespace Buildout.IntegrationTests.Llm;

[Collection("BuildinWireMock")]
public sealed class PageReadingLlmTests
{
    private const string Q3PageId = "33333333-3333-3333-3333-333333333333";
    private const string DbId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    private readonly ITestOutputHelper _output;
    private readonly BuildinWireMockFixture _fixture;

    public PageReadingLlmTests(ITestOutputHelper output, BuildinWireMockFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _fixture.Reset();
    }

    private static string? GetApiKey()
        => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

    private void SetupSearchAndPageStubs()
    {
        BuildinStubs.RegisterSearchPages(_fixture.Server, new
        {
            @object = "list",
            results = new object[]
            {
                new
                {
                    id = Q3PageId,
                    archived = false,
                    created_time = "2025-03-01T12:00:00Z",
                    last_edited_time = "2025-03-02T12:00:00Z",
                    properties = new
                    {
                        title = new { title = new[] { new { type = "text", plain_text = "Q3 Revenue Report" } } }
                    }
                },
                new
                {
                    id = "44444444-4444-4444-4444-444444444444",
                    archived = false,
                    created_time = "2025-03-01T12:00:00Z",
                    last_edited_time = "2025-03-02T12:00:00Z",
                    properties = new
                    {
                        title = new { title = new[] { new { type = "text", plain_text = "Marketing Plan" } } }
                    }
                }
            },
            has_more = false
        });

        BuildinStubs.RegisterGetPage(_fixture.Server, new
        {
            id = Q3PageId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            archived = false,
            url = "https://api.buildin.ai/pages/33333333",
            properties = new
            {
                title = new { title = new[] { new { type = "text", plain_text = "Quarterly Revenue Report" } } }
            }
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
    }

    private void SetupDatabaseStubs()
    {
        BuildinStubs.RegisterGetDatabase(_fixture.Server, DbId, new
        {
            id = DbId,
            created_time = "2025-01-15T10:30:00Z",
            last_edited_time = "2025-01-16T14:00:00Z",
            title = new[] { new { type = "text", plain_text = "Employee Directory" } },
            properties = new
            {
                Name = new { type = "title", title = new { } },
                Department = new
                {
                    type = "select",
                    select = new { options = new[] { new { name = "Engineering" }, new { name = "Sales" } } }
                }
            }
        });

        BuildinStubs.RegisterQueryDatabase(_fixture.Server, DbId, new
        {
            results = new object[]
            {
                new
                {
                    properties = new
                    {
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Alice Chen" } } },
                        Department = new { type = "select", select = new { name = "Engineering" } }
                    }
                },
                new
                {
                    properties = new
                    {
                        Name = new { type = "title", title = new[] { new { type = "text", plain_text = "Bob Torres" } } },
                        Department = new { type = "select", select = new { name = "Sales" } }
                    }
                }
            },
            has_more = false,
            next_cursor = (string?)null
        });
    }

    [Fact]
    public async Task LlmCanSearchAndReadPage()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        SetupSearchAndPageStubs();

        await using var harness = await McpSkBridge.CreateHarnessAsync(_fixture);
        var kernel = McpSkBridge.CreateOpenRouterKernel(apiKey);

        var plugin = await McpSkBridge.CreatePluginFromMcpToolsAsync(harness.Client);
        kernel.Plugins.Add(plugin);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await kernel.InvokePromptAsync(
            "Which page describes Q3 revenue, and what was the total?",
            new KernelArguments(settings));

        _output.WriteLine($"LLM response: {result}");

        Assert.Contains("4.2", result.ToString());
    }

    [Fact]
    public async Task LlmCanQueryDatabaseView()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        SetupDatabaseStubs();

        await using var harness = await McpSkBridge.CreateHarnessAsync(_fixture, includeDatabaseView: true);
        var kernel = McpSkBridge.CreateOpenRouterKernel(apiKey);

        var plugin = await McpSkBridge.CreatePluginFromMcpToolsAsync(harness.Client);
        kernel.Plugins.Add(plugin);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await kernel.InvokePromptAsync(
            $"How many employees are in the Employee Directory database (id: {DbId}), and what are their names and departments?",
            new KernelArguments(settings));

        _output.WriteLine($"LLM response: {result}");

        var response = result.ToString();
        Assert.True(
            response.Contains("Alice", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Bob", StringComparison.OrdinalIgnoreCase),
            "Expected the LLM to mention at least one employee from the database.");
    }

    [Fact]
    public async Task LlmCanHandleToolError()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        BuildinStubs.RegisterSearchPages(_fixture.Server, new
        {
            @object = "list",
            results = Array.Empty<object>(),
            has_more = false
        });

        await using var harness = await McpSkBridge.CreateHarnessAsync(_fixture);
        var kernel = McpSkBridge.CreateOpenRouterKernel(apiKey);

        var plugin = await McpSkBridge.CreatePluginFromMcpToolsAsync(harness.Client);
        kernel.Plugins.Add(plugin);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await kernel.InvokePromptAsync(
            "Search for pages about 'nonexistent topic xyz123' and tell me what you find.",
            new KernelArguments(settings));

        _output.WriteLine($"LLM response: {result}");

        var response = result.ToString();
        Assert.True(
            response.Contains("no", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("empty", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("0 result", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("zero", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("couldn't find", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("none", StringComparison.OrdinalIgnoreCase),
            "Expected the LLM to acknowledge the empty search results.");
    }
}
