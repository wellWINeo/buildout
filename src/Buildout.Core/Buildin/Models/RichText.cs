namespace Buildout.Core.Buildin.Models;

public sealed record RichText
{
    public required string Type { get; init; }
    public required string Content { get; init; }
    public string? Href { get; init; }
    public Annotations? Annotations { get; init; }
}
