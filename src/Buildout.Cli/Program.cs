using Buildout.Cli.Commands;
using Buildout.Cli.Rendering;
using Buildout.Configuration;
using Buildout.Core.Buildin;
using Buildout.Core.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

// Extract --config/-c before Spectre sees the args; Spectre doesn't know about this flag.
string? configPath = null;
var spectreArgs = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if ((args[i] is "--config" or "-c") && i + 1 < args.Length)
        configPath = args[++i];
    else if (args[i].StartsWith("--config=", StringComparison.Ordinal))
        configPath = args[i]["--config=".Length..];
    else if (args[i].StartsWith("-c=", StringComparison.Ordinal))
        configPath = args[i]["-c=".Length..];
    else
        spectreArgs.Add(args[i]);
}

try
{
    var config = BuildoutConfiguration.Build(configPath);

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddBuildinClient(config);
    services.AddBuildoutCore(config);
    services.AddSingleton<Spectre.Console.IAnsiConsole>(_ => Spectre.Console.AnsiConsole.Console);
    services.AddSingleton<TerminalCapabilities>();
    services.AddSingleton<MarkdownTerminalRenderer>();
    services.AddSingleton<SearchResultStyledRenderer>();

    var authProvider = config.GetValue<string>("Auth:Provider")?.ToLowerInvariant();
    if (authProvider == "sqlite")
    {
        var sqlitePath = config.GetValue<string>("Auth:SqlitePath");
        var authCs = $"Data Source={sqlitePath}";
        services.AddSingleton<Buildout.Mcp.Auth.ITokenStore>(sp =>
            new Buildout.Mcp.Auth.AdoNetTokenStore(authCs, "sqlite",
                sp.GetRequiredService<ILogger<Buildout.Mcp.Auth.AdoNetTokenStore>>()));
    }
    else if (authProvider == "postgresql")
    {
        var authCs = config.GetValue<string>("Auth:ConnectionString")!;
        services.AddSingleton<Buildout.Mcp.Auth.ITokenStore>(sp =>
            new Buildout.Mcp.Auth.AdoNetTokenStore(authCs, "postgresql",
                sp.GetRequiredService<ILogger<Buildout.Mcp.Auth.AdoNetTokenStore>>()));
    }

    var registrar = new TypeRegistrar(services);
    var app = new CommandApp(registrar);

    app.Configure(config =>
    {
        config.AddCommand<CreateCommand>("create");
        config.AddCommand<GetCommand>("get");
        config.AddCommand<SearchCommand>("search");
        config.AddCommand<UpdateCommand>("update");
        config.AddCommand<DeleteCommand>("delete");
        config.AddCommand<RestoreCommand>("restore");
        config.AddCommand<TreeCommand>("tree");
        config.AddBranch<DbSettings>("db", db =>
        {
            db.AddCommand<DbViewCommand>("view");
        });
        config.AddBranch<SkillsSettings>("skills", skills =>
        {
            skills.AddCommand<SkillsInstallCommand>("install");
            skills.AddCommand<SkillsRemoveCommand>("remove");
        });
        config.AddBranch<AuthSettings>("auth", auth =>
        {
            auth.AddBranch<AuthSettings>("token", token =>
            {
                token.AddCommand<AuthTokenCreateCommand>("create");
                token.AddCommand<AuthTokenListCommand>("list");
                token.AddCommand<AuthTokenRevokeCommand>("revoke");
                token.AddCommand<AuthTokenMapCommand>("map");
            });
            auth.AddBranch<AuthSettings>("key", key =>
            {
                key.AddCommand<AuthKeyCreateCommand>("create");
                key.AddCommand<AuthKeyListCommand>("list");
            });
        });
    });

    await app.RunAsync(spectreArgs.ToArray());
}
catch (BuildoutConfigurationException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    Environment.Exit(1);
}

internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;
    public TypeRegistrar(IServiceCollection services) => _services = services;
    public void Register(Type service, Type implementation) => _services.AddSingleton(service, implementation);
    public void RegisterInstance(Type service, object implementation) => _services.AddSingleton(service, implementation);
    public void RegisterLazy(Type service, Func<object> factory) => _services.AddSingleton(service, _ => factory());
    public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());
}

internal sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;
    public TypeResolver(IServiceProvider provider) => _provider = provider;
    public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
}
