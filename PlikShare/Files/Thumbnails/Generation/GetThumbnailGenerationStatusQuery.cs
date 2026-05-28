using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.Files.Thumbnails.Generation.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Thumbnails.Generation;

/// <summary>
/// Reconstructs a thumbnail generation batch's status from the queue tables alone: in-flight
/// variants come from pending/processing rows in <c>q_queue</c>; failures come from the latest
/// completed job per variant in <c>qc_queue_completed</c> (its <c>qc_result</c> payload). Both are
/// keyed by the <c>q_batch_id</c> / <c>qc_batch_id</c> column and confined to the caller's
/// workspace in SQL (via the definition's <c>workspaceId</c>) so a batch id can never cross a
/// tenant boundary. No dedicated status table — the queue is the source of truth.
/// </summary>
public class GetThumbnailGenerationStatusQuery(PlikShareDb plikShareDb)
{
    public ThumbnailGenerationStatusResponseDto Execute(
        WorkspaceContext workspace,
        Guid batchId)
    {
        using var connection = plikShareDb.OpenConnection();

        var generatingVariants = connection
            .AggregateRows(
                sql: """
                    SELECT q_definition
                    FROM q_queue
                    WHERE
                        q_batch_id = $batchId
                        AND q_status IN ($pending, $processing)
                        AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                    """,
                seed: new HashSet<ThumbnailVariant>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(0));

                    if (definition is not null)
                    {
                        foreach (var variant in definition.Variants)
                            acc.Add(variant);
                    }

                    return acc;
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$pending", QueueStatus.Pending)
            .WithParameter("$processing", QueueStatus.Processing)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        // Rows are ascending by qc_id, so a later job overrides an earlier one per variant. A
        // variant absent from a job's failures succeeded (null); a variant present failed (error).
        var latestErrorByVariant = connection
            .AggregateRows(
                sql: """
                    SELECT qc_definition, qc_result
                    FROM qc_queue_completed
                    WHERE
                        qc_batch_id = $batchId
                        AND json_extract(qc_definition, '$.workspaceId') = $workspaceId
                    ORDER BY qc_id ASC
                    """,
                seed: new Dictionary<ThumbnailVariant, string?>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(0));

                    if (definition is null)
                        return acc;

                    var resultJson = reader.GetStringOrNull(1);

                    var failures = resultJson is null
                        ? null
                        : Json.Deserialize<ThumbnailGenerationResult>(resultJson);

                    foreach (var variant in definition.Variants)
                    {
                        acc[variant] = failures?
                            .FailedVariants
                            .FirstOrDefault(failed => failed.Variant == variant)?
                            .Error;
                    }

                    return acc;
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        var failedVariants = latestErrorByVariant
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
            FailedVariants = failedVariants
        };
    }
}
