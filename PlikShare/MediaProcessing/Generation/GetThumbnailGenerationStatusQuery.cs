using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.MediaProcessing.Generation.Contracts;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Reads batched-thumbnail-generation progress from the queue tables. Counts are expressed in
/// FILES (not jobs), because one job covers up to <c>BatchSize</c> parents — the UI cares
/// about file-level progress. Per-file processing state lives in the file-processing endpoints;
/// this query reports batch counts and per-variant failures only.
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
        Guid batchId)
    {
        var snapshot = GetSnapshot(
            batchId,
            afterCompletedAt: null);

        var failedByVariant = new Dictionary<ThumbnailVariant, string?>();

        Apply(
            snapshot.NewCompleted,
            failedByVariant);

        return BuildStatus(
            snapshot.Counts,
            failedByVariant);
    }

    public Snapshot GetSnapshot(
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
        Dictionary<ThumbnailVariant, string?> failedByVariant)
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
            }
        }
    }

    public static ThumbnailGenerationStatusResponseDto BuildStatus(
        Counts counts,
        Dictionary<ThumbnailVariant, string?> failedByVariant)
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
            Pending = counts.Outstanding - counts.Failed
        };
    }

    private static Counts GetCounts(
        SqliteConnection connection,
        Guid batchId)
    {
        var counts = BatchProgressQuery.GetCounts(
            connection,
            batchId);

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
                    var definition = Json.Deserialize<GenerateImageThumbnailsJobDefinition>(
                        reader.GetString(2));

                    var resultJson = reader.GetStringOrNull(3);

                    return new CompletedJob(
                        QcId: reader.GetInt64(0),
                        CompletedAt: reader.GetFieldValue<DateTimeOffset>(1),
                        Variants: definition?.Variants.ToList() ?? [],
                        Result: resultJson is null
                            ? null
                            : Json.Deserialize<ThumbnailGenerationResult>(resultJson));
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$afterCompletedAt", afterCompletedAt)
            .Execute();
    }
}
