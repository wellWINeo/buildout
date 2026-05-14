namespace Buildout.Core.Markdown.Authoring;

public enum CreatePagePrintMode
{
    Id,
    Json,
    None
}

public sealed record CreatePageInput
{
    public required string ParentId { get; init; }
    public required string Markdown { get; init; }
    public string? Title { get; init; }
    public string? Icon { get; init; }
    public string? CoverUrl { get; init; }
    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}
