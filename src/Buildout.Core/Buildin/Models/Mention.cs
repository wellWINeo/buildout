namespace Buildout.Core.Buildin.Models;

public abstract record Mention;

public sealed record PageMention : Mention
{
    public required string PageId { get; init; }
}

public sealed record DatabaseMention : Mention
{
    public required string DatabaseId { get; init; }
}

public sealed record UserMention : Mention
{
    public required string UserId { get; init; }
    public string? DisplayName { get; init; }
}

public sealed record DateMention : Mention
{
    public required string Start { get; init; }
    public string? End { get; init; }
}
