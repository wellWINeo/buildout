using Microsoft.Extensions.Logging;

namespace Buildout.Core.Diagnostics;

internal static partial class OperationRecorderLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Operation {Operation} started")]
    internal static partial void OperationStarted(ILogger logger, string operation);

    [LoggerMessage(Level = LogLevel.Information, Message = "Operation {Operation} completed in {DurationMs:F2}ms{Tags}")]
    internal static partial void OperationCompleted(ILogger logger, string operation, double durationMs, string tags);

    [LoggerMessage(Level = LogLevel.Error, Message = "Operation {Operation} failed with error_type {ErrorType} status_code {StatusCode} in {DurationMs:F2}ms{Tags}")]
    internal static partial void OperationFailed(ILogger logger, string operation, string errorType, int? statusCode, double durationMs, string tags);
}
