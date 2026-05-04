namespace Buildout.Core.Buildin.Models;

public sealed record AppendBlockChildrenResult
{
    public IReadOnlyList<Block> Results { get; init; } = [];
}
