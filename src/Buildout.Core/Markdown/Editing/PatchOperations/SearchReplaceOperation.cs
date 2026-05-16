namespace Buildout.Core.Markdown.Editing.PatchOperations;

public sealed record SearchReplaceOperation : PatchOperation
{
    public required string OldStr { get; init; }
    public required string NewStr { get; init; }
}
