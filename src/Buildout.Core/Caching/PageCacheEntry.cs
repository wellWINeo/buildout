using Buildout.Core.Buildin.Models;
using Buildout.Core.Markdown.Internal;

namespace Buildout.Core.Caching;

/// <summary>
/// A cached entry containing page metadata and its block tree.
/// </summary>
public sealed record PageCacheEntry
{
    /// <summary>
    /// Gets the page metadata.
    /// </summary>
    public required Page Page { get; init; }

    /// <summary>
    /// Gets the fully-resolved block tree for the page.
    /// </summary>
    public required List<BlockSubtree> Blocks { get; init; }

    /// <summary>
    /// Gets the timestamp when this entry was cached.
    /// </summary>
    public required DateTimeOffset CachedAt { get; init; }
}