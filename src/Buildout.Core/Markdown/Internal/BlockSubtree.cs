using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Internal;

public sealed record BlockSubtree
{
    public Block Block { get; init; } = null!;
    public IReadOnlyList<BlockSubtree> Children { get; init; } = [];
}
