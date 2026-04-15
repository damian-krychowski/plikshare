using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_24_UserEncryptionKeypairIntroduced : SQLiteMigrationBase
{
    public override string Name => "user_encryption_keypair_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 14);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.24_user_encryption_keypair_introduced.sql"
    ];
}
