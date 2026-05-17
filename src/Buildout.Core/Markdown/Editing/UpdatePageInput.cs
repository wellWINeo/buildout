using Buildout.Core.Markdown.Editing.PatchOperations;

namespace Buildout.Core.Markdown.Editing;

public sealed record UpdatePageInput
{
    public required string PageId { get; init; }
    public required string Revision { get; init; }
    public required IReadOnlyList<PatchOperation> Operations { get; init; }
    public bool DryRun { get; init; }
    public bool AllowLargeDelete { get; init; }
}
