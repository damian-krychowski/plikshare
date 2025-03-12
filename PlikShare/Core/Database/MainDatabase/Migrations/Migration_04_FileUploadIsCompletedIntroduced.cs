using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_04_FileUploadIsCompletedIntroduced: SQLiteMigrationBase
{
    public override string Name => "file_upload_is_completed_introduced";
    public override DateOnly Date { get; } = new(2024, 11, 23);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.04_file_upload_is_completed_introduced.sql"
    ];
}