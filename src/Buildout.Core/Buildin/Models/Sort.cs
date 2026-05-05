namespace Buildout.Core.Buildin.Models;

public sealed record Sort
{
    public string? Property { get; init; }
    public string? Direction { get; init; }
    public string? Timestamp { get; init; }
}

public sealed record SearchFilter
{
    public string? Value { get; init; }
    public string? Property { get; init; }
}

public sealed record SearchSort
{
    public string? Direction { get; init; }
    public string? Timestamp { get; init; }
}
