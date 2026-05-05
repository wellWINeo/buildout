namespace Buildout.Core.Buildin.Models;

public sealed record UpdateBlockRequest
{
    public required string Type { get; init; }
    public IReadOnlyList<RichText>? RichTextContent { get; init; }
    public string? Language { get; init; }
    public string? Url { get; init; }
    public bool? Checked { get; init; }
    public bool? Archived { get; init; }
}
