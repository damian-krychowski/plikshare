using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_53_AgentEventSubscriptionsIntroduced : SQLiteMigrationBase
{
    public override string Name => "agent_event_subscriptions_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 30);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.53_agent_event_subscriptions_introduced.sql"
    ];
}
