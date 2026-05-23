using Buildout.Mcp.Prompts;
using Microsoft.Extensions.AI;
using Xunit;

namespace Buildout.UnitTests.Mcp;

public class BuildoutPromptsTests
{
    [Fact]
    public void UpdateWorkflow_ReturnsChatMessage_WithUserRole()
    {
        var result = BuildoutPrompts.UpdateWorkflow();
        Assert.Equal(ChatRole.User, result.Role);
    }

    [Fact]
    public void UpdateWorkflow_ContentIsNonEmpty()
    {
        var result = BuildoutPrompts.UpdateWorkflow();
        Assert.NotEmpty(result.Contents);
    }

    [Fact]
    public void UpdateWorkflow_ContentMatchesEmbeddedResource()
    {
        var result = BuildoutPrompts.UpdateWorkflow();
        var text = string.Concat(result.Contents.OfType<TextContent>().Select(c => c.Text));
        Assert.Equal(PromptResourceLoader.Load("update"), text);
    }

    [Fact]
    public void BaseInstructions_UnderFourHundredWords()
    {
        var content = PromptResourceLoader.Load("server-instructions");
        var wordCount = content.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(wordCount < 400, $"Base instructions should be under 400 words: {wordCount} words");
    }

    [Fact]
    public void BaseInstructions_DoNotContainUpdateDetails()
    {
        var content = PromptResourceLoader.Load("server-instructions");
        Assert.DoesNotContain("Step 1:", content);
        Assert.DoesNotContain("Partial Updates", content);
    }
}
