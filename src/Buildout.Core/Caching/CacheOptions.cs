namespace Buildout.Core.Caching;

/// <summary>
/// Configuration options for the page read cache.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Gets or sets whether the cache is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of entries in the cache.
    /// </summary>
    /// <remarks>
    /// Must be greater than 0 when <see cref="Enabled"/> is true.
    /// </remarks>
    public int MaxEntries { get; set; } = 50;
}