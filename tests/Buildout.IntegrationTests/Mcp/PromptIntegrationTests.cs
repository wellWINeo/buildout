using Buildout.Mcp.Prompts;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

public class PromptIntegrationTests
{
    private static readonly char[] WordSeparators = new[] { ' ', '\n', '\r', '\t' };

    [Fact]
    public void PromptResourceLoader_LoadServerInstructions_ReturnsContent()
    {
        var content = PromptResourceLoader.Load("server-instructions");
        Assert.NotEmpty(content);
        Assert.Contains("Buildout Wiki MCP Server", content);
    }

    [Fact]
    public void PromptResourceLoader_LoadUpdate_ReturnsContent()
    {
        var content = PromptResourceLoader.Load("update");
        Assert.NotEmpty(content);
        Assert.Contains("Page Update Workflow", content);
    }

    [Fact]
    public void PromptResourceLoader_LoadInvalidName_ThrowsException()
    {
        Assert.Throws<InvalidOperationException>(() => PromptResourceLoader.Load("invalid"));
    }

    [Fact]
    public void ServerInstructions_Under500Words()
    {
        var content = PromptResourceLoader.Load("server-instructions");
        var wordCount = content.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(wordCount < 400, $"Base instructions should be under 400 words: {wordCount} words");
    }

    [Fact]
    public void UpdateWorkflow_SufficientlyDetailed()
    {
        var content = PromptResourceLoader.Load("update");
        var wordCount = content.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(wordCount > 200, $"Update workflow should be detailed: {wordCount} words");
    }

    [Fact]
    public void ServerInstructions_DoesNotContainUpdateSpecifics()
    {
        var content = PromptResourceLoader.Load("server-instructions");

        Assert.DoesNotContain("Step 1: Verify Current State", content);
        Assert.DoesNotContain("Prepare the Update", content);
    }

    [Fact]
    public void UpdateWorkflow_ContainsWorkflowSteps()
    {
        var content = PromptResourceLoader.Load("update");

        Assert.Contains("Step 1:", content);
        Assert.Contains("Step 2:", content);
        Assert.Contains("Step 3:", content);
        Assert.Contains("Step 4:", content);
    }
}