namespace Buildout.Core.Markdown.Editing.PatchOperations;

public sealed record AppendSectionOperation : PatchOperation
{
    public string? Anchor { get; init; }
    public required string Markdown { get; init; }
}
