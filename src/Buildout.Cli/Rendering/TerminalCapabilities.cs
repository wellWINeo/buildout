namespace Buildout.Cli.Rendering;

public sealed class TerminalCapabilities
{
    public bool IsStyledStdout { get; }

    public TerminalCapabilities()
        : this(
            isAnsi: Spectre.Console.AnsiConsole.Profile.Capabilities.Ansi,
            isOutputRedirected: Console.IsOutputRedirected,
            noColorEnv: Environment.GetEnvironmentVariable("NO_COLOR"))
    {
    }

    public TerminalCapabilities(bool isAnsi, bool isOutputRedirected, string? noColorEnv)
    {
        IsStyledStdout = isAnsi && !isOutputRedirected && noColorEnv is null;
    }
}
