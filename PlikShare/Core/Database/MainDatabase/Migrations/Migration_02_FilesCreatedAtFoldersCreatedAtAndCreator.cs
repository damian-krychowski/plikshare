using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_02_FilesCreatedAtFoldersCreatedAtAndCreator: SQLiteMigrationBase
{
    public override string Name => "files_created_at_folders_created_at_and_creator";
    public override DateOnly Date { get; } = new(2024, 11, 10);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.02_files_created_at_folders_created_at_and_creator.sql"
    ];
}