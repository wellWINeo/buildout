using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class CreateSettings : CommandSettings
{
    [CommandArgument(0, "<markdown_source>")]
    public string MarkdownSource { get; set; } = string.Empty;

    [CommandOption("--parent")]
    public string ParentId { get; set; } = string.Empty;

    [CommandOption("--title")]
    public string? Title { get; set; }

    [CommandOption("--icon")]
    public string? Icon { get; set; }

    [CommandOption("--cover")]
    public string? CoverUrl { get; set; }

    [CommandOption("--property")]
    public string[] Properties { get; set; } = [];

    [CommandOption("--print")]
    public string PrintMode { get; set; } = "id";
}
