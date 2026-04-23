using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_28_EphemeralWorkspaceEncryptionKeysIntroduced : SQLiteMigrationBase
{
    public override string Name => "ephemeral_workspace_encryption_keys_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 22);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.28_ephemeral_workspace_encryption_keys_introduced.sql"
    ];
}
