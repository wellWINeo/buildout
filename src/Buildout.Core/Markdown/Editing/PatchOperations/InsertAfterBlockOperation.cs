namespace Buildout.Core.Markdown.Editing.PatchOperations;

public sealed record InsertAfterBlockOperation : PatchOperation
{
    public required string Anchor { get; init; }
    public required string Markdown { get; init; }
}
