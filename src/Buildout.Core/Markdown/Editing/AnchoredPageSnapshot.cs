namespace Buildout.Core.Markdown.Editing;

public sealed record AnchoredPageSnapshot
{
    public required string Markdown { get; init; }
    public required string Revision { get; init; }
    public required IReadOnlyList<string> UnknownBlockIds { get; init; }
}
