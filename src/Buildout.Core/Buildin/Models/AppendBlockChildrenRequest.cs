namespace Buildout.Core.Buildin.Models;

public sealed record AppendBlockChildrenRequest
{
    public required IReadOnlyList<Block> Children { get; init; }
}
