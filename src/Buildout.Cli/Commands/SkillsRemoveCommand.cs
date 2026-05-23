using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public class SkillsRemoveCommand : Command<SkillsRemoveSettings>
{
    protected override int Execute(CommandContext context, SkillsRemoveSettings settings, CancellationToken cancellationToken)
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

            if (!Directory.Exists(targetDirectory))
            {
                AnsiConsole.MarkupLine($"[yellow]No buildout skill files found at {targetDirectory}.[/]");
                return 0;
            }

            var files = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(targetDirectory, recursive: true);

            AnsiConsole.MarkupLine($"[green]Removed {targetDirectory} ({files} files)[/]");
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
