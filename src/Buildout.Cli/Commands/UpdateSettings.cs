using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class UpdateSettings : CommandSettings
{
    [CommandOption("--page")]
    public string PageId { get; init; } = string.Empty;

    [CommandOption("--revision")]
    public string Revision { get; init; } = string.Empty;

    [CommandOption("--ops")]
    public string OpsSource { get; init; } = string.Empty;

    [CommandOption("--dry-run")]
    public bool DryRun { get; init; }

    [CommandOption("--allow-large-delete")]
    public bool AllowLargeDelete { get; init; }

    [CommandOption("--print")]
    public string PrintMode { get; init; } = "summary";
}
