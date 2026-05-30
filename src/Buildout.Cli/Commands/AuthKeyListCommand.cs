using System.Globalization;
using Buildout.Mcp.Auth;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildout.Cli.Commands;

public sealed class AuthKeyListCommand : AsyncCommand<AuthSettings>
{
    private readonly ITokenStore _tokenStore;
    private readonly IAnsiConsole _console;

    public AuthKeyListCommand(ITokenStore tokenStore, IAnsiConsole console)
    {
        _tokenStore = tokenStore;
        _console = console;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AuthSettings settings, CancellationToken cancellationToken)
    {
        var keys = await _tokenStore.ListBuildinKeysAsync(cancellationToken);

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Created");

        foreach (var key in keys)
        {
            table.AddRow(
                key.Id.ToString(),
                key.Name,
                key.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        _console.Write(table);
        return 0;
    }
}
