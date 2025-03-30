using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_17_WorkspaceMaxSizeColumnIntroduced : SQLiteMigrationBase
{
    public override string Name => "workspace_max_size_column_introduced";
    public override DateOnly Date { get; } = new(2025, 3, 24);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.17_workspace_max_size_column_introduced.sql"
    ];
}