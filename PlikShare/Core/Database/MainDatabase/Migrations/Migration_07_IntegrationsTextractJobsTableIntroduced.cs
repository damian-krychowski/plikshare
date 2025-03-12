using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_07_IntegrationsTextractJobsTableIntroduced: SQLiteMigrationBase
{
    public override string Name => "integrations_textract_jobs_table_introduced";
    public override DateOnly Date { get; } = new(2025, 1, 7);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.07_integrations_textract_jobs_table_introduced.sql"
    ];
}