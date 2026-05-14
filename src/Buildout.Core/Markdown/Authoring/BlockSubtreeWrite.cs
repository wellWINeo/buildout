using Buildout.Core.Buildin.Models;

namespace Buildout.Core.Markdown.Authoring;

public sealed record BlockSubtreeWrite
{
    public required Block Block { get; init; }
    public required IReadOnlyList<BlockSubtreeWrite> Children { get; init; }
}
