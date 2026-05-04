namespace Buildout.Core.Buildin.Models;

public sealed record PaginatedList<T>
{
    public IReadOnlyList<T> Results { get; init; } = [];
    public bool HasMore { get; init; }
    public string? NextCursor { get; init; }
}
