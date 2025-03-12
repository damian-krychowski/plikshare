using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_03_StorageEncryptionIntoruced: SQLiteMigrationBase
{
    public override string Name => "storage_encryption_introduced";
    public override DateOnly Date { get; } = new(2024, 11, 20);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.03_storage_encryption_introduced.sql"
    ];
}