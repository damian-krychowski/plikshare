using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.MediaProcessing.Generation.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Reads batched-thumbnail-generation progress from the queue tables. Counts are expressed in
/// FILES (not jobs), because one job now covers up to <c>BatchSize</c> parents — the UI cares
/// about file-level progress. File counts are derived from <c>json_array_length(... '$.files')</c>
/// on each row.
/// </summary>
public class GetThumbnailGenerationStatusQuery(PlikShareDb plikShareDb)
{
    public sealed record Counts(int Completed, int Outstanding, int Failed);

    public sealed record CompletedJob(
        long QcId,
        DateTimeOffset CompletedAt,
        List<ThumbnailVariant> Variants,
        ThumbnailGenerationResult? Result);

    public sealed record Snapshot(
        Counts Counts,
        List<CompletedJob> NewCompleted);

    public ThumbnailGenerationStatusResponseDto Execute(
        WorkspaceContext workspace,
        Guid batchId)
    {
        var snapshot = GetSnapshot(
            workspace,
            batchId,
            afterCompletedAt: null);

        var failedByVariant = new Dictionary<ThumbnailVariant, string?>();
        var ready = new List<ReadyThumbnailDto>();

        Apply(
            snapshot.NewCompleted,
            failedByVariant,
            ready);

        // Outstanding file ids are streamed separately (SSE chunks) — this one-shot status never
        // carries them; its readers only need the counts.
        return BuildStatus(
            snapshot.Counts,
            failedByVariant,
            ready,
            []);
    }

    public Snapshot GetSnapshot(
        WorkspaceContext workspace,
        Guid batchId,
        DateTimeOffset? afterCompletedAt)
    {
        using var connection = plikShareDb.OpenConnection();
        
        return new Snapshot(
            Counts: GetCounts(
                connection, 
                batchId),

            NewCompleted: GetCompletedSince(
                connection, 
                batchId, afterCompletedAt));
    }

    public static void Apply(
        List<CompletedJob> completedJobs,
        Dictionary<ThumbnailVariant, string?> failedByVariant,
        List<ReadyThumbnailDto> ready)
    {
        foreach (var job in completedJobs)
        {
            if (job.Result is null)
                continue;

            foreach (var fileResult in job.Result.Files)
            {
                foreach (var variant in job.Variants)
                {
                    failedByVariant[variant] = fileResult
                        .FailedVariants
                        .FirstOrDefault(failed => failed.Variant == variant)
                        ?.Error;
                }

                if (fileResult.GeneratedVariants.Count > 0)
                {
                    ready.Add(new ReadyThumbnailDto
                    {
                        FileExternalId = fileResult.ParentFileExternalId.Value,
                        Variants = fileResult.GeneratedVariants
                            .Select(generated => new ReadyThumbnailVariantDto
                            {
                                Variant = generated.Variant,
                                Etag = generated.Etag
                            })
                            .ToList()
                    });
                }
            }
        }
    }

    public static ThumbnailGenerationStatusResponseDto BuildStatus(
        Counts counts,
        Dictionary<ThumbnailVariant, string?> failedByVariant,
        List<ReadyThumbnailDto> ready,
        List<string> processingFileExternalIds)
    {
        var failedVariants = failedByVariant
            .Where(entry => entry.Value is not null)
            .Select(entry => new FailedThumbnailVariantDto
            {
                Variant = entry.Key,
                Error = entry.Value!
            })
            .ToList();

        return new ThumbnailGenerationStatusResponseDto
        {
            FailedVariants = failedVariants,
            Total = counts.Completed + counts.Outstanding,
            Completed = counts.Completed,
            Failed = counts.Failed,
            Pending = counts.Outstanding - counts.Failed,
            ReadyThumbnails = ready,
            ProcessingFileExternalIds = processingFileExternalIds
        };
    }

