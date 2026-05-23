using Microsoft.Extensions.Logging;

namespace Buildout.Core.Buildin;

internal static partial class BotBuildinClientLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "API call {Method} completed with {Outcome}")]
    internal static partial void ApiCallCompleted(ILogger<BotBuildinClient> logger, string method, string outcome);

    [LoggerMessage(Level = LogLevel.Error, Message = "API call {Method} failed with {Outcome} error_type {ErrorType}")]
    internal static partial void ApiCallFailed(ILogger<BotBuildinClient> logger, string method, string outcome, string errorType);
}
