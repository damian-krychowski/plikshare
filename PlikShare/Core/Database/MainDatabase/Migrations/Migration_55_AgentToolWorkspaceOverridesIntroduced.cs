using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_55_AgentToolWorkspaceOverridesIntroduced : SQLiteMigrationBase
{
    public override string Name => "agent_tool_workspace_overrides_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 17);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.55_agent_tool_workspace_overrides_introduced.sql"
    ];
}
