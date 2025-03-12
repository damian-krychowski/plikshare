using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_14_WorkspaceIdAddedToIntegrationsTable : SQLiteMigrationBase
{
    public override string Name => "workspace_id_added_to_integrations_table";
    public override DateOnly Date { get; } = new(2025, 2, 19);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.14_workspace_id_added_to_integrations_table.sql"
    ];
}