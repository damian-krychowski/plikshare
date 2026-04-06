using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.AuditLogDatabase.Migrations;

public class Migration_AuditLog_01_InitialDbSetup : SQLiteMigrationBase
{
    public override string Name => "initial_audit_log_db_setup";
    public override DateOnly Date { get; } = new(2026, 4, 4);
    public override PlikShareDbType Type { get; } = PlikShareAuditLogDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.AuditLogDatabase.Migrations.01_audit_log_table_introduced.sql"
    ];
}
