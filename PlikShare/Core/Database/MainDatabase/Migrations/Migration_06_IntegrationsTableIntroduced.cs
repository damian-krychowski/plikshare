using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_06_IntegrationsTableIntroduced: SQLiteMigrationBase
{
    public override string Name => "integrations_table_introduced";
    public override DateOnly Date { get; } = new(2024, 12, 31);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.06_integrations_table_introduced.sql"
    ];
}