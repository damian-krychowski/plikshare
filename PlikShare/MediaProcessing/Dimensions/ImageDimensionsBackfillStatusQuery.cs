using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;

namespace PlikShare.MediaProcessing.Dimensions;

// Server-side discovery of an in-progress image-dimensions backfill for a workspace. Because the
// batchId lives on the queue jobs (q_batch_id) — not on the workspace row — any user opening the
// workspace settings (or the same user after a reload) can find the active batch and its live
// progress without it being stored anywhere extra. "Active" = there is at least one still-queued
// job for that batch; once every job moves to qc_queue_completed the backfill is done and this
// returns null.
public class ImageDimensionsBackfillStatusQuery(PlikShareDb plikShareDb)
{
    public sealed record ActiveBackfill(
        Guid BatchId,
        BatchProgressQuery.Counts Counts);

    public ActiveBackfill? GetActive(int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        var batchId = GetActiveBatchId(
            connection,
            workspaceId);

        if (batchId is null)
            return null;

        var counts = BatchProgressQuery.GetCounts(
            connection,
            batchId.Value);

        return new ActiveBackfill(
            BatchId: batchId.Value,
            Counts: counts);
    }

    public Guid? GetActiveBatchId(int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        return GetActiveBatchId(
            connection,
            workspaceId);
    }

    private static Guid? GetActiveBatchId(
        SqliteConnection connection,
        int workspaceId)
    {
        var rows = connection
            .Cmd(
                sql: """
                    SELECT q_batch_id
                    FROM q_queue
                    WHERE q_job_type = $jobType
                        AND q_batch_id IS NOT NULL
                        AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                    ORDER BY q_id DESC
                    LIMIT 1
                    """,
                readRowFunc: reader => reader.GetGuid(0))
            .WithParameter("$jobType", ExtractImageDimensionsQueueJobType.Value)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        return rows.Count > 0
            ? rows[0]
            : null;
    }
}
