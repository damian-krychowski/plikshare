using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_51_AuditLogMaxSizeGrandfatherExistingInstalls : SQLiteMigrationBase
{
    public override string Name => "audit_log_max_size_grandfather_existing_installs";
    public override DateOnly Date { get; } = new(2026, 6, 13);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.51_audit_log_max_size_grandfather_existing_installs.sql"
    ];
}
