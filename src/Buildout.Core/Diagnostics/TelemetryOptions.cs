namespace Buildout.Core.Diagnostics;

public sealed class TelemetryOptions
{
    public bool Enabled { get; set; }
    public Uri OtlpEndpoint { get; set; } = new("http://localhost:4318");
}