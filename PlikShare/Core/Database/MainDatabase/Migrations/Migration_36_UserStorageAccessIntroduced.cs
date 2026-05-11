using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_36_UserStorageAccessIntroduced : SQLiteMigrationBase
{
    public override string Name => "user_storage_access_introduced";
    public override DateOnly Date { get; } = new(2026, 5, 11);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.36_user_storage_access_introduced.sql"
    ];
}
