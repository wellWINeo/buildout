using Buildout.AdminUI.Models;

namespace Buildout.AdminUI.Services;

public interface IAuditLogService
{
    IReadOnlyList<AuditLogEntry> GetAll();
}
