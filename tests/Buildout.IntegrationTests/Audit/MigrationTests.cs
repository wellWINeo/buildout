using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using System.Data.SQLite;
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
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                runner.MigrateUp();
            }

            using var connection = new SQLiteConnection($"Data Source={tempDb}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
            var tables = new List<string>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            Assert.Contains("audit_entries", tables);
            Assert.Contains("VersionInfo", tables);

            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "PRAGMA index_list(audit_entries);";
            var indexes = new List<string>();
            using (var indexReader = indexCommand.ExecuteReader())
            {
                while (indexReader.Read())
                {
                    indexes.Add(indexReader.GetString(0));
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
                try
                {
                    File.Delete(tempDb);
                }
                catch
                {
                }
            }
        }
    }
}