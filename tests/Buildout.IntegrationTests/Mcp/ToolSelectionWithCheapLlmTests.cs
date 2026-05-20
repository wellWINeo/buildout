using Buildout.IntegrationTests.Buildin;
using Buildout.IntegrationTests.Llm;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

[Collection("BuildinWireMock")]
public sealed class ToolSelectionWithCheapLlmTests
{
    private const string PageId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    private static readonly (string Prompt, string ExpectedTool)[] Prompts =
    [
        ("Delete the page at {PageId}.", "delete_page"),
        ("Archive this page: {PageId}.", "delete_page"),
        ("Please remove the page {PageId} from my workspace.", "delete_page"),
        ("Trash the page with id {PageId}.", "delete_page"),
        ("Soft-delete {PageId}.", "delete_page"),
        ("Restore the deleted page {PageId}.", "restore_page"),
        ("Undo the delete of page {PageId}.", "restore_page"),
        ("Un-archive page {PageId}.", "restore_page"),
        ("Bring page {PageId} back from the trash.", "restore_page"),
        ("Recover the archived page {PageId}.", "restore_page"),
    ];

    private readonly ITestOutputHelper _output;
    private readonly BuildinWireMockFixture _fixture;

    public ToolSelectionWithCheapLlmTests(ITestOutputHelper output, BuildinWireMockFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _fixture.Reset();
    }

    private static string GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        Assert.False(string.IsNullOrWhiteSpace(key), "OPENROUTER_API_KEY environment variable is not set.");
        return key!;
    }

    private void SetupStubs()
    {
        BuildinStubs.RegisterGetPageArchived(_fixture.Server, PageId, archived: false);
        BuildinStubs.RegisterUpdatePageToggleArchived(_fixture.Server, PageId);
    }

    private static ChatHistory BuildHistory(McpTestHarness harness, string userMessage)
    {
        var history = new ChatHistory();
        if (!string.IsNullOrWhiteSpace(harness.Client.ServerInstructions))
            history.AddSystemMessage(harness.Client.ServerInstructions);
        history.AddUserMessage(userMessage);
        return history;
    }

    [Fact]
    public async Task LlmSelectsCorrectTool_AcrossAllPrompts()
    {
        var apiKey = GetApiKey();
        SetupStubs();

        await using var harness = await McpSkBridge.CreateHarnessAsync(_fixture);
        var kernel = McpSkBridge.CreateOpenRouterKernel(apiKey);

        var plugin = await McpSkBridge.CreatePluginFromMcpToolsAsync(harness.Client);
        kernel.Plugins.Add(plugin);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var results = new List<(string Prompt, string Expected, string Actual, bool Pass)>();

        foreach (var (promptTemplate, expectedTool) in Prompts)
        {
            var prompt = promptTemplate.Replace("{PageId}", PageId, StringComparison.Ordinal);

            var history = BuildHistory(harness,
                $"Perform exactly this action and nothing else: {prompt} " +
                "After performing the action, reply with a single sentence confirming what you did.");

            var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
            var result = response.Content ?? "";

            _output.WriteLine($"[{expectedTool}] Prompt: {prompt}");
            _output.WriteLine($"  Response: {result}");

            var invokedDelete = result.Contains("deleted", StringComparison.OrdinalIgnoreCase)
                                || result.Contains("archived", StringComparison.OrdinalIgnoreCase)
                                || result.Contains("removed", StringComparison.OrdinalIgnoreCase)
                                || result.Contains("trashed", StringComparison.OrdinalIgnoreCase)
                                || result.Contains("soft-deleted", StringComparison.OrdinalIgnoreCase);
            var invokedRestore = result.Contains("restored", StringComparison.OrdinalIgnoreCase)
                                 || result.Contains("un-archived", StringComparison.OrdinalIgnoreCase)
                                 || result.Contains("unarchived", StringComparison.OrdinalIgnoreCase)
                                 || result.Contains("recovered", StringComparison.OrdinalIgnoreCase)
                                 || result.Contains("brought back", StringComparison.OrdinalIgnoreCase);

            var actualTool = expectedTool.Contains("delete", StringComparison.Ordinal) switch
            {
                true when invokedDelete => "delete_page",
                false when invokedRestore => "restore_page",
                _ when invokedDelete => "delete_page",
                _ when invokedRestore => "restore_page",
                _ => "unknown"
            };

            var pass = actualTool == expectedTool;
            results.Add((prompt, expectedTool, actualTool, pass));

            _output.WriteLine($"  Expected: {expectedTool}, Actual: {actualTool}, Pass: {pass}");
        }

        var correctCount = results.Count(r => r.Pass);
        _output.WriteLine($"\nScore: {correctCount}/{results.Count}");

        foreach (var failure in results.Where(r => !r.Pass))
        {
            _output.WriteLine($"  FAIL: '{failure.Prompt}' → expected {failure.Expected}, got {failure.Actual}");
        }

        Assert.True(correctCount >= 9,
            $"Expected at least 9/10 correct tool selections, got {correctCount}/10. " +
            $"Failures: {string.Join(", ", results.Where(r => !r.Pass).Select(r => $"'{r.Prompt}' → {r.Actual}"))}");
    }
}
