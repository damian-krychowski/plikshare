using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_29_FileArtifactContentHashIntroduced : SQLiteMigrationBase
{
    public override string Name => "file_artifact_content_hash_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 25);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.29_file_artifact_content_hash_introduced.sql"
    ];
}
