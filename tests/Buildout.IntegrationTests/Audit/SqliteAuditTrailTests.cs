using System.Globalization;
using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class SqliteAuditTrailTests
{
    [Fact]
    public async Task AdoNetAuditTrail_PersistsEntry_AndRetrieves()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.sqlite");

        try
        {
            var connectionString = $"Data Source={tempDb}";

            var serviceProvider = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(builder => builder
                    .AddSQLite()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(Buildout.Mcp.Audit.Migrations.Migration_001_CreateAuditEntries).Assembly)
                    .For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole())
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
            }

            var mockLogger = new MockLogger();
            var auditTrail = new AdoNetAuditTrail(connectionString, "sqlite", mockLogger);

            await auditTrail.RecordEntryAsync(new AuditEntry
            {
                ToolName = "test_tool",
                SessionId = "session-123",
                Parameters = "{}",
                Outcome = AuditOutcome.Success,
                Duration = TimeSpan.FromMilliseconds(100),
            });

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM audit_entries WHERE tool_name = 'test_tool'";
            var count = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);

            Assert.Equal(1, count);
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
    public async Task AdoNetAuditTrail_HandlesFailureWithErrorDetails()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.sqlite");

        try
        {
            var connectionString = $"Data Source={tempDb}";

            var serviceProvider = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(builder => builder
                    .AddSQLite()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(Buildout.Mcp.Audit.Migrations.Migration_001_CreateAuditEntries).Assembly)
                    .For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole())
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
            }

            var mockLogger = new MockLogger();
            var auditTrail = new AdoNetAuditTrail(connectionString, "sqlite", mockLogger);

            await auditTrail.RecordEntryAsync(new AuditEntry
            {
                ToolName = "test_tool",
                SessionId = "session-123",
                Parameters = "{}",
                Outcome = AuditOutcome.Failure,
                Duration = TimeSpan.FromMilliseconds(50),
                ErrorDetails = "Test error occurred",
            });

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT error_details FROM audit_entries WHERE tool_name = 'test_tool'";
            var errorDetails = command.ExecuteScalar()?.ToString();

            Assert.Equal("Test error occurred", errorDetails);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    private sealed class MockLogger : ILogger<AdoNetAuditTrail>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
