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
        List<CompletedJob> NewCompleted,
        List<string> ProcessingFileExternalIds);

    public ThumbnailGenerationStatusResponseDto Execute(
        WorkspaceContext workspace,
        Guid batchId)
    {
        var snapshot = GetSnapshot(
            workspace,
            batchId,
            afterCompletedAt: null,
            includeOutstandingFileIds: true);

        var failedByVariant = new Dictionary<ThumbnailVariant, string?>();
        var ready = new List<ReadyThumbnailDto>();

        Apply(
            snapshot.NewCompleted,
            failedByVariant,
            ready);

        return BuildStatus(
            snapshot.Counts,
            failedByVariant,
            ready,
            snapshot.ProcessingFileExternalIds);
    }

    public Snapshot GetSnapshot(
        WorkspaceContext workspace,
        Guid batchId,
        DateTimeOffset? afterCompletedAt,
        bool includeOutstandingFileIds)
    {
        using var connection = plikShareDb.OpenConnection();

        // Authorize ONCE that the batch belongs to this workspace (batchId is a random GUID, so this
        // guards against a member of workspace A peeking at a batch of workspace B). After this, the
        // per-event counts/scans filter by the indexed q_batch_id alone — no per-row json_extract.
        if (!BatchBelongsToWorkspace(connection, workspace, batchId))
            return new Snapshot(
                Counts: new Counts(Completed: 0, Outstanding: 0, Failed: 0),
                NewCompleted: [],
                ProcessingFileExternalIds: []);

        return new Snapshot(
            Counts: GetCounts(connection, batchId),
            NewCompleted: GetCompletedSince(connection, batchId, afterCompletedAt),
            ProcessingFileExternalIds: includeOutstandingFileIds
                ? GetUnprocessedFileExternalIds(connection, batchId)
                : []);
    }

    private static bool BatchBelongsToWorkspace(
        SqliteConnection connection,
        WorkspaceContext workspace,
        Guid batchId)
    {
        var outstanding = connection
            .OneRowCmd(
                sql: """
                    SELECT 1
                    FROM q_queue
                    WHERE q_batch_id = $batchId
                        AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                    LIMIT 1
                    """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        if (!outstanding.IsEmpty)
            return true;

        var completed = connection
            .OneRowCmd(
                sql: """
                    SELECT 1
                    FROM qc_queue_completed
                    WHERE qc_batch_id = $batchId
                        AND json_extract(qc_definition, '$.workspaceId') = $workspaceId
                    LIMIT 1
                    """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return !completed.IsEmpty;
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
        // SUM(json_array_length(... files)) turns job-row counts into FILE counts. Indexed by
        // q_batch_id / qc_batch_id; per-row JSON parse is small (each row has at most BatchSize
        // entries in $.files). For 1000-file batch with BatchSize=10 → 100 rows × json scan.
        var result = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        (
                            SELECT COALESCE(SUM(json_array_length(json_extract(qc_definition, '$.files'))), 0)
                            FROM qc_queue_completed
                            WHERE qc_batch_id = $batchId
                        ),
                        (
                            SELECT COALESCE(SUM(json_array_length(json_extract(q_definition, '$.files'))), 0)
                            FROM q_queue
                            WHERE q_batch_id = $batchId
                        ),
                        (
                            SELECT COALESCE(SUM(json_array_length(json_extract(q_definition, '$.files'))), 0)
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
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(2));

                    var resultJson = reader.GetStringOrNull(3);

                    return new CompletedJob(
                        QcId: reader.GetInt64(0),
                        CompletedAt: reader.GetFieldValue<DateTimeOffset>(1),
                        Variants: definition?.Variants ?? [],
                        Result: resultJson is null
                            ? null
                            : Json.Deserialize<ThumbnailGenerationResult>(resultJson));
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$afterCompletedAt", afterCompletedAt)
            .Execute();
    }

    private static List<string> GetUnprocessedFileExternalIds(
        SqliteConnection connection,
        Guid batchId)
    {
        return connection
            .AggregateRows(
                sql: """
                    SELECT q_definition
                    FROM q_queue
                    WHERE
                        q_batch_id = $batchId
                        AND q_status != $failedStatus
                    """,
                seed: new List<string>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(0));

                    if (definition is null)
                        return acc;

                    foreach (var file in definition.Files)
                        acc.Add(file.ParentFileExternalId.Value);

                    return acc;
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$failedStatus", QueueStatus.Failed)
            .Execute();
    }
}
