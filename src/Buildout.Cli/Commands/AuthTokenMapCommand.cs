using Buildout.Mcp.Auth;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Buildout.Cli.Commands;

public sealed class AuthTokenMapSettings : AuthSettings
{
    [CommandArgument(0, "<token-id>")]
    [Description("MCP token ID to map")]
    public string TokenId { get; set; } = string.Empty;

    [CommandArgument(1, "<key-id>")]
    [Description("Buildin key ID to map to")]
    public string KeyId { get; set; } = string.Empty;
}

public sealed class AuthTokenMapCommand : AsyncCommand<AuthTokenMapSettings>
{
    private readonly ITokenStore _tokenStore;
    private readonly IAnsiConsole _console;

    public AuthTokenMapCommand(ITokenStore tokenStore, IAnsiConsole console)
    {
        _tokenStore = tokenStore;
        _console = console;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AuthTokenMapSettings settings, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(settings.TokenId, out var tokenId))
        {
            _console.WriteLine($"Invalid token ID: {settings.TokenId}");
            return 2;
        }

        if (!Guid.TryParse(settings.KeyId, out var keyId))
        {
            _console.WriteLine($"Invalid key ID: {settings.KeyId}");
            return 2;
        }

        await _tokenStore.MapTokenAsync(tokenId, keyId, cancellationToken);
        _console.WriteLine("Token mapped to key.");
        return 0;
    }
}
