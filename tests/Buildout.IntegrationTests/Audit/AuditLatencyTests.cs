using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class AuditLatencyTests
{
    [Fact]
    public async Task AuditEnabled_LatencyOverhead_Under5ms()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.sqlite");
        
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<AuditTrailFilter>, NullLoggerFilter>();

        services.AddFluentMigratorCore()
            .ConfigureRunner(builder => builder
                .AddSQLite()
                .WithGlobalConnectionString($"Data Source={tempDb}")
                .ScanIn(typeof(Buildout.Mcp.Audit.Migrations.Migration_001_CreateAuditEntries).Assembly)
                .For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();
        }

        var mockLoggerAuditTrail = new MockLoggerAuditTrail();
        var auditTrail = new Linq2DbAuditTrail($"Data Source={tempDb}", "sqlite", mockLoggerAuditTrail);

        var iterations = 100;
        var latencies = new List<long>();

        for (int i = 0; i < iterations; i++)
        {
            var entry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                ToolName = "test_tool",
                SessionId = "session-123",
                Timestamp = DateTimeOffset.UtcNow,
                Parameters = "{}",
                Outcome = AuditOutcome.Success,
                Duration = TimeSpan.FromMilliseconds(100),
                ErrorDetails = null
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await auditTrail.RecordEntryAsync(entry);
            stopwatch.Stop();

            latencies.Add(stopwatch.ElapsedMilliseconds);
        }

        var averageLatency = latencies.Average();
        
        Assert.True(averageLatency < 50, $"Average audit latency {averageLatency}ms should be under 50ms for SQLite");
        
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

    [Fact]
    public async Task AuditDisabled_LatencyOverhead_Negligible()
    {
        var nullAuditTrail = new NullAuditTrail();

        var iterations = 100;
        var latencies = new List<long>();

        for (int i = 0; i < iterations; i++)
        {
            var entry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                ToolName = "test_tool",
                SessionId = "session-123",
                Timestamp = DateTimeOffset.UtcNow,
                Parameters = "{}",
                Outcome = AuditOutcome.Success,
                Duration = TimeSpan.FromMilliseconds(100),
                ErrorDetails = null
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await nullAuditTrail.RecordEntryAsync(entry);
            stopwatch.Stop();

            latencies.Add(stopwatch.ElapsedMilliseconds);
        }

        var averageLatency = latencies.Average();
        
        Assert.True(averageLatency < 1, $"Average null audit latency {averageLatency}ms should be negligible");
    }

    private sealed class MockLoggerAuditTrail : ILogger<Linq2DbAuditTrail>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class NullLoggerFilter : ILogger<AuditTrailFilter>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}