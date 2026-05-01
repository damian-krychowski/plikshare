using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_30_QSoftRetriesLeftIntroduced : SQLiteMigrationBase
{
    public override string Name => "q_soft_retries_left_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 30);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.30_q_soft_retries_left_introduced.sql"
    ];
}
