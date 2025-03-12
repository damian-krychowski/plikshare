using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_09_FilesParentFileIdColumnIntroduced : SQLiteMigrationBase
{
    public override string Name => "files_parent_file_id_column_introduced";
    public override DateOnly Date { get; } = new(2025, 1, 19);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.09_files_parent_file_id_column_introduced.sql"
    ];
}