using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public class SkillsInstallSettings : SkillsSettings
{
    [CommandOption("--agent")]
    public required string Agent { get; init; }

    [CommandOption("--local")]
    public bool Local { get; init; }

    [CommandOption("--overwrite")]
    public bool Overwrite { get; init; }
}