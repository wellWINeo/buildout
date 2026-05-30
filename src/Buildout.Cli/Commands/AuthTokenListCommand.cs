using System.Globalization;
using Buildout.Mcp.Auth;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class AuthTokenListCommand : AsyncCommand<AuthSettings>
{
    private readonly ITokenStore _tokenStore;
    private readonly IAnsiConsole _console;

    public AuthTokenListCommand(ITokenStore tokenStore, IAnsiConsole console)
    {
        _tokenStore = tokenStore;
        _console = console;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AuthSettings settings, CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.ListTokensAsync(cancellationToken);

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Status");
        table.AddColumn("Created");

        foreach (var token in tokens)
        {
            var status = token.RevokedAt.HasValue ? "revoked" : "active";
            table.AddRow(
                token.Id.ToString(),
                token.Name,
                status,
                token.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        _console.Write(table);
        return 0;
    }
}
