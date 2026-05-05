namespace Buildout.Core.Buildin;

public sealed class BuildinClientOptions
{
    public Uri BaseUrl { get; set; } = new("https://api.buildin.ai/");
    public string BotToken { get; set; } = string.Empty;
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool UnsafeAllowInsecure { get; set; }
}
