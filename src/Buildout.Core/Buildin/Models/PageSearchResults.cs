namespace Buildout.Core.Buildin.Models;

public sealed record PageSearchResults
{
    public IReadOnlyList<Page> Results { get; init; } = [];
    public bool HasMore { get; init; }
    public string? NextCursor { get; init; }
}
