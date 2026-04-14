using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_23_FilesEncryptionChainSaltsIntroduced : SQLiteMigrationBase
{
    public override string Name => "files_encryption_chain_salts_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 14);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.23_files_encryption_chain_salts_introduced.sql"
    ];
}
