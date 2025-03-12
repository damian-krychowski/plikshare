using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_10_FilesMetadataColumnIntroduced : SQLiteMigrationBase
{
    public override string Name => "files_metadata_column_introduced";
    public override DateOnly Date { get; } = new(2025, 1, 21);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.10_files_metadata_column_introduced.sql"
    ];
}