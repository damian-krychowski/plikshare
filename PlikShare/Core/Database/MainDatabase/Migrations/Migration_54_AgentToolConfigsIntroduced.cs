using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_54_AgentToolConfigsIntroduced : SQLiteMigrationBase
{
    public override string Name => "agent_tool_configs_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 17);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.54_agent_tool_configs_introduced.sql"
    ];
}
