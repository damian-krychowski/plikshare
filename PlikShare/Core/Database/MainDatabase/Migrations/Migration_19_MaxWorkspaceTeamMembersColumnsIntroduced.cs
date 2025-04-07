using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_19_MaxWorkspaceTeamMembersColumnsIntroduced : SQLiteMigrationBase
{
    public override string Name => "max_workspace_team_members_columns_introduced";
    public override DateOnly Date { get; } = new(2025, 3, 24);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.19_max_workspace_team_members_columns_introduced.sql"
    ];
}