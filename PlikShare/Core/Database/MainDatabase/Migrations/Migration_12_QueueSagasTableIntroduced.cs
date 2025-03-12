using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_12_QueueSagasTableIntroduced : SQLiteMigrationBase
{
    public override string Name => "queue_sagas_table_introduced";
    public override DateOnly Date { get; } = new(2025, 1, 26);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.12_queue_sagas_table_introduced.sql"
    ];
}