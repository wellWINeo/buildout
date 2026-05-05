using Buildout.Cli.Rendering;
using Xunit;

namespace Buildout.UnitTests.Cli;

public class TerminalCapabilitiesTests
{
    [Fact]
    public void IsStyledStdout_True_WhenAnsiAndNotRedirectedAndNoNoColor()
    {
        var caps = new TerminalCapabilities(
            isAnsi: true,
            isOutputRedirected: false,
            noColorEnv: null);

        Assert.True(caps.IsStyledStdout);
    }

    [Fact]
    public void IsStyledStdout_False_WhenNotAnsi()
    {
        var caps = new TerminalCapabilities(
            isAnsi: false,
            isOutputRedirected: false,
            noColorEnv: null);

        Assert.False(caps.IsStyledStdout);
    }

    [Fact]
    public void IsStyledStdout_False_WhenOutputRedirected()
    {
        var caps = new TerminalCapabilities(
            isAnsi: true,
            isOutputRedirected: true,
            noColorEnv: null);

        Assert.False(caps.IsStyledStdout);
    }

    [Fact]
    public void IsStyledStdout_False_WhenNoColorSet()
    {
        var caps = new TerminalCapabilities(
            isAnsi: true,
            isOutputRedirected: false,
            noColorEnv: "1");

        Assert.False(caps.IsStyledStdout);
    }

    [Fact]
    public void IsStyledStdout_False_WhenNoColorEmpty()
    {
        var caps = new TerminalCapabilities(
            isAnsi: true,
            isOutputRedirected: false,
            noColorEnv: "");

        Assert.False(caps.IsStyledStdout);
    }

    [Fact]
    public void IsStyledStdout_False_WhenAllConditionsNegative()
    {
        var caps = new TerminalCapabilities(
            isAnsi: false,
            isOutputRedirected: true,
            noColorEnv: "1");

        Assert.False(caps.IsStyledStdout);
    }
}
