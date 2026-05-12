using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_37_WorkspaceAuditLogDisabledEventsIntroduced : SQLiteMigrationBase
{
    public override string Name => "workspace_audit_log_disabled_events_introduced";
    public override DateOnly Date { get; } = new(2026, 5, 12);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.37_workspace_audit_log_disabled_events_introduced.sql"
    ];
}
