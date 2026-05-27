using Buildout.Mcp.Audit.Migrations;
using FluentMigrator;

namespace Buildout.Mcp.Auth.Migrations;

[Migration(2)]
public class Migration_002_CreateAuthTables : Migration
{
    public override void Up()
    {
        IfDatabase("Postgres").Execute.Sql(@"
            CREATE TYPE buildout_auth_mode AS ENUM ('none', 'passthrough', 'proxy', 'mapped');
        ");

        Execute.Sql(@"
            CREATE TABLE IF NOT EXISTS buildin_keys (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                key_value TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_buildin_keys_name ON buildin_keys (name);
        ");

        Execute.Sql(@"
            CREATE TABLE IF NOT EXISTS mcp_tokens (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                token_hash TEXT NOT NULL,
                buildin_key_id TEXT,
                created_at TEXT NOT NULL,
                revoked_at TEXT,
                metadata TEXT DEFAULT '{}',
                FOREIGN KEY (buildin_key_id) REFERENCES buildin_keys(id)
            );

            CREATE INDEX IF NOT EXISTS idx_mcp_tokens_token_hash ON mcp_tokens (token_hash);
            CREATE INDEX IF NOT EXISTS idx_mcp_tokens_buildin_key_id ON mcp_tokens (buildin_key_id);
        ");

        IfDatabase("Postgres").Execute.Sql(@"
            ALTER TABLE audit_entries ADD COLUMN IF NOT EXISTS auth_identity TEXT DEFAULT NULL;
        ");
        IfDatabase("SQLite").Execute.Sql(@"
            ALTER TABLE audit_entries ADD COLUMN auth_identity TEXT DEFAULT NULL;
        ");
    }

    public override void Down()
    {
        IfDatabase("Postgres").Execute.Sql(@"
            ALTER TABLE audit_entries DROP COLUMN IF EXISTS auth_identity;
        ");
        IfDatabase("SQLite").Execute.Sql(@"
            ALTER TABLE audit_entries DROP COLUMN auth_identity;
        ");

        Execute.Sql("DROP INDEX IF EXISTS idx_mcp_tokens_buildin_key_id;");
        Execute.Sql("DROP INDEX IF EXISTS idx_mcp_tokens_token_hash;");
        Execute.Sql("DROP TABLE IF EXISTS mcp_tokens;");

        Execute.Sql("DROP INDEX IF EXISTS idx_buildin_keys_name;");
        Execute.Sql("DROP TABLE IF EXISTS buildin_keys;");

        IfDatabase("Postgres").Execute.Sql(@"
            DROP TYPE IF EXISTS buildout_auth_mode;
        ");
    }
}