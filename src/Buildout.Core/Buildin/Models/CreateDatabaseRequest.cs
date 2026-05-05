namespace Buildout.Core.Buildin.Models;

public sealed record CreateDatabaseRequest
{
    public required Parent Parent { get; init; }
    public required IReadOnlyList<RichText> Title { get; init; }
    public required Dictionary<string, PropertySchema> Properties { get; init; }
}
