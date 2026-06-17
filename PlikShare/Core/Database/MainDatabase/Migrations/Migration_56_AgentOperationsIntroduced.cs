using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_56_AgentOperationsIntroduced : SQLiteMigrationBase
{
    public override string Name => "agent_operations_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 17);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.56_agent_operations_introduced.sql"
    ];
}
