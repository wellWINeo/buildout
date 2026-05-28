using Buildout.Mcp.Auth;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Buildout.Cli.Commands;

public sealed class AuthTokenCreateSettings : AuthSettings
{
    [CommandArgument(0, "<name>")]
    [Description("Human-readable name for this token")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AuthTokenCreateCommand : AsyncCommand<AuthTokenCreateSettings>
{
    private readonly ITokenStore _tokenStore;
    private readonly IAnsiConsole _console;

    public AuthTokenCreateCommand(ITokenStore tokenStore, IAnsiConsole console)
    {
        _tokenStore = tokenStore;
        _console = console;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AuthTokenCreateSettings settings, CancellationToken cancellationToken)
    {
        var (_, rawToken) = await _tokenStore.CreateTokenAsync(settings.Name, cancellationToken);
        _console.WriteLine(rawToken);
        return 0;
    }
}
