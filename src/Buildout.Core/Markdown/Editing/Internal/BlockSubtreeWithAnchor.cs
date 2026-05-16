using Buildout.Core.Markdown.Authoring;

namespace Buildout.Core.Markdown.Editing.Internal;

public sealed record BlockSubtreeWithAnchor
{
    public string? AnchorId { get; init; }
    public required AnchorKind AnchorKind { get; init; }
    public BlockSubtreeWrite? Block { get; init; }
    public required IReadOnlyList<BlockSubtreeWithAnchor> Children { get; init; }
}
