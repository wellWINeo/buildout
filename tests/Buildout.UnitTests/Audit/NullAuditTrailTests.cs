using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using Xunit;

namespace Buildout.UnitTests.Audit;

public class NullAuditTrailTests
{
    [Fact]
    public async Task RecordEntryAsync_CompletesSynchronously_WithNoSideEffects()
    {
        var nullAuditTrail = new NullAuditTrail();
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

        Assert.True(stopwatch.ElapsedMilliseconds < 10, "NullAuditTrail should complete synchronously");
    }

    [Fact]
    public async Task RecordEntryAsync_DoesNotThrow_WithNullSessionId()
    {
        var nullAuditTrail = new NullAuditTrail();
        var entry = new AuditEntry
        {
            Id = Guid.NewGuid(),
            ToolName = "test_tool",
            SessionId = null,
            Timestamp = DateTimeOffset.UtcNow,
            Parameters = "{}",
            Outcome = AuditOutcome.Success,
            Duration = TimeSpan.FromMilliseconds(100),
            ErrorDetails = null
        };

        await nullAuditTrail.RecordEntryAsync(entry);
    }

    [Fact]
    public async Task RecordEntryAsync_DoesNotThrow_WithErrorDetails()
    {
        var nullAuditTrail = new NullAuditTrail();
        var entry = new AuditEntry
        {
            Id = Guid.NewGuid(),
            ToolName = "test_tool",
            SessionId = "session-123",
            Timestamp = DateTimeOffset.UtcNow,
            Parameters = "{}",
            Outcome = AuditOutcome.Failure,
            Duration = TimeSpan.FromMilliseconds(50),
            ErrorDetails = "Test error"
        };

        await nullAuditTrail.RecordEntryAsync(entry);
    }
}