using Spectre.Console.Cli;
using System.ComponentModel;

namespace Buildout.Cli.Commands;

public abstract class BuildoutCommandSettings : CommandSettings
{
    [CommandOption("-c|--config")]
    [Description("Path to a JSON configuration file. Overrides the default ~/.config/buildout/config.json.")]
    public string? ConfigPath { get; init; }
}