using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Materializes each batched job's item count into <c>q_batch_items_count</c> /
/// <c>qc_batch_items_count</c> so batch progress is summed from a plain column instead of parsing the
/// opaque (and ephemeral-redacted on completion) job definition JSON. NULL for non-batch jobs.
/// </summary>
public class Migration_45_QueueBatchItemsCountColumns : ISQLiteMigration
{
    public string Name => "queue_batch_items_count_columns";
    public DateOnly Date { get; } = new(2026, 6, 9);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection
            .NonQueryCmd(
                sql: "ALTER TABLE q_queue ADD COLUMN q_batch_items_count INTEGER",
                transaction: transaction)
            .Execute();

        connection
            .NonQueryCmd(
                sql: "ALTER TABLE qc_queue_completed ADD COLUMN qc_batch_items_count INTEGER",
                transaction: transaction)
            .Execute();
    }
}
