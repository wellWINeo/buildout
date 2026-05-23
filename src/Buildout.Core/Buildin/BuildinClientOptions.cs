namespace Buildout.Core.Buildin;

/// <summary>
/// Configuration options for the Buildin client.
/// </summary>
/// <remarks>
/// For configuration documentation, see docs/configuration.md
/// </remarks>
public sealed class BuildinClientOptions
{
    /// <summary>
    /// Gets or sets the base URL for the Buildin API.
    /// </summary>
    public Uri BaseUrl { get; set; } = new("https://api.buildin.ai/");

    /// <summary>
    /// Gets or sets the bot token for authentication.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP timeout for API requests.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to allow insecure HTTP connections.
    /// </summary>
    public bool UnsafeAllowInsecure { get; set; }
}
