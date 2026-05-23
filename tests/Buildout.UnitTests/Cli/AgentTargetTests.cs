using System.Runtime.InteropServices;
using Buildout.Cli.Commands;
using Xunit;

namespace Buildout.UnitTests.Cli;

public class AgentTargetTests
{
    [Fact]
    public void GetTargetDirectory_Claude_Global_ReturnsCorrectPath()
    {
        var result = AgentTarget.GetTargetDirectory("claude", isLocal: false);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, ".claude", "skills", "buildout-cli");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTargetDirectory_Claude_Local_ReturnsCorrectPath()
    {
        var result = AgentTarget.GetTargetDirectory("claude", isLocal: true);
        var current = Directory.GetCurrentDirectory();
        var expected = Path.Combine(current, ".claude", "skills", "buildout-cli");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTargetDirectory_Opencode_Global_ReturnsCorrectPath()
    {
        var result = AgentTarget.GetTargetDirectory("opencode", isLocal: false);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, ".config", "opencode", "skills", "buildout-cli");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTargetDirectory_Opencode_Local_ReturnsCorrectPath()
    {
        var result = AgentTarget.GetTargetDirectory("opencode", isLocal: true);
        var current = Directory.GetCurrentDirectory();
        var expected = Path.Combine(current, ".opencode", "skills", "buildout-cli");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTargetDirectory_UnsupportedAgent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AgentTarget.GetTargetDirectory("cursor", isLocal: false));
        Assert.Throws<ArgumentException>(() => AgentTarget.GetTargetDirectory("unknown", isLocal: true));
    }
}