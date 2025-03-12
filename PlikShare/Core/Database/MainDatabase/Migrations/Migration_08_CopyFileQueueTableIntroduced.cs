using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_08_CopyFileQueueTableIntroduced: SQLiteMigrationBase
{
    public override string Name => "copy_file_queue_table_introduced";
    public override DateOnly Date { get; } = new(2025, 1, 8);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.08_copy_file_queue_table_introduced.sql"
    ];
}