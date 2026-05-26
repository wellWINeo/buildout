using Buildout.Core.Audit;

namespace Buildout.Mcp.Audit;

public class NullAuditTrail : IAuditTrail
{
    public Task RecordEntryAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}