using System.ComponentModel;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class TreeSettings : BuildoutCommandSettings
{
    [CommandArgument(0, "<page_id>")]
    [Description("UUID of the root page or database.")]
    public string PageId { get; init; } = string.Empty;

    [CommandOption("--format")]
    [Description("Output format: ascii or json. Default: ascii.")]
    public string Format { get; init; } = "ascii";

    [CommandOption("--depth")]
    [Description("Number of descendant levels to traverse (1–7). Default: 3.")]
    public int Depth { get; init; } = 3;
}
