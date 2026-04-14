using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_22_WorkspaceEncryptionSaltIntroduced : SQLiteMigrationBase
{
    public override string Name => "workspace_encryption_salt_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 14);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.22_workspace_encryption_salt_introduced.sql"
    ];
}
