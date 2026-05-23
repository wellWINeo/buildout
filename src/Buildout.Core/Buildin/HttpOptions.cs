namespace Buildout.Core.Buildin;

/// <summary>
/// HTTP-related configuration options.
/// </summary>
public sealed class HttpOptions
{
    /// <summary>
    /// Gets or sets the HTTP timeout for API requests.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to allow insecure HTTP connections.
    /// </summary>
    public bool UnsafeAllowInsecure { get; set; }
}
