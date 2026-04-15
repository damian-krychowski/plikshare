using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_25_StorageEncryptionKeysIntroduced : SQLiteMigrationBase
{
    public override string Name => "storage_encryption_keys_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 14);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.25_storage_encryption_keys_introduced.sql"
    ];
}
