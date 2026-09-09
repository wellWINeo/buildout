namespace Buildout.Core.Buildin.Models;

/// <summary>A page read paired with the opaque service ETag used for edit preflight.</summary>
public sealed record VersionedPage
{
    public required Page Page { get; init; }
    public string? ETag { get; init; }
}
