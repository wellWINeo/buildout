using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public class SkillsRemoveSettings : SkillsSettings
{
    [CommandOption("--agent")]
    public required string Agent { get; init; }

    [CommandOption("--local")]
    public bool Local { get; init; }
}