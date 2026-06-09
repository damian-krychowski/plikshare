using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Queue;

public class BatchProgressQuery(PlikShareDb plikShareDb)
{
    public sealed record Counts(int Completed, int Outstanding, int Failed)
    {
        public int Total => Completed + Outstanding;
        public int Pending => Outstanding - Failed;
    }

    public Counts GetCounts(
        Guid batchId)
    {
        using var connection = plikShareDb.OpenConnection();

        return GetCounts(
            connection,
            batchId);
    }

    public static Counts GetCounts(
        SqliteConnection connection,
        Guid batchId)
    {
        var result = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        (
                            SELECT COALESCE(SUM(qc_batch_items_count), 0)
                            FROM qc_queue_completed
                            WHERE qc_batch_id = $batchId
                        ),
                        (
                            SELECT COALESCE(SUM(q_batch_items_count), 0)
                            FROM q_queue
                            WHERE q_batch_id = $batchId
                        ),
                        (
                            SELECT COALESCE(SUM(q_batch_items_count), 0)
                            FROM q_queue
                            WHERE q_batch_id = $batchId
                                AND q_status = $failedStatus
                        )
                    """,
                readRowFunc: reader => new Counts(
                    Completed: reader.GetInt32(0),
                    Outstanding: reader.GetInt32(1),
                    Failed: reader.GetInt32(2)))
            .WithParameter("$batchId", batchId)
            .WithParameter("$failedStatus", QueueStatus.Failed)
            .Execute();

        return result.Value;
    }
}
