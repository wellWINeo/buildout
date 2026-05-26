using LinqToDB;
using LinqToDB.Data;

namespace Buildout.Mcp.Audit;

public class AuditDataConnection : DataConnection
{
    public AuditDataConnection(DataOptions<AuditDataConnection> options)
        : base(options.Options)
    {
    }

    public ITable<AuditEntryRecord> AuditEntries => this.GetTable<AuditEntryRecord>();
}