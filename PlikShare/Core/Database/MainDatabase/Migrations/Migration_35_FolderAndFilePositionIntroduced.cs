using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_35_FolderAndFilePositionIntroduced : SQLiteMigrationBase
{
    public override string Name => "folder_and_file_position_introduced";
    public override DateOnly Date { get; } = new(2026, 5, 7);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.35_folder_and_file_position_introduced.sql"
    ];
}
