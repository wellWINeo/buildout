using Buildout.Mcp.Auth;
using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buildout.IntegrationTests.Auth;

public sealed class SqliteTokenStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AdoNetTokenStore _tokenStore;
    private readonly ILogger<AdoNetTokenStore> _logger;

    public SqliteTokenStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_auth_{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_dbPath}";
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AdoNetTokenStore>.Instance;

        RunMigrations(connectionString);

        _tokenStore = new AdoNetTokenStore(connectionString, "sqlite", _logger);
    }

    private static void RunMigrations(string connectionString)
    {
        var serviceProvider = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(builder => builder
                .AddSQLite()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(Buildout.Mcp.Auth.Migrations.Migration_002_CreateAuthTables).Assembly)
                .For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider();

        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task CreateTokenAsync_CreatesTokenWithValidHash()
    {
        var (result, rawToken) = await _tokenStore.CreateTokenAsync("test-token");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("test-token", result.Name);
        Assert.Equal(64, result.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", result.TokenHash);
        Assert.Null(result.BuildinKeyId);
        Assert.Null(result.RevokedAt);
        Assert.StartsWith("mcp_", rawToken);
    }

    [Fact]
    public async Task CreateTokenAsync_MultipleTokens_UniqueIds()
    {
        var (token1, rawToken1) = await _tokenStore.CreateTokenAsync("token-1");
        var (token2, rawToken2) = await _tokenStore.CreateTokenAsync("token-2");

        Assert.NotEqual(token1.Id, token2.Id);
        Assert.NotEqual(rawToken1, rawToken2);
    }

    [Fact]
    public async Task ListTokensAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var tokens = await _tokenStore.ListTokensAsync();

        Assert.Empty(tokens);
    }

    [Fact]
    public async Task ListTokensAsync_AfterCreate_ReturnsCreatedToken()
    {
        var (created, _) = await _tokenStore.CreateTokenAsync("test-token");

        var tokens = await _tokenStore.ListTokensAsync();

        Assert.Single(tokens);
        Assert.Equal(created.Id, tokens[0].Id);
        Assert.Equal("test-token", tokens[0].Name);
    }

    [Fact]
    public async Task ListTokensAsync_MultipleTokens_ReturnsAll()
    {
        await _tokenStore.CreateTokenAsync("token-1");
        await _tokenStore.CreateTokenAsync("token-2");
        await _tokenStore.CreateTokenAsync("token-3");

        var tokens = await _tokenStore.ListTokensAsync();

        Assert.Equal(3, tokens.Count);
    }

    [Fact]
    public async Task RevokeTokenAsync_ExistingToken_SetsRevokedAt()
    {
        var (created, _) = await _tokenStore.CreateTokenAsync("test-token");

        var revoked = await _tokenStore.RevokeTokenAsync(created.Id);

        Assert.True(revoked);

        var tokens = await _tokenStore.ListTokensAsync();
        Assert.NotNull(tokens[0].RevokedAt);
    }

    [Fact]
    public async Task RevokeTokenAsync_NonExistingToken_ReturnsFalse()
    {
        var result = await _tokenStore.RevokeTokenAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_ReturnsTokenRecord()
    {
        var (_, rawToken) = await _tokenStore.CreateTokenAsync("test-token");

        var validated = await _tokenStore.ValidateTokenAsync(rawToken);

        Assert.NotNull(validated);
        Assert.Equal("test-token", validated.Name);
        Assert.Null(validated.RevokedAt);
    }

    [Fact]
    public async Task ValidateTokenAsync_InvalidToken_ReturnsNull()
    {
        var result = await _tokenStore.ValidateTokenAsync("invalid-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_RevokedToken_ReturnsNull()
    {
        var (created, rawToken) = await _tokenStore.CreateTokenAsync("test-token");
        await _tokenStore.RevokeTokenAsync(created.Id);

        var validated = await _tokenStore.ValidateTokenAsync(rawToken);

        Assert.Null(validated);
    }

    [Fact]
    public async Task MapTokenAsync_ValidMapping_SetsBuildinKeyId()
    {
        var (token, _) = await _tokenStore.CreateTokenAsync("test-token");
        var buildinKey = await _tokenStore.CreateBuildinKeyAsync("test-key", "key-value");

        await _tokenStore.MapTokenAsync(token.Id, buildinKey.Id);

        var tokens = await _tokenStore.ListTokensAsync();
        Assert.Equal(buildinKey.Id, tokens[0].BuildinKeyId);
    }

    [Fact]
    public async Task CreateBuildinKeyAsync_CreatesKey()
    {
        var result = await _tokenStore.CreateBuildinKeyAsync("test-key", "key-value");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("test-key", result.Name);
        Assert.Equal("key-value", result.KeyValue);
    }

    [Fact]
    public async Task ListBuildinKeysAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var keys = await _tokenStore.ListBuildinKeysAsync();

        Assert.Empty(keys);
    }

    [Fact]
    public async Task ListBuildinKeysAsync_AfterCreate_ReturnsCreatedKey()
    {
        var created = await _tokenStore.CreateBuildinKeyAsync("test-key", "key-value");

        var keys = await _tokenStore.ListBuildinKeysAsync();

        Assert.Single(keys);
        Assert.Equal(created.Id, keys[0].Id);
    }
}