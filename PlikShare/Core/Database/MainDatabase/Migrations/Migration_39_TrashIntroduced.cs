using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_39_TrashIntroduced : SQLiteMigrationBase
{
    public override string Name => "trash_introduced";
    public override DateOnly Date { get; } = new(2026, 5, 19);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.39_trash_introduced.sql"
    ];
}
