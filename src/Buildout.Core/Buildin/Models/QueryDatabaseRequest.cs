namespace Buildout.Core.Buildin.Models;

public sealed record QueryDatabaseRequest
{
    public object? Filter { get; init; }
    public IReadOnlyList<Sort>? Sorts { get; init; }
    public string? StartCursor { get; init; }
    public int? PageSize { get; init; }
}
