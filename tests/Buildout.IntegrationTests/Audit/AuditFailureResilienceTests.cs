using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class AuditFailureResilienceTests
{
    [Fact]
    public async Task RecordEntryAsync_WithInvalidConnectionString_LogsErrorAndDoesNotThrow()
    {
        var mockLogger = new MockLogger();
        var auditTrail = new AdoNetAuditTrail("invalid_connection_string", "sqlite", mockLogger);

        var entry = new AuditEntry
        {
            ToolName = "test_tool",
            SessionId = "session-123",
            Parameters = "{}",
            Outcome = AuditOutcome.Success,
            Duration = TimeSpan.FromMilliseconds(100),
        };

        await auditTrail.RecordEntryAsync(entry);

        Assert.True(mockLogger.ErrorLogged);
    }

    private sealed class MockLogger : ILogger<AdoNetAuditTrail>
    {
        public bool ErrorLogged { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception != null)
            {
                ErrorLogged = true;
            }
        }
    }
}
