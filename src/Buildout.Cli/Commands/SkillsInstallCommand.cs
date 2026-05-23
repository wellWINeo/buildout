using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public class SkillsInstallCommand : AsyncCommand<SkillsInstallSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SkillsInstallSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var agentNormalized = settings.Agent.ToLowerInvariant();
            if (agentNormalized != "claude" && agentNormalized != "opencode")
            {
                AnsiConsole.MarkupLine($"[red]Error: Unsupported agent '{settings.Agent}'. Supported agents: claude, opencode.[/]");
                return 2;
            }

            var targetDirectory = AgentTarget.GetTargetDirectory(settings.Agent, settings.Local);

            if (!settings.Local)
            {
                var agentRoot = AgentTarget.GetAgentRootDirectory(settings.Agent, isLocal: false);
                if (!Directory.Exists(agentRoot))
                {
                    AnsiConsole.MarkupLine($"[red]Error: Global config directory {agentRoot} does not exist. Create it first or use --local.[/]");
                    return 2;
                }
            }

            if (Directory.Exists(targetDirectory) && !settings.Overwrite)
            {
                var files = Directory.GetFiles(targetDirectory);
                if (files.Length > 0)
                {
                    AnsiConsole.MarkupLine($"[red]Error: Skill files already exist at {targetDirectory}. Use --overwrite to replace them.[/]");
                    return 2;
                }
            }

            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            var skills = Skills.SkillResourceLoader.LoadAll().ToList();

            foreach (var (fileName, content) in skills)
            {
                var filePath = Path.Combine(targetDirectory, fileName);
                await File.WriteAllTextAsync(filePath, content, cancellationToken);
            }

            AnsiConsole.MarkupLine($"[green]Installed {skills.Count} skill files to {targetDirectory}[/]");
            return 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: Permission denied. {ex.Message}[/]");
            return 2;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 2;
        }
    }
}
