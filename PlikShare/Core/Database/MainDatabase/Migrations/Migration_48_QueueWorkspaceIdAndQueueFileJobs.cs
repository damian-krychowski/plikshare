using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_48_QueueWorkspaceIdAndQueueFileJobs : SQLiteMigrationBase
{
    public override string Name => "queue_workspace_id_and_queue_file_jobs";
    public override DateOnly Date { get; } = new(2026, 6, 10);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.48_queue_workspace_id_and_queue_file_jobs.sql"
    ];
}
