using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.AuditLogDatabase.Migrations;

public class Migration_AuditLog_02_BoxColumnsAdded : SQLiteMigrationBase
{
    public override string Name => "audit_log_box_columns_added";
    public override DateOnly Date { get; } = new(2026, 4, 9);
    public override PlikShareDbType Type { get; } = PlikShareAuditLogDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.AuditLogDatabase.Migrations.02_box_columns_added.sql"
    ];
}
