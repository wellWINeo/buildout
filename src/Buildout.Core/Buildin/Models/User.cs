namespace Buildout.Core.Buildin.Models;

public sealed record User
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Type { get; init; }
}
