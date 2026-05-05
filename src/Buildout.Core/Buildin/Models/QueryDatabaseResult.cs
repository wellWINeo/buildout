namespace Buildout.Core.Buildin.Models;

public sealed record QueryDatabaseResult
{
    public IReadOnlyList<Dictionary<string, PropertyValue>> Results { get; init; } = [];
    public bool HasMore { get; init; }
    public string? NextCursor { get; init; }
}
