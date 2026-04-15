using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_26_WorkspaceEncryptionKeysIntroduced : SQLiteMigrationBase
{
    public override string Name => "workspace_encryption_keys_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 15);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.26_workspace_encryption_keys_introduced.sql"
    ];
}
