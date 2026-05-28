using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_40_QueueResultAndThumbnailIndexesIntroduced : SQLiteMigrationBase
{
    public override string Name => "queue_result_and_thumbnail_indexes_introduced";
    public override DateOnly Date { get; } = new(2026, 5, 28);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.40_queue_result_and_thumbnail_indexes_introduced.sql"
    ];
}
