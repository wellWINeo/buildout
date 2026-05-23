using Microsoft.Extensions.Logging;

namespace Buildout.Core.Search.Internal;

internal static partial class AncestorScopeFilterLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Cycle detected in parent chain at {AncestorId}")]
    internal static partial void CycleDetected(ILogger logger, string ancestorId);
}
