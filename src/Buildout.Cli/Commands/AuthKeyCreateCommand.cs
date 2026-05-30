using Buildout.Mcp.Auth;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Buildout.Cli.Commands;

public sealed class AuthKeyCreateSettings : AuthSettings
{
    [CommandArgument(0, "<name>")]
    [Description("Human-readable name for this key")]
    public string Name { get; set; } = string.Empty;

    [CommandArgument(1, "<key-value>")]
    [Description("The Buildin Bot API key value")]
    public string KeyValue { get; set; } = string.Empty;
}

public sealed class AuthKeyCreateCommand : AsyncCommand<AuthKeyCreateSettings>
{
    private readonly ITokenStore _tokenStore;
    private readonly IAnsiConsole _console;

    public AuthKeyCreateCommand(ITokenStore tokenStore, IAnsiConsole console)
    {
        _tokenStore = tokenStore;
        _console = console;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AuthKeyCreateSettings settings, CancellationToken cancellationToken)
    {
        var key = await _tokenStore.CreateBuildinKeyAsync(settings.Name, settings.KeyValue, cancellationToken);
        _console.WriteLine(key.Id.ToString());
        return 0;
    }
}
