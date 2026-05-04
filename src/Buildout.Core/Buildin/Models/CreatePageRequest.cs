namespace Buildout.Core.Buildin.Models;

public sealed record CreatePageRequest
{
    public required Parent Parent { get; init; }
    public required Dictionary<string, PropertyValue> Properties { get; init; }
    public IReadOnlyList<Block>? Children { get; init; }
}
