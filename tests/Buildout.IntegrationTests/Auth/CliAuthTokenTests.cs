using Buildout.Cli.Commands;
using Buildout.Mcp.Auth;
using Buildout.Mcp.Auth.Migrations;
using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Xunit;

namespace Buildout.IntegrationTests.Auth;

public sealed class CliAuthTokenTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public CliAuthTokenTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_cli_auth_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
        RunMigrations(_connectionString);
    }

    private static void RunMigrations(string connectionString)
    {
        var sp = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(b => b.AddSQLite()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(Migration_002_CreateAuthTables).Assembly)
                .For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider();
        sp.GetRequiredService<IMigrationRunner>().MigrateUp();
    }

    private CommandApp CreateApp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var cs = _connectionString;
        services.AddSingleton<ITokenStore>(sp =>
            new AdoNetTokenStore(cs, "sqlite",
                sp.GetRequiredService<ILogger<AdoNetTokenStore>>()));
        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);

        var registrar = new SimpleRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
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
        return app;
    }

    [Fact]
    public async Task AuthTokenCreate_ReturnsZeroAndPersistsToken()
    {
        var exitCode = await CreateApp().RunAsync(["auth", "token", "create", "test-client"]);

        Assert.Equal(0, exitCode);
        var store = new AdoNetTokenStore(_connectionString, "sqlite",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdoNetTokenStore>.Instance);
        var tokens = await store.ListTokensAsync();
        Assert.Single(tokens);
        Assert.Equal("test-client", tokens[0].Name);
    }

    [Fact]
    public async Task AuthTokenList_EmptyDatabase_ReturnsZeroExitCode()
    {
        var exitCode = await CreateApp().RunAsync(["auth", "token", "list"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task AuthTokenList_AfterCreate_ReturnsZeroExitCode()
    {
        await CreateApp().RunAsync(["auth", "token", "create", "my-client"]);

        var exitCode = await CreateApp().RunAsync(["auth", "token", "list"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task AuthTokenRevoke_ExistingToken_ReturnsZeroAndRevokesToken()
    {
        var store = new AdoNetTokenStore(_connectionString, "sqlite",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdoNetTokenStore>.Instance);
        var (record, _) = await store.CreateTokenAsync("revoke-test");

        var exitCode = await CreateApp().RunAsync(["auth", "token", "revoke", record.Id.ToString()]);

        Assert.Equal(0, exitCode);
        var tokens = await store.ListTokensAsync();
        Assert.NotNull(tokens[0].RevokedAt);
    }

    [Fact]
    public async Task AuthTokenRevoke_NonExistentId_ReturnsNonZero()
    {
        var exitCode = await CreateApp().RunAsync(["auth", "token", "revoke", Guid.NewGuid().ToString()]);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task AuthKeyCreate_ReturnsZeroAndPersistsKey()
    {
        var exitCode = await CreateApp().RunAsync(["auth", "key", "create", "prod-key", "sk-buildin-abc123"]);

        Assert.Equal(0, exitCode);
        var store = new AdoNetTokenStore(_connectionString, "sqlite",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdoNetTokenStore>.Instance);
        var keys = await store.ListBuildinKeysAsync();
        Assert.Single(keys);
        Assert.Equal("prod-key", keys[0].Name);
    }

    [Fact]
    public async Task AuthKeyList_EmptyDatabase_ReturnsZeroExitCode()
    {
        var exitCode = await CreateApp().RunAsync(["auth", "key", "list"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task AuthTokenMap_ValidIds_ReturnsZeroAndPersistsMapping()
    {
        var store = new AdoNetTokenStore(_connectionString, "sqlite",
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdoNetTokenStore>.Instance);
        var (token, _) = await store.CreateTokenAsync("map-test");
        var key = await store.CreateBuildinKeyAsync("map-key", "sk-test");

        var exitCode = await CreateApp().RunAsync([
            "auth", "token", "map", token.Id.ToString(), key.Id.ToString()]);

        Assert.Equal(0, exitCode);
        var tokens = await store.ListTokensAsync();
        Assert.Equal(key.Id, tokens[0].BuildinKeyId);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private sealed class SimpleRegistrar : ITypeRegistrar
    {
        private readonly IServiceCollection _services;
        public SimpleRegistrar(IServiceCollection services) => _services = services;
        public void Register(Type service, Type impl) => _services.AddSingleton(service, impl);
        public void RegisterInstance(Type service, object impl) => _services.AddSingleton(service, impl);
        public void RegisterLazy(Type service, Func<object> factory) => _services.AddSingleton(service, _ => factory());
        public ITypeResolver Build() => new SimpleResolver(_services.BuildServiceProvider());
    }

    private sealed class SimpleResolver : ITypeResolver
    {
        private readonly IServiceProvider _provider;
        public SimpleResolver(IServiceProvider provider) => _provider = provider;
        public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);
    }
}
