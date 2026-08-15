using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.EventsDatabase.Migrations;

public class Migration_Events_01_AgentEventsInboxIntroduced : SQLiteMigrationBase
{
    public override string Name => "agent_events_inbox_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 30);
    public override PlikShareDbType Type { get; } = PlikShareEventsDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.EventsDatabase.Migrations.01_agent_events_inbox_introduced.sql"
    ];
}
