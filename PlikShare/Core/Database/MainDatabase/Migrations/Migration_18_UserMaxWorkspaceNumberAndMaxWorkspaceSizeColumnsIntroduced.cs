using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_18_UserMaxWorkspaceNumberAndMaxWorkspaceSizeColumnsIntroduced : SQLiteMigrationBase
{
    public override string Name => "user_max_workspace_number_and_max_workspace_size_columns_introduced";
    public override DateOnly Date { get; } = new(2025, 3, 24);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.18_user_max_workspace_number_and_max_workspace_size_columns_introduced.sql"
    ];
}