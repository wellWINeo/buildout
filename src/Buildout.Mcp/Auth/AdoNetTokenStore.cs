using Buildout.Core.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Buildout.Mcp.Auth;

public sealed class AdoNetTokenStore : ITokenStore
{
    private static readonly Action<ILogger, string, Exception?> s_failedToWrite =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, "TokenStoreWriteFailed"),
            "Failed to write token operation: {Operation}");

    private readonly string _connectionString;
    private readonly string _provider;
    private readonly ILogger<AdoNetTokenStore> _logger;

    public AdoNetTokenStore(string connectionString, string provider, ILogger<AdoNetTokenStore> logger)
    {
        _connectionString = connectionString;
        _provider = provider;
        _logger = logger;
    }

    public async Task<(McpTokenRecord record, string rawToken)> CreateTokenAsync(string name, CancellationToken ct = default)
    {
        var rawToken = GenerateToken();
        var tokenHash = TokenHasher.Hash(rawToken);
        var tokenId = Guid.NewGuid();

        if (_provider == "sqlite")
        {
            await CreateTokenSqliteAsync(tokenId, name, tokenHash, ct);
        }
        else if (_provider == "postgresql")
        {
            await CreateTokenPostgresAsync(tokenId, name, tokenHash, ct);
        }

        var record = new McpTokenRecord(tokenId, name, tokenHash, null, DateTimeOffset.UtcNow, null);
        return (record, rawToken);
    }

    public async Task<IReadOnlyList<McpTokenRecord>> ListTokensAsync(CancellationToken ct = default)
    {
        if (_provider == "sqlite")
        {
            return await ListTokensSqliteAsync(ct);
        }
        else if (_provider == "postgresql")
        {
            return await ListTokensPostgresAsync(ct);
        }

        return Array.Empty<McpTokenRecord>();
    }

    public async Task<bool> RevokeTokenAsync(Guid tokenId, CancellationToken ct = default)
    {
        if (_provider == "sqlite")
        {
            return await RevokeTokenSqliteAsync(tokenId, ct);
        }
        else if (_provider == "postgresql")
        {
            return await RevokeTokenPostgresAsync(tokenId, ct);
        }

        return false;
    }

    public async Task<McpTokenRecord?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHash = TokenHasher.Hash(token);

        if (_provider == "sqlite")
        {
            return await ValidateTokenSqliteAsync(tokenHash, ct);
        }
        else if (_provider == "postgresql")
        {
            return await ValidateTokenPostgresAsync(tokenHash, ct);
        }

        return null;
    }

    public async Task MapTokenAsync(Guid tokenId, Guid buildinKeyId, CancellationToken ct = default)
    {
        if (_provider == "sqlite")
        {
            await MapTokenSqliteAsync(tokenId, buildinKeyId, ct);
        }
        else if (_provider == "postgresql")
        {
            await MapTokenPostgresAsync(tokenId, buildinKeyId, ct);
        }
    }

    public async Task<BuildinKeyRecord> CreateBuildinKeyAsync(string name, string keyValue, CancellationToken ct = default)
    {
        var keyId = Guid.NewGuid();

        if (_provider == "sqlite")
        {
            await CreateBuildinKeySqliteAsync(keyId, name, keyValue, ct);
        }
        else if (_provider == "postgresql")
        {
            await CreateBuildinKeyPostgresAsync(keyId, name, keyValue, ct);
        }

        return new BuildinKeyRecord(keyId, name, keyValue, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<BuildinKeyRecord>> ListBuildinKeysAsync(CancellationToken ct = default)
    {
        if (_provider == "sqlite")
        {
            return await ListBuildinKeysSqliteAsync(ct);
        }
        else if (_provider == "postgresql")
        {
            return await ListBuildinKeysPostgresAsync(ct);
        }

        return Array.Empty<BuildinKeyRecord>();
    }

    private async Task CreateTokenSqliteAsync(Guid tokenId, string name, string tokenHash, CancellationToken ct)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO mcp_tokens (id, name, token_hash, buildin_key_id, created_at, revoked_at, metadata)
            VALUES (@id, @name, @tokenHash, @buildinKeyId, @createdAt, @revokedAt, @metadata)";

        command.Parameters.AddWithValue("@id", tokenId.ToString());
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@tokenHash", tokenHash);
        command.Parameters.AddWithValue("@buildinKeyId", DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", DateTimeOffset.UtcNow.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("@revokedAt", DBNull.Value);
        command.Parameters.AddWithValue("@metadata", "{}");

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task CreateTokenPostgresAsync(Guid tokenId, string name, string tokenHash, CancellationToken ct)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO mcp_tokens (id, name, token_hash, buildin_key_id, created_at, revoked_at, metadata)
            VALUES ($1, $2, $3, $4, $5, $6, $7)";

        command.Parameters.AddWithValue(tokenId);
        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue(tokenHash);
        command.Parameters.AddWithValue(DBNull.Value);
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue(DBNull.Value);
        command.Parameters.AddWithValue("{}");

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<IReadOnlyList<McpTokenRecord>> ListTokensSqliteAsync(CancellationToken ct)
    {
        var tokens = new List<McpTokenRecord>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, token_hash, buildin_key_id, created_at, revoked_at FROM mcp_tokens";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var buildinKeyId = reader.IsDBNull(3) ? (Guid?)null : Guid.Parse(reader.GetString(3));
            var revokedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture);

            tokens.Add(new McpTokenRecord(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                buildinKeyId,
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                revokedAt));
        }

        return tokens;
    }

    private async Task<IReadOnlyList<McpTokenRecord>> ListTokensPostgresAsync(CancellationToken ct)
    {
        var tokens = new List<McpTokenRecord>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, token_hash, buildin_key_id, created_at, revoked_at FROM mcp_tokens";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var buildinKeyId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
            var revokedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : reader.GetDateTime(5);

            tokens.Add(new McpTokenRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                buildinKeyId,
                reader.GetDateTime(4),
                revokedAt));
        }

        return tokens;
    }

    private async Task<bool> RevokeTokenSqliteAsync(Guid tokenId, CancellationToken ct)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE mcp_tokens SET revoked_at = @revokedAt WHERE id = @id AND revoked_at IS NULL";
        command.Parameters.AddWithValue("@revokedAt", DateTimeOffset.UtcNow.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("@id", tokenId.ToString());

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }

    private async Task<bool> RevokeTokenPostgresAsync(Guid tokenId, CancellationToken ct)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE mcp_tokens SET revoked_at = $1 WHERE id = $2 AND revoked_at IS NULL";
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue(tokenId);

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }

    private async Task<McpTokenRecord?> ValidateTokenSqliteAsync(string tokenHash, CancellationToken ct)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, token_hash, buildin_key_id, created_at, revoked_at
            FROM mcp_tokens
            WHERE token_hash = @tokenHash";
        command.Parameters.AddWithValue("@tokenHash", tokenHash);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var buildinKeyId = reader.IsDBNull(3) ? (Guid?)null : Guid.Parse(reader.GetString(3));
            var revokedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture);

            return new McpTokenRecord(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                buildinKeyId,
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                revokedAt);
        }

        return null;
    }

    private async Task<McpTokenRecord?> ValidateTokenPostgresAsync(string tokenHash, CancellationToken ct)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, token_hash, buildin_key_id, created_at, revoked_at
            FROM mcp_tokens
            WHERE token_hash = $1";
        command.Parameters.AddWithValue(tokenHash);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var buildinKeyId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
            var revokedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : reader.GetDateTime(5);

            return new McpTokenRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                buildinKeyId,
                reader.GetDateTime(4),
                revokedAt);
        }

        return null;
    }

    private async Task MapTokenSqliteAsync(Guid tokenId, Guid buildinKeyId, CancellationToken ct)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE mcp_tokens SET buildin_key_id = @buildinKeyId WHERE id = @id";
        command.Parameters.AddWithValue("@buildinKeyId", buildinKeyId.ToString());
        command.Parameters.AddWithValue("@id", tokenId.ToString());

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task MapTokenPostgresAsync(Guid tokenId, Guid buildinKeyId, CancellationToken ct)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE mcp_tokens SET buildin_key_id = $1 WHERE id = $2";
        command.Parameters.AddWithValue(buildinKeyId);
        command.Parameters.AddWithValue(tokenId);

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task CreateBuildinKeySqliteAsync(Guid keyId, string name, string keyValue, CancellationToken ct)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO buildin_keys (id, name, key_value, created_at) VALUES (@id, @name, @keyValue, @createdAt)";
        command.Parameters.AddWithValue("@id", keyId.ToString());
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@keyValue", keyValue);
        command.Parameters.AddWithValue("@createdAt", DateTimeOffset.UtcNow.UtcDateTime.ToString("o"));

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task CreateBuildinKeyPostgresAsync(Guid keyId, string name, string keyValue, CancellationToken ct)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO buildin_keys (id, name, key_value, created_at) VALUES ($1, $2, $3, $4)";
        command.Parameters.AddWithValue(keyId);
        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue(keyValue);
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<IReadOnlyList<BuildinKeyRecord>> ListBuildinKeysSqliteAsync(CancellationToken ct)
    {
        var keys = new List<BuildinKeyRecord>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, key_value, created_at FROM buildin_keys";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            keys.Add(new BuildinKeyRecord(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return keys;
    }

    private async Task<IReadOnlyList<BuildinKeyRecord>> ListBuildinKeysPostgresAsync(CancellationToken ct)
    {
        var keys = new List<BuildinKeyRecord>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, key_value, created_at FROM buildin_keys";

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            keys.Add(new BuildinKeyRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3)));
        }

        return keys;
    }

    private static string GenerateToken()
    {
        return $"mcp_{Guid.NewGuid():N}";
    }
}

public static class TokenHasher
{
    public static string Hash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string token, string storedHash)
    {
        var computedHash = Hash(token);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(storedHash));
    }
}