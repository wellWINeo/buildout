using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class MigrationTests
{
    [Fact]
    public void Migration_001_CreatesCorrectSchemaInSQLite()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"migration_test_{Guid.NewGuid():N}.sqlite");

        try
        {
            var serviceProvider = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(builder => builder
                    .AddSQLite()
                    .WithGlobalConnectionString($"Data Source={tempDb}")
                    .ScanIn(typeof(Buildout.Mcp.Audit.Migrations.Migration_001_CreateAuditEntries).Assembly)
                    .For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole())
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
            }

            using var connection = new SqliteConnection($"Data Source={tempDb}");
            connection.Open();

            using var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
            var tables = new List<string>();
            using (var reader = tableCmd.ExecuteReader())
            {
                while (reader.Read()) tables.Add(reader.GetString(0));
            }

            Assert.Contains("audit_entries", tables);
            Assert.Contains("VersionInfo", tables);

            using var indexCmd = connection.CreateCommand();
            indexCmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='audit_entries' ORDER BY name;";
            var indexes = new List<string>();
            using (var reader = indexCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    if (!string.IsNullOrEmpty(name) && !name.StartsWith("sqlite_", StringComparison.Ordinal))
                        indexes.Add(name);
                }
            }

            Assert.Contains("idx_audit_entries_timestamp", indexes);
            Assert.Contains("idx_audit_entries_tool_name", indexes);
            Assert.Contains("idx_audit_entries_session_id", indexes);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task Migration_001_CreatesCorrectSchemaInPostgreSQL()
    {
        // Requires Docker (Testcontainers). Skipped gracefully when unavailable.
        AuditTestFixture fixture = new();
        await fixture.InitializeAsync();

        if (fixture.PostgresConnectionString is null)
        {
            await fixture.DisposeAsync();
            return;
        }

        try
        {
            var connectionString = fixture.PostgresConnectionString;

            var serviceProvider = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(builder => builder
                    .AddPostgres()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(Buildout.Mcp.Audit.Migrations.Migration_001_CreateAuditEntries).Assembly)
                    .For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole())
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Verify table exists
            await using var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'audit_entries'";
            var tableCount = Convert.ToInt64(await tableCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(1L, tableCount);

            // Verify timestamp column is TIMESTAMPTZ
            await using var typeCmd = connection.CreateCommand();
            typeCmd.CommandText =
                "SELECT data_type FROM information_schema.columns " +
                "WHERE table_name = 'audit_entries' AND column_name = 'timestamp'";
            var dataType = (await typeCmd.ExecuteScalarAsync())?.ToString();
            Assert.Equal("timestamp with time zone", dataType);

            // Verify indexes exist
            await using var indexCmd = connection.CreateCommand();
            indexCmd.CommandText =
                "SELECT indexname FROM pg_indexes WHERE tablename = 'audit_entries' ORDER BY indexname";
            var indexes = new List<string>();
            await using (var reader = await indexCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) indexes.Add(reader.GetString(0));
            }

            Assert.Contains("idx_audit_entries_timestamp", indexes);
            Assert.Contains("idx_audit_entries_tool_name", indexes);
            Assert.Contains("idx_audit_entries_session_id", indexes);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
