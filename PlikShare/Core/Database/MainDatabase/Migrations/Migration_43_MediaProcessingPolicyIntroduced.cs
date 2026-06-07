using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_43_MediaProcessingPolicyIntroduced : SQLiteMigrationBase
{
    public override string Name => "media_processing_policy_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 6);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.43_media_processing_policy_introduced.sql"
    ];
}
