namespace Buildout.Core.Auth;

public class AuthOptions
{
    public AuthMode Mode { get; set; } = AuthMode.None;
    public string? Provider { get; set; }
    public string? SqlitePath { get; set; }
    public string? ConnectionString { get; set; }
}