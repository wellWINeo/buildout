using Buildout.Core.Audit;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Buildout.Mcp.Audit;

public class Linq2DbAuditTrail : IAuditTrail
{
    private readonly string _connectionString;
    private readonly string _provider;
    private readonly ILogger<Linq2DbAuditTrail> _logger;

    public Linq2DbAuditTrail(string connectionString, string provider, ILogger<Linq2DbAuditTrail> logger)
    {
        _connectionString = connectionString;
        _provider = provider;
        _logger = logger;
    }

    public Task RecordEntryAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (_provider == "sqlite")
                {
                    using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken);

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO audit_entries (id, tool_name, session_id, timestamp, parameters, outcome, duration_ms, error_details)
                        VALUES (@id, @toolName, @sessionId, @timestamp, @parameters, @outcome, @durationMs, @errorDetails)";

                    command.Parameters.AddWithValue("@id", entry.Id.ToString());
                    command.Parameters.AddWithValue("@toolName", entry.ToolName);
                    command.Parameters.AddWithValue("@sessionId", entry.SessionId ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@timestamp", entry.Timestamp.UtcDateTime.ToString("o"));
                    command.Parameters.AddWithValue("@parameters", entry.Parameters);
                    command.Parameters.AddWithValue("@outcome", (int)entry.Outcome);
                    command.Parameters.AddWithValue("@durationMs", entry.Duration.TotalMilliseconds);
                    command.Parameters.AddWithValue("@errorDetails", entry.ErrorDetails ?? (object)DBNull.Value);

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                else if (_provider == "postgresql")
                {
                    using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken);

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO audit_entries (id, tool_name, session_id, timestamp, parameters, outcome, duration_ms, error_details)
                        VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";

                    command.Parameters.AddWithValue(entry.Id.ToString());
                    command.Parameters.AddWithValue(entry.ToolName);
                    command.Parameters.AddWithValue(entry.SessionId ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(entry.Timestamp.UtcDateTime.ToString("o"));
                    command.Parameters.AddWithValue(entry.Parameters);
                    command.Parameters.AddWithValue((int)entry.Outcome);
                    command.Parameters.AddWithValue(entry.Duration.TotalMilliseconds);
                    command.Parameters.AddWithValue(entry.ErrorDetails ?? (object)DBNull.Value);

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit entry for tool {ToolName}", entry.ToolName);
            }
        }, cancellationToken);
    }
}