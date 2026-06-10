using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;

namespace PlikShare.MediaProcessing.Generation;

// Server-side discovery of in-progress thumbnail generation for a workspace, mirroring
// ImageDimensionsBackfillStatusQuery: the batchId lives on the queue jobs (q_batch_id), so any
// user opening the workspace settings (or the same user after a reload) finds the active batch
// and its live progress. Covers both the policy backfill and explorer-triggered bulk generation —
// they share the job type, and for the settings card both just mean "thumbnails are being
// generated in this workspace".
public class ThumbnailsBackfillStatusQuery(PlikShareDb plikShareDb)
{
    public sealed record ActiveBackfill(
        Guid BatchId,
        BatchProgressQuery.Counts Counts);

    public ActiveBackfill? GetActive(int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        var batchId = GetLatestActiveBatchId(
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

    private static Guid? GetLatestActiveBatchId(
        SqliteConnection connection,
        int workspaceId)
    {
        var rows = connection
            .Cmd(
                sql: """
                    SELECT q_batch_id
                    FROM q_queue
                    WHERE q_workspace_id = $workspaceId
                        AND q_job_type = $jobType
                        AND q_batch_id IS NOT NULL
                    ORDER BY q_id DESC
                    LIMIT 1
                    """,
                readRowFunc: reader => reader.GetGuid(0))
            .WithParameter("$jobType", GenerateImageThumbnailsJobType.Value)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        return rows.Count > 0
            ? rows[0]
            : null;
    }

    public List<Guid> GetActiveBatchIds(int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        return GetActiveBatchIds(
            connection,
            workspaceId);
    }

    private static List<Guid> GetActiveBatchIds(
        SqliteConnection connection,
        int workspaceId)
    {
        return connection
            .Cmd(
                sql: """
                    SELECT DISTINCT q_batch_id
                    FROM q_queue
                    WHERE q_workspace_id = $workspaceId
                        AND q_job_type = $jobType
                        AND q_batch_id IS NOT NULL
                    ORDER BY q_batch_id
                    """,
                readRowFunc: reader => reader.GetGuid(0))
            .WithParameter("$jobType", GenerateImageThumbnailsJobType.Value)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }
}
