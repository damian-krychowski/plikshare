using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.MediaProcessing.Generation.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

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
        HashSet<ThumbnailVariant> GeneratingVariants);

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

        return BuildStatus(
            snapshot.Counts, 
            snapshot.GeneratingVariants, 
            failedByVariant, 
            ready);
    }

    public Snapshot GetSnapshot(
        WorkspaceContext workspace,
        Guid batchId,
        DateTimeOffset? afterCompletedAt)
    {
        using var connection = plikShareDb.OpenConnection();

        return new Snapshot(
            Counts: GetCounts(connection, workspace, batchId),
            NewCompleted: GetCompletedSince(connection, workspace, batchId, afterCompletedAt),
            GeneratingVariants: GetGeneratingVariants(connection, workspace, batchId));
    }

    public static void Apply(
        List<CompletedJob> completedJobs,
        Dictionary<ThumbnailVariant, string?> failedByVariant,
        List<ReadyThumbnailDto> ready)
    {
        foreach (var job in completedJobs)
        {
            foreach (var variant in job.Variants)
            {
                failedByVariant[variant] = job
                    .Result
                    ?.FailedVariants
                    .FirstOrDefault(failed => failed.Variant == variant)
                    ?.Error;
            }

            if (job.Result is { GeneratedVariants.Count: > 0 } result)
            {
                ready.Add(new ReadyThumbnailDto
                {
                    FileExternalId = result.ParentFileExternalId.Value,
                    Variants = result.GeneratedVariants
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

    public static ThumbnailGenerationStatusResponseDto BuildStatus(
        Counts counts,
        HashSet<ThumbnailVariant> generatingVariants,
        Dictionary<ThumbnailVariant, string?> failedByVariant,
        List<ReadyThumbnailDto> ready)
    {
        var failedVariants = failedByVariant
            .Where(entry => entry.Value is not null && !generatingVariants.Contains(entry.Key))
            .Select(entry => new FailedThumbnailVariantDto
            {
                Variant = entry.Key,
                Error = entry.Value!
            })
            .ToList();

        return new ThumbnailGenerationStatusResponseDto
        {
            GeneratingVariants = generatingVariants.ToList(),
            FailedVariants = failedVariants,
            Total = counts.Completed + counts.Outstanding,
            Completed = counts.Completed,
            Failed = counts.Failed,
            Pending = counts.Outstanding - counts.Failed,
            ReadyThumbnails = ready
        };
    }

    private static Counts GetCounts(
        SqliteConnection connection,
        WorkspaceContext workspace,
        Guid batchId)
    {
        var result = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        (
                            SELECT COUNT(*)
                            FROM qc_queue_completed
                            WHERE qc_batch_id = $batchId
                                AND json_extract(qc_definition, '$.workspaceId') = $workspaceId
                        ),
                        (
                            SELECT COUNT(*)
                            FROM q_queue
                            WHERE q_batch_id = $batchId
                                AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                        ),
                        (
                            SELECT COUNT(*)
                            FROM q_queue
                            WHERE q_batch_id = $batchId
                                AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                                AND q_status = $failedStatus
                        )
                    """,
                readRowFunc: reader => new Counts(
                    Completed: reader.GetInt32(0),
                    Outstanding: reader.GetInt32(1),
                    Failed: reader.GetInt32(2)))
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$failedStatus", QueueStatus.Failed)
            .Execute();

        return result.Value;
    }

    private static List<CompletedJob> GetCompletedSince(
        SqliteConnection connection,
        WorkspaceContext workspace,
        Guid batchId,
        DateTimeOffset? afterCompletedAt)
    {
        // qc_id is the original enqueue id (q_id), not completion order, so jobs finishing
        // out-of-order can't be cursored by it. qc_completed_at is the completion clock; the
        // caller dedups by qc_id because $afterCompletedAt is inclusive (>=) to not drop ties.
        return connection
            .Cmd(
                sql: """
                    SELECT qc_id, qc_completed_at, qc_definition, qc_result
                    FROM qc_queue_completed
                    WHERE
                        qc_batch_id = $batchId
                        AND json_extract(qc_definition, '$.workspaceId') = $workspaceId
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
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$afterCompletedAt", afterCompletedAt)
            .Execute();
    }

    private static HashSet<ThumbnailVariant> GetGeneratingVariants(
        SqliteConnection connection,
        WorkspaceContext workspace,
        Guid batchId)
    {
        return connection
            .AggregateRows(
                sql: """
                    SELECT q_definition
                    FROM q_queue
                    WHERE
                        q_batch_id = $batchId
                        AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                        AND q_status IN ($pending, $processing)
                    """,
                seed: new HashSet<ThumbnailVariant>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(0));

                    if (definition is not null)
                        foreach (var variant in definition.Variants)
                            acc.Add(variant);

                    return acc;
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$pending", QueueStatus.Pending)
            .WithParameter("$processing", QueueStatus.Processing)
            .Execute();
    }
}