    private static Counts GetCounts(
        SqliteConnection connection,
        Guid batchId)
    {
        // File-level counts come from the shared batch-progress query; thumbnail job definitions
        // hold their parents under '$.files'.
        var counts = BatchProgressQuery.GetCounts(
            connection,
            batchId,
            filesJsonPath: "$.files");

        return new Counts(
            Completed: counts.Completed,
            Outstanding: counts.Outstanding,
            Failed: counts.Failed);
    }

    private static List<CompletedJob> GetCompletedSince(
        SqliteConnection connection,
        Guid batchId,
        DateTimeOffset? afterCompletedAt)
    {
        return connection
            .Cmd(
                sql: """
                    SELECT qc_id, qc_completed_at, qc_definition, qc_result
                    FROM qc_queue_completed
                    WHERE
                        qc_batch_id = $batchId
                        AND ($afterCompletedAt IS NULL OR qc_completed_at >= $afterCompletedAt)
                    ORDER BY qc_completed_at ASC
                    """,
                readRowFunc: reader =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinitionV2>(
                        reader.GetString(2));

                    var resultJson = reader.GetStringOrNull(3);

                    return new CompletedJob(
                        QcId: reader.GetInt64(0),
                        CompletedAt: reader.GetFieldValue<DateTimeOffset>(1),
                        Variants: definition?.Files[0].GetVariants() ?? [],
                        Result: resultJson is null
                            ? null
                            : Json.Deserialize<ThumbnailGenerationResult>(resultJson));
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$afterCompletedAt", afterCompletedAt)
            .Execute();
    }

    public sealed record OutstandingPage(
        List<string> FileExternalIds,
        long? LastQId,
        bool HasMore);

    // Keyset-paginated outstanding file ids for the initial SSE chunk stream. Authorization is done
    // once by the caller's GetSnapshot before paging starts, so here we filter by the indexed
    // q_batch_id (a random GUID) and keyset on q_id — a 30k-file batch streams in bounded-memory
    // pages instead of materializing one ~1 MB list. rowLimit is in q_queue ROWS; each row holds up
    // to BatchSize files, so ~200 rows ≈ 2000 file ids per page.
    public OutstandingPage GetUnprocessedFileExternalIdsPage(
        Guid batchId,
        long? afterQId,
        int rowLimit)
    {
        using var connection = plikShareDb.OpenConnection();

        var rows = connection
            .AggregateRows(
                sql: """
                    SELECT 
                        q_id, 
                        q_definition
                    FROM q_queue
                    WHERE
                        q_batch_id = $batchId
                        AND q_status != $failedStatus
                        AND ($afterQId IS NULL OR q_id > $afterQId)
                    ORDER BY q_id
                    LIMIT $rowLimit
                    """,
                seed: new OutstandingPageAcc(
                    FileExternalIds: new List<string>(rowLimit * 10),
                    LastQId: afterQId,
                    RowCount: 0),
                aggregateRowFunc: (acc, row) =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinitionV2>(
                        row.GetString(1));

                    foreach (var file in definition!.Files)
                    {
                        acc.FileExternalIds.Add(
                            file.ParentFileExternalId.Value);
                    }

                    return new OutstandingPageAcc(
                        FileExternalIds: acc.FileExternalIds,
                        LastQId: row.GetInt64(0),
                        RowCount: acc.RowCount + 1);
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$failedStatus", QueueStatus.Failed)
            .WithParameter("$afterQId", afterQId)
            .WithParameter("$rowLimit", rowLimit)
            .Execute();

        // HasMore is measured in q_queue ROWS, not file ids — each row holds up to BatchSize files,
        // so FileExternalIds.Count is a multiple of the row count. Comparing ids to the row limit
        // would always be false and stop paging after the first chunk.
        return new OutstandingPage(
            FileExternalIds: rows.FileExternalIds,
            LastQId: rows.LastQId,
            HasMore: rows.RowCount == rowLimit);
    }

    private readonly record struct OutstandingPageAcc(
        List<string> FileExternalIds,
        long? LastQId,
        int RowCount);
}
