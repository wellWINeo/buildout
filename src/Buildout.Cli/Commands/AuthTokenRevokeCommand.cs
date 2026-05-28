using Buildout.Mcp.Auth;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Buildout.Cli.Commands;

public sealed class AuthTokenRevokeSettings : AuthSettings
{
    [CommandArgument(0, "<id>")]
    [Description("Token ID to revoke")]
    public string Id { get; set; } = string.Empty;
}

public sealed class AuthTokenRevokeCommand : AsyncCommand<AuthTokenRevokeSettings>
{
    private readonly ITokenStore _tokenStore;
    private readonly IAnsiConsole _console;

    public AuthTokenRevokeCommand(ITokenStore tokenStore, IAnsiConsole console)
    {
        _tokenStore = tokenStore;
        _console = console;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AuthTokenRevokeSettings settings, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(settings.Id, out var tokenId))
        {
            _console.WriteLine($"Invalid token ID: {settings.Id}");
            return 2;
        }

        var revoked = await _tokenStore.RevokeTokenAsync(tokenId, cancellationToken);
        if (!revoked)
        {
            _console.WriteLine($"Token not found or already revoked: {settings.Id}");
            return 3;
        }

        _console.WriteLine("Token revoked.");
        return 0;
    }
}
