using Buildout.Core.Audit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Buildout.Mcp.Audit;

public sealed class AdoNetAuditTrail : IAuditTrail
{
    private static readonly Action<ILogger, string, Exception?> s_failedToWrite =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, "AuditWriteFailed"),
            "Failed to write audit entry for tool {ToolName}");

    private readonly string _connectionString;
    private readonly string _provider;
    private readonly ILogger<AdoNetAuditTrail> _logger;

    public AdoNetAuditTrail(string connectionString, string provider, ILogger<AdoNetAuditTrail> logger)
    {
        _connectionString = connectionString;
        _provider = provider;
        _logger = logger;
    }

    public async Task RecordEntryAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_provider == "sqlite")
            {
                await WriteSqliteAsync(entry, cancellationToken);
            }
            else if (_provider == "postgresql")
            {
                await WritePostgresAsync(entry, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            s_failedToWrite(_logger, entry.ToolName, ex);
        }
    }

    private async Task WriteSqliteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO audit_entries (id, tool_name, session_id, timestamp, parameters, outcome, duration_ms, error_details) " +
            "VALUES (@id, @toolName, @sessionId, @timestamp, @parameters, @outcome, @durationMs, @errorDetails)";

        command.Parameters.AddWithValue("@id", entry.Id.ToString());
        command.Parameters.AddWithValue("@toolName", entry.ToolName);
        command.Parameters.AddWithValue("@sessionId", entry.SessionId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@timestamp", entry.Timestamp.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue("@parameters", entry.Parameters);
        command.Parameters.AddWithValue("@outcome", (int)entry.Outcome);
        command.Parameters.AddWithValue("@durationMs", (long)entry.Duration.TotalMilliseconds);
        command.Parameters.AddWithValue("@errorDetails", entry.ErrorDetails ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task WritePostgresAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO audit_entries (id, tool_name, session_id, timestamp, parameters, outcome, duration_ms, error_details) " +
            "VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";

        command.Parameters.AddWithValue(entry.Id.ToString());
        command.Parameters.AddWithValue(entry.ToolName);
        command.Parameters.AddWithValue(entry.SessionId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(entry.Timestamp.UtcDateTime.ToString("o"));
        command.Parameters.AddWithValue(entry.Parameters);
        command.Parameters.AddWithValue((int)entry.Outcome);
        command.Parameters.AddWithValue((long)entry.Duration.TotalMilliseconds);
        command.Parameters.AddWithValue(entry.ErrorDetails ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
