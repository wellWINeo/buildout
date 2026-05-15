using Buildout.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Buildout.UnitTests.Diagnostics;

public sealed class OperationRecorderTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void Start_LogsOperationStartedAtDebug()
    {
        using var recorder = OperationRecorder.Start(_logger, "test_op");

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("test_op", entry.Message);
        Assert.Contains("started", entry.Message);
    }

    [Fact]
    public void Succeed_LogsCompletedAtInfoWithDuration()
    {
        var recorder = OperationRecorder.Start(_logger, "page_read");
        recorder.Succeed();
        recorder.Dispose();

        Assert.Equal(2, _logger.Entries.Count);
        Assert.Equal(LogLevel.Debug, _logger.Entries[0].Level);

        var completed = _logger.Entries[1];
        Assert.Equal(LogLevel.Information, completed.Level);
        Assert.Contains("page_read", completed.Message);
        Assert.Contains("completed", completed.Message);
        Assert.True(completed.DurationMs > 0);
    }

    [Fact]
    public void Fail_LogsFailedAtErrorWithDurationAndErrorType()
    {
        var recorder = OperationRecorder.Start(_logger, "search");
        recorder.Fail("transport");
        recorder.Dispose();

        Assert.Equal(2, _logger.Entries.Count);

        var failed = _logger.Entries[1];
        Assert.Equal(LogLevel.Error, failed.Level);
        Assert.Contains("search", failed.Message);
        Assert.Contains("failed", failed.Message);
        Assert.Contains("transport", failed.Message);
        Assert.True(failed.DurationMs > 0);
    }

    [Fact]
    public void Dispose_WithoutComplete_CallsFailWithUnknown()
    {
        var recorder = OperationRecorder.Start(_logger, "page_create");
        recorder.Dispose();

        Assert.Equal(2, _logger.Entries.Count);

        var guardEntry = _logger.Entries[1];
        Assert.Equal(LogLevel.Error, guardEntry.Level);
        Assert.Contains("unknown", guardEntry.Message);
    }

    [Fact]
    public void Succeed_AfterDispose_DoesNotLogAgain()
    {
        var recorder = OperationRecorder.Start(_logger, "db_view");
        recorder.Dispose();

        Assert.Equal(2, _logger.Entries.Count);

        recorder.Succeed();

        Assert.Equal(2, _logger.Entries.Count);
    }

    [Fact]
    public void SetTag_AppearsInLogMessage()
    {
        var recorder = OperationRecorder.Start(_logger, "page_read");
        recorder.SetTag("page_id", "abc-123");
        recorder.Succeed();
        recorder.Dispose();

        Assert.Equal(2, _logger.Entries.Count);
        Assert.Contains("abc-123", _logger.Entries[1].Message);
    }

    [Fact]
    public void Succeed_RecordsPositiveDuration()
    {
        var recorder = OperationRecorder.Start(_logger, "search");
        Thread.Sleep(2);
        recorder.Succeed();
        recorder.Dispose();

        var completed = _logger.Entries[1];
        Assert.True(completed.DurationMs >= 0);
        Assert.True(completed.DurationMs < 10_000);
    }

    [Fact]
    public void Fail_WithStatusCode_IncludesStatusCodeInLog()
    {
        var recorder = OperationRecorder.Start(_logger, "api_call");
        recorder.Fail("api", 404);
        recorder.Dispose();

        var failed = _logger.Entries[1];
        Assert.Contains("404", failed.Message);
    }

    private sealed class TestLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            double? durationMs = null;

            if (state is IReadOnlyList<KeyValuePair<string, object>> kvps)
            {
                foreach (var kvp in kvps)
                {
                    if (kvp.Key == "DurationMs" && kvp.Value is double d)
                        durationMs = d;
                }
            }

            Entries.Add(new LogEntry(logLevel, message, durationMs));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    private sealed record LogEntry(LogLevel Level, string Message, double? DurationMs);
}
