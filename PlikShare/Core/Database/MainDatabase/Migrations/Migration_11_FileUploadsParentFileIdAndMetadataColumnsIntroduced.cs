using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_11_FileUploadsParentFileIdAndMetadataColumnsIntroduced : SQLiteMigrationBase
{
    public override string Name => "file_uploads_parent_file_id_and_metadata_columns_introduced";
    public override DateOnly Date { get; } = new(2025, 1, 22);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.11_file_uploads_parent_file_id_and_metadata_columns_introduced.sql"
    ];
}