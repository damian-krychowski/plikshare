using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_52_AgentsIntroduced : SQLiteMigrationBase
{
    public override string Name => "agents_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 15);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.52_agents_introduced.sql"
    ];
}
