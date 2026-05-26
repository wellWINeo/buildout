using LinqToDB.Mapping;

namespace Buildout.Mcp.Audit;

[Table("audit_entries")]
public class AuditEntryRecord
{
    [Column(Name = "id", IsPrimaryKey = true)]
    public string Id { get; set; } = string.Empty;

    [Column(Name = "tool_name", CanBeNull = false)]
    public string ToolName { get; set; } = string.Empty;

    [Column(Name = "session_id")]
    public string? SessionId { get; set; }

    [Column(Name = "timestamp", CanBeNull = false)]
    public string Timestamp { get; set; } = string.Empty;

    [Column(Name = "parameters", CanBeNull = false)]
    public string Parameters { get; set; } = "{}";

    [Column(Name = "outcome", CanBeNull = false)]
    public int Outcome { get; set; }

    [Column(Name = "duration_ms", CanBeNull = false)]
    public long DurationMs { get; set; }

    [Column(Name = "error_details")]
    public string? ErrorDetails { get; set; }
}