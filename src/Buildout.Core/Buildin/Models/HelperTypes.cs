namespace Buildout.Core.Buildin.Models;

public sealed record SelectOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
}

public sealed record DateRange
{
    public string? Start { get; init; }
    public string? End { get; init; }
}

public sealed record FileObject
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Url { get; init; }
}
