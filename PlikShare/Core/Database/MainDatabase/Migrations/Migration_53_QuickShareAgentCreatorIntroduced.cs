using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_53_QuickShareAgentCreatorIntroduced : SQLiteMigrationBase
{
    public override string Name => "quick_share_agent_creator_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 16);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.53_quick_share_agent_creator_introduced.sql"
    ];
}
