namespace Buildout.Core.Buildin.Models;

public sealed record BlockChildrenQuery
{
    public string? StartCursor { get; init; }
    public int? PageSize { get; init; }
}
