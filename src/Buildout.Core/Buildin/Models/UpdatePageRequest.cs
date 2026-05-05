namespace Buildout.Core.Buildin.Models;

public sealed record UpdatePageRequest
{
    public required Dictionary<string, PropertyValue> Properties { get; init; }
}
