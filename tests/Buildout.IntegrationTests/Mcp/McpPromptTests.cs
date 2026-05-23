using Buildout.Mcp.Prompts;
using Xunit;

namespace Buildout.IntegrationTests.Mcp;

public class McpPromptTests
{
    [Fact]
    public void UpdatePrompt_ContainsDetailedInstructions()
    {
        var content = PromptResourceLoader.Load("update");

        Assert.Contains("Page Update Workflow", content);
        Assert.Contains("When a user asks to update a wiki page", content);
        Assert.Contains("Step 1: Verify Current State", content);
        Assert.Contains("Step 2: Prepare the Update", content);
        Assert.Contains("Step 3: Execute the Update", content);
        Assert.Contains("Step 4: Verify the Update", content);
    }

    [Fact]
    public void UpdatePrompt_ContainsErrorHandling()
    {
        var content = PromptResourceLoader.Load("update");

        Assert.Contains("Important Considerations", content);
        Assert.Contains("Partial Updates", content);
        Assert.Contains("Adding New Content", content);
        Assert.Contains("Fixing Errors", content);
    }

    [Fact]
    public void ServerInstructions_CompactBaseInstructions()
    {
        var baseInstructions = PromptResourceLoader.Load("server-instructions");
        var updateInstructions = PromptResourceLoader.Load("update");

        Assert.True(baseInstructions.Length < 1200, $"Base instructions should be compact: {baseInstructions.Length} chars");
        Assert.True(updateInstructions.Length > 500, $"Update instructions should be detailed: {updateInstructions.Length} chars");
    }

    [Fact]
    public void ServerInstructions_GeneralGuidance()
    {
        var content = PromptResourceLoader.Load("server-instructions");

        Assert.Contains("Available Tools", content);
        Assert.Contains("Best Practices", content);
        Assert.Contains("Error Handling", content);
        Assert.DoesNotContain("Update Workflow", content); // Should not contain update-specific detail
    }

    [Fact]
    public void UpdatePrompt_SpecificToUpdates()
    {
        var content = PromptResourceLoader.Load("update");

        Assert.Contains("update", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("modify", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("changes", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServerInstructions_SeparateFromUpdateDetails()
    {
        var serverInstructions = PromptResourceLoader.Load("server-instructions");
        var updatePrompt = PromptResourceLoader.Load("update");

        Assert.NotEqual(serverInstructions, updatePrompt);
        Assert.DoesNotContain("Update Workflow", serverInstructions);
        Assert.Contains("Update Workflow", updatePrompt);
    }
}