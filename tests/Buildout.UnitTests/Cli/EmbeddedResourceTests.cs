using System.Reflection;
using Buildout.Cli.Skills;
using Xunit;

namespace Buildout.UnitTests.Cli;

public class EmbeddedResourceTests
{
    [Fact]
    public void AllSkillResourcesExist()
    {
        var assembly = typeof(SkillResourceLoader).Assembly;

        var resourceNames = assembly.GetManifestResourceNames();
        var skillResources = resourceNames.Where(n => n.StartsWith("Buildout.Cli.Skills.", StringComparison.Ordinal)).ToList();

        var expectedFiles = new[]
        {
            "Buildout.Cli.Skills.SKILL.md",
            "Buildout.Cli.Skills.create.md",
            "Buildout.Cli.Skills.read.md",
            "Buildout.Cli.Skills.update.md",
            "Buildout.Cli.Skills.delete.md",
            "Buildout.Cli.Skills.restore.md",
            "Buildout.Cli.Skills.search.md",
            "Buildout.Cli.Skills.database-views.md"
        };

        foreach (var expectedFile in expectedFiles)
        {
            Assert.Contains(expectedFile, skillResources);
        }
    }

    [Theory]
    [InlineData("Buildout.Cli.Skills.SKILL.md")]
    [InlineData("Buildout.Cli.Skills.create.md")]
    [InlineData("Buildout.Cli.Skills.read.md")]
    [InlineData("Buildout.Cli.Skills.update.md")]
    [InlineData("Buildout.Cli.Skills.delete.md")]
    [InlineData("Buildout.Cli.Skills.restore.md")]
    [InlineData("Buildout.Cli.Skills.search.md")]
    [InlineData("Buildout.Cli.Skills.database-views.md")]
    public void SkillResourcesHaveNonEmptyContent(string resourceName)
    {
        var assembly = typeof(SkillResourceLoader).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.NotEmpty(content);
        Assert.NotEqual('\0', content[0]);
    }
}