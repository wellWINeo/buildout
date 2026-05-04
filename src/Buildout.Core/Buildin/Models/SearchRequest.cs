namespace Buildout.Core.Buildin.Models;

public sealed record SearchRequest
{
    public string? Query { get; init; }
    public SearchFilter? Filter { get; init; }
    public SearchSort? Sort { get; init; }
    public string? StartCursor { get; init; }
    public int? PageSize { get; init; }
}
