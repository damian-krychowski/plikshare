using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.Files.Thumbnails.Generation.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Thumbnails.Generation;

/// <summary>
/// Reconstructs a thumbnail generation batch's status from the queue tables alone, serving both
/// the single-file view (per-variant generating/failed) and the bulk view (per-file counts).
/// In-flight work comes from <c>q_queue</c> (pending/processing/failed), finished work from
/// <c>qc_queue_completed</c>; both keyed by the <c>q_batch_id</c> / <c>qc_batch_id</c> column and
/// confined to the caller's workspace in SQL so a batch id can never cross a tenant boundary.
/// </summary>
public class GetThumbnailGenerationStatusQuery(PlikShareDb plikShareDb)
{
    public ThumbnailGenerationStatusResponseDto Execute(
        WorkspaceContext workspace,
        Guid batchId)
    {
        using var connection = plikShareDb.OpenConnection();

        var outstanding = connection
            .AggregateRows(
                sql: """
                    SELECT q_status, q_definition
                    FROM q_queue
                    WHERE
                        q_batch_id = $batchId
                        AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                    """,
                seed: new OutstandingAccumulator(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var status = reader.GetString(0);

                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(1));

                    if (definition is null)
                        return acc;

                    if (status == QueueStatus.Failed)
                    {
                        acc.Failed++;
                        return acc;
                    }

                    // pending / processing / blocked — still outstanding.
                    acc.Pending++;

                    if (status is QueueStatus.Pending or QueueStatus.Processing)
                    {
                        foreach (var variant in definition.Variants)
                            acc.GeneratingVariants.Add(variant);
                    }

                    return acc;
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        var completed = connection
            .AggregateRows(
                sql: """
                    SELECT qc_definition, qc_result
                    FROM qc_queue_completed
                    WHERE
                        qc_batch_id = $batchId
                        AND json_extract(qc_definition, '$.workspaceId') = $workspaceId
                    ORDER BY qc_id ASC
                    """,
                seed: new CompletedAccumulator(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(0));

                    if (definition is null)
                        return acc;

                    acc.Count++;

                    var resultJson = reader.GetStringOrNull(1);

                    var failures = resultJson is null
                        ? null
                        : Json.Deserialize<ThumbnailGenerationResult>(resultJson);

                    // Rows are ascending by qc_id, so a later job overrides an earlier one per
                    // variant: absent from failures = succeeded (null), present = failed (error).
                    foreach (var variant in definition.Variants)
                    {
                        acc.LatestErrorByVariant[variant] = failures?
                            .FailedVariants
                            .FirstOrDefault(failed => failed.Variant == variant)?
                            .Error;
                    }

                    return acc;
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        var failedVariants = completed.LatestErrorByVariant
            .Where(entry => entry.Value is not null && !outstanding.GeneratingVariants.Contains(entry.Key))
            .Select(entry => new FailedThumbnailVariantDto
            {
                Variant = entry.Key,
                Error = entry.Value!
            })
            .ToList();

        return new ThumbnailGenerationStatusResponseDto
        {
            GeneratingVariants = outstanding.GeneratingVariants.ToList(),
            FailedVariants = failedVariants,
            Total = outstanding.Pending + outstanding.Failed + completed.Count,
            Completed = completed.Count,
            Failed = outstanding.Failed,
            Pending = outstanding.Pending
        };
    }

    private sealed class OutstandingAccumulator
    {
        public HashSet<ThumbnailVariant> GeneratingVariants { get; } = [];
        public int Pending { get; set; }
        public int Failed { get; set; }
    }

    private sealed class CompletedAccumulator
    {
        public Dictionary<ThumbnailVariant, string?> LatestErrorByVariant { get; } = [];
        public int Count { get; set; }
    }
}
