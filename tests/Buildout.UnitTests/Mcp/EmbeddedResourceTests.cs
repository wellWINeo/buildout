using System.Reflection;
using Buildout.Mcp.Prompts;
using Xunit;

namespace Buildout.UnitTests.Mcp;

public class EmbeddedResourceTests
{
    [Fact]
    public void AllPromptResourcesExist()
    {
        var assembly = typeof(PromptResourceLoader).Assembly;

        var resourceNames = assembly.GetManifestResourceNames();
        var promptResources = resourceNames.Where(n => n.StartsWith("Buildout.Mcp.Prompts.", StringComparison.Ordinal)).ToList();

        var expectedFiles = new[]
        {
            "Buildout.Mcp.Prompts.server-instructions.md",
            "Buildout.Mcp.Prompts.update.md"
        };

        foreach (var expectedFile in expectedFiles)
        {
            Assert.Contains(expectedFile, promptResources);
        }
    }

    [Fact]
    public void OnlyPromptResourcesExist()
    {
        var assembly = Assembly.GetAssembly(typeof(Buildout.Mcp.Prompts.PromptResourceLoader));
        Assert.NotNull(assembly);

        var resourceNames = assembly.GetManifestResourceNames();
        var promptResources = resourceNames.Where(n => n.StartsWith("Buildout.Mcp.Prompts.", StringComparison.Ordinal)).ToList();

        var expectedCount = 2;
        Assert.Equal(expectedCount, promptResources.Count);
    }

    [Theory]
    [InlineData("Buildout.Mcp.Prompts.server-instructions.md")]
    [InlineData("Buildout.Mcp.Prompts.update.md")]
    public void PromptResourcesHaveNonEmptyContent(string resourceName)
    {
        var assembly = typeof(PromptResourceLoader).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.NotEmpty(content);
        Assert.NotEqual('\0', content[0]);
    }
}