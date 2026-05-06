using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Search;

public enum SearchObjectType
{
    Page,
    Database
}

public sealed record SearchMatch
{
    public required string PageId { get; init; }
    public required SearchObjectType ObjectType { get; init; }
    public required string DisplayTitle { get; init; }
    public Parent? Parent { get; init; }
    public bool Archived { get; init; }
}
