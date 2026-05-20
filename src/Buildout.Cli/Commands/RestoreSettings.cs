using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class RestoreSettings : CommandSettings
{
    [CommandArgument(0, "<page_id>")]
    public string PageId { get; init; } = string.Empty;

    [CommandOption("--print")]
    public string PrintMode { get; init; } = "summary";
}
