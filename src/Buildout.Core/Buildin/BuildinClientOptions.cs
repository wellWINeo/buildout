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
    /// Gets or sets the primary opaque bearer token for authentication.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deprecated compatibility token setting.
    /// </summary>
    [Obsolete("Use AccessToken instead.")]
    public string? BotToken { get; set; }

    /// <summary>
    /// Gets or sets the HTTP-related configuration options.
    /// </summary>
    public HttpOptions Http { get; set; } = new();
}
