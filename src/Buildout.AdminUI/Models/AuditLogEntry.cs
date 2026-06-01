namespace Buildout.AdminUI.Models;

public sealed class AuditLogEntry
{
    public Guid Id { get; init; }
    public string Actor { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string? Details { get; init; }
}
