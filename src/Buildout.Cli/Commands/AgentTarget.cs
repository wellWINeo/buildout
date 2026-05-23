namespace Buildout.Cli.Commands;

public static class AgentTarget
{
    public static string GetTargetDirectory(string agent, bool isLocal)
    {
        return Path.Combine(GetAgentRootDirectory(agent, isLocal), "skills", "buildout-cli");
    }

    public static string GetAgentRootDirectory(string agent, bool isLocal)
    {
        var agentNormalized = agent.ToLowerInvariant();
        if (agentNormalized != "claude" && agentNormalized != "opencode")
            throw new ArgumentException($"Unsupported agent '{agent}'. Supported agents: claude, opencode.", nameof(agent));

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var currentDirectory = Directory.GetCurrentDirectory();

        if (agentNormalized == "claude")
            return isLocal
                ? Path.Combine(currentDirectory, ".claude")
                : Path.Combine(homeDirectory, ".claude");

        return isLocal
            ? Path.Combine(currentDirectory, ".opencode")
            : Path.Combine(homeDirectory, ".config", "opencode");
    }
}
