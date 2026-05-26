using FluentMigrator;

namespace Buildout.Mcp.Audit.Migrations;

[Migration(1)]
public class Migration_001_CreateAuditEntries : Migration
{
    public override void Up()
    {
        Create.Table("audit_entries")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("tool_name").AsString(255).NotNullable()
            .WithColumn("session_id").AsString(255).Nullable()
            .WithColumn("timestamp").AsString(50).NotNullable()
            .WithColumn("parameters").AsString(int.MaxValue).NotNullable().WithDefaultValue("{}")
            .WithColumn("outcome").AsInt32().NotNullable()
            .WithColumn("duration_ms").AsInt64().NotNullable()
            .WithColumn("error_details").AsString(int.MaxValue).Nullable();

        Create.Index("idx_audit_entries_timestamp").OnTable("audit_entries").OnColumn("timestamp");
        Create.Index("idx_audit_entries_tool_name").OnTable("audit_entries").OnColumn("tool_name");
        Create.Index("idx_audit_entries_session_id").OnTable("audit_entries").OnColumn("session_id");
    }

    public override void Down()
    {
        Delete.Table("audit_entries");
    }
}