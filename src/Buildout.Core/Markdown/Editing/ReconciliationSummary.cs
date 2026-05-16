namespace Buildout.Core.Markdown.Editing;

public sealed record ReconciliationSummary
{
    public required int PreservedBlocks { get; init; }
    public required int UpdatedBlocks { get; init; }
    public required int NewBlocks { get; init; }
    public required int DeletedBlocks { get; init; }
    public required int AmbiguousMatches { get; init; }
    public required string NewRevision { get; init; }
    public string? PostEditMarkdown { get; init; }
}
