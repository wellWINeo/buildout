namespace Buildout.Core.Buildin.Models;

public sealed record QueryDatabasePage
{
    public string Id { get; init; } = string.Empty;
    public string? Url { get; init; }
    public string? Title { get; init; }
}

public sealed record QueryDatabaseResult
{
    public IReadOnlyList<Dictionary<string, PropertyValue>> Results { get; init; } = [];
    public IReadOnlyList<QueryDatabasePage> Pages { get; init; } = [];
    public bool HasMore { get; init; }
    public string? NextCursor { get; init; }
}
