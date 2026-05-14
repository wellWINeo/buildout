namespace Buildout.Core.Markdown.Authoring;

public sealed record AuthoredDocument
{
    public string? Title { get; init; }
    public required IReadOnlyList<BlockSubtreeWrite> Body { get; init; }
}
