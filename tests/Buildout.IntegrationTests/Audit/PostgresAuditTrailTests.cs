using System.Globalization;
using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

[Collection("PostgresAudit")]
public class PostgresAuditTrailTests : IClassFixture<AuditTestFixture>
{
    private readonly AuditTestFixture _fixture;

    public PostgresAuditTrailTests(AuditTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdoNetAuditTrail_PostgreSQL_PersistsEntry_AndRetrieves()
    {
        if (_fixture.PostgresConnectionString is null)
        {
            return; // Docker not available; skip.
        }

        var connectionString = _fixture.PostgresConnectionString;

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

        var mockLogger = new MockLogger();
        var auditTrail = new AdoNetAuditTrail(connectionString, "postgresql", mockLogger);

        var entryId = Guid.NewGuid();
        await auditTrail.RecordEntryAsync(new AuditEntry
        {
            Id = entryId,
            ToolName = "pg_test_tool",
            SessionId = "pg-session-456",
            Parameters = "{\"key\":\"value\"}",
            Outcome = AuditOutcome.Success,
            Duration = TimeSpan.FromMilliseconds(42),
        });

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit_entries WHERE tool_name = 'pg_test_tool'";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task AdoNetAuditTrail_PostgreSQL_HandlesFailureWithErrorDetails()
    {
        if (_fixture.PostgresConnectionString is null)
        {
            return; // Docker not available; skip.
        }

        var connectionString = _fixture.PostgresConnectionString;

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

        var mockLogger = new MockLogger();
        var auditTrail = new AdoNetAuditTrail(connectionString, "postgresql", mockLogger);

        await auditTrail.RecordEntryAsync(new AuditEntry
        {
            ToolName = "pg_failing_tool",
            Parameters = "{}",
            Outcome = AuditOutcome.Failure,
            Duration = TimeSpan.FromMilliseconds(5),
            ErrorDetails = "Postgres test error",
        });

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT error_details FROM audit_entries WHERE tool_name = 'pg_failing_tool'";
        var errorDetails = (await command.ExecuteScalarAsync())?.ToString();

        Assert.Equal("Postgres test error", errorDetails);
    }

    private sealed class MockLogger : ILogger<AdoNetAuditTrail>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
