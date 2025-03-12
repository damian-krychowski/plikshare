using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_05_FileArtifactsIntroduced: SQLiteMigrationBase
{
    public override string Name => "file_artifacts_table_introduced";
    public override DateOnly Date { get; } = new(2024, 12, 1);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.05_file_artifacts_table_introduced.sql"
    ];
}