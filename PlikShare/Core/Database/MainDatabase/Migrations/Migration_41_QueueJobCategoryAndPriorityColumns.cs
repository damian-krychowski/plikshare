using Microsoft.Data.Sqlite;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Materializes each queue job's category and priority into <c>q_job_category</c> /
/// <c>q_job_priority</c> columns so the producer's job-selection query reads plain indexed integers
/// instead of calling per-row custom SQLite functions on every scan. Backfills every existing row
/// from <see cref="QueueJobInfoProvider"/> (the same source the runtime uses) and adds a partial
/// index over pending jobs matching the selection query's filter + ordering.
///
/// <para><c>qc_queue_completed</c> is intentionally untouched — it's an archive, never scanned for
/// job selection.</para>
/// </summary>
public class Migration_41_QueueJobCategoryAndPriorityColumns(
    QueueJobInfoProvider queueJobInfoProvider) : ISQLiteMigration
{
    public string Name => "queue_job_category_and_priority_columns";
    public DateOnly Date { get; } = new(2026, 5, 31);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Map job_type -> category/priority through the same provider the runtime uses. An unknown
        // type (e.g. a job enqueued by a since-removed executor) falls back to Normal so the upgrade
        // never fails mid-migration on a stale row.
        connection.CreateFunction("app_migrate_job_category", (string? jobType) =>
        {
            try { return (int)queueJobInfoProvider.GetJobCategory(jobType!); }
            catch { return (int)QueueJobCategory.Normal; }
        });

        connection.CreateFunction("app_migrate_job_priority", (string? jobType) =>
        {
            try { return queueJobInfoProvider.GetJobPriority(jobType!); }
            catch { return QueueJobPriority.Normal; }
        });

        connection
            .NonQueryCmd(
                sql: "ALTER TABLE q_queue ADD COLUMN q_job_category INTEGER",
                transaction: transaction)
            .Execute();

        connection
            .NonQueryCmd(
                sql: "ALTER TABLE q_queue ADD COLUMN q_job_priority INTEGER",
                transaction: transaction)
            .Execute();

        connection
            .NonQueryCmd(
                sql: """
                    UPDATE q_queue
                    SET
                        q_job_category = app_migrate_job_category(q_job_type),
                        q_job_priority = app_migrate_job_priority(q_job_type)
                    """,
                transaction: transaction)
            .Execute();

        // Partial index mirroring the producer's selection query: filter on pending status, then
        // rank/order by category + priority, narrowed by execute-after date.
        connection
            .NonQueryCmd(
                sql: """
                    CREATE INDEX IF NOT EXISTS index__q_queue__pending_selection
                    ON q_queue (q_job_category, q_job_priority, q_execute_after_date)
                    WHERE q_status = 'pending'
                    """,
                transaction: transaction)
            .Execute();
    }
}
