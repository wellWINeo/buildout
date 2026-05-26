namespace Buildout.Core.Audit;

public interface IAuditTrail
{
    Task RecordEntryAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}