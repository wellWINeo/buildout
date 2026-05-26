using System.Globalization;
using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class SqliteAuditTrailTests
{
    [Fact]
    public async Task Linq2DbAuditTrail_PersistsEntry_AndRetrieves()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.sqlite");

        try
        {
            var connectionString = $"Data Source={tempDb}";
            
            var services = new ServiceCollection();
            services.AddFluentMigratorCore()
                .ConfigureRunner(builder => builder
                    .AddSQLite()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(Buildout.Mcp.Audit.Migrations.Migration_001_CreateAuditEntries).Assembly)
                    .For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole());

            var serviceProvider = services.BuildServiceProvider();
            var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();

            var mockLogger = new MockLogger();
            var auditTrail = new Linq2DbAuditTrail(connectionString, "sqlite", mockLogger);

            await auditTrail.RecordEntryAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                ToolName = "test_tool",
                SessionId = "session-123",
                Timestamp = DateTimeOffset.UtcNow,
                Parameters = "{}",
                Outcome = AuditOutcome.Success,
                Duration = TimeSpan.FromMilliseconds(100),
                ErrorDetails = null
            });

            await Task.Delay(100);

            var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
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

    [Fact]
    public async Task Linq2DbAuditTrail_HandlesFailureWithErrorDetails()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.sqlite");

        try
        {
            var connectionString = $"Data Source={tempDb}";
            
            var services = new ServiceCollection();
            services.AddFluentMigratorCore()
                .ConfigureRunner(builder => builder
                    .AddSQLite()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(Buildout.Mcp.Audit.Migrations.Migration_001_CreateAuditEntries).Assembly)
                    .For.Migrations())
                .AddLogging(lb => lb.AddFluentMigratorConsole());

            var serviceProvider = services.BuildServiceProvider();
            var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();

            var mockLogger = new MockLogger();
            var auditTrail = new Linq2DbAuditTrail(connectionString, "sqlite", mockLogger);

            await auditTrail.RecordEntryAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                ToolName = "test_tool",
                SessionId = "session-123",
                Timestamp = DateTimeOffset.UtcNow,
                Parameters = "{}",
                Outcome = AuditOutcome.Failure,
                Duration = TimeSpan.FromMilliseconds(50),
                ErrorDetails = "Test error occurred"
            });

            await Task.Delay(100);

            var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
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

    private sealed class MockLogger : ILogger<Linq2DbAuditTrail>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}