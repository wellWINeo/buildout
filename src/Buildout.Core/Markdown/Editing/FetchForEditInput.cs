namespace Buildout.Core.Markdown.Editing;

public sealed record FetchForEditInput
{
    public required string PageId { get; init; }
}
