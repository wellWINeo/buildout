namespace Buildout.Core.Buildin.Models;

public sealed record Page
{
    public required string Id { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? LastEditedAt { get; init; }
    public UserMe? CreatedBy { get; init; }
    public UserMe? LastEditedBy { get; init; }
    public string? Cover { get; init; }
    public Icon? Icon { get; init; }
    public Parent? Parent { get; init; }
    public Dictionary<string, PropertyValue>? Properties { get; init; }
    public IReadOnlyList<RichText>? Title { get; init; }
    public bool Archived { get; init; }
    public string? Url { get; init; }
}
