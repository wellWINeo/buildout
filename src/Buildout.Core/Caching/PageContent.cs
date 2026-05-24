using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Caching;

/// <summary>
/// Page content returned by <see cref="IPageContentProvider"/>.
/// </summary>
public sealed record PageContent
{
    /// <summary>
    /// Gets the page metadata.
    /// </summary>
    public required Page Page { get; init; }

    /// <summary>
    /// Gets the fully-resolved block tree for the page.
    /// </summary>
    public required List<BlockSubtree> Blocks { get; init; }
}