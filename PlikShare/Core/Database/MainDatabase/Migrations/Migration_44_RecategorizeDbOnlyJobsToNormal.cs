using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// The four delete jobs (workspace / folders / boxes / ephemeral-key cleanup) were moved off the
/// removed DbOnly queue lane onto the Normal lane. Re-stamp any rows still queued under the old
/// DbOnly category (0) to Normal (1) so the producer keeps selecting them after the DbOnly capacity
/// branch is gone — otherwise they would fall into the zero-capacity ELSE and never run.
/// </summary>
public class Migration_44_RecategorizeDbOnlyJobsToNormal : ISQLiteMigration
{
    public string Name => "recategorize_dbonly_jobs_to_normal";
    public DateOnly Date { get; } = new(2026, 6, 8);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection
            .NonQueryCmd(
                sql: """
                     UPDATE q_queue
                     SET q_job_category = 1
                     WHERE q_job_category = 0
                     """,
                transaction: transaction)
            .Execute();
    }
}
