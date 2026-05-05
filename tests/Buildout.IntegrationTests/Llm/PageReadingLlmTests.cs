using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Xunit;

namespace Buildout.IntegrationTests.Llm;

public sealed class PageReadingLlmTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task LlmCanAnswerQuestionsAboutRenderedPage()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
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

        var client = new AnthropicClient(new APIAuthentication(apiKey));

        var parameters = new MessageParameters
        {
            Model = "claude-haiku-4-5-20250415",
            MaxTokens = 200,
            Messages = [new Message { Role = RoleType.User, Content = [new TextContent { Text = prompt }] }],
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters);

        Assert.NotEmpty(response.Content);
        var text = string.Join("", response.Content.OfType<TextContent>().Select(c => c.Text));
        _output.WriteLine($"LLM response: {text}");

        Assert.Contains("4.2", text);
        Assert.Contains("Enterprise", text, StringComparison.OrdinalIgnoreCase);
    }
}
