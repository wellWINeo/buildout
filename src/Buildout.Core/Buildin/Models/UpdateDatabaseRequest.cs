namespace Buildout.Core.Buildin.Models;

public sealed record UpdateDatabaseRequest
{
    public IReadOnlyList<RichText>? Title { get; init; }
    public Dictionary<string, PropertySchema>? Properties { get; init; }
}
