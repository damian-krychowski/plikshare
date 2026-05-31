using Amazon.S3;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Workspaces.DeleteBucket;

public class DeleteBucketJobExecutor(
    StorageClientStore storageClientStore) : IQueueNormalJobExecutor
{
    public static string StaticJobType => DeleteBucketQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.ExtremelyLow;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<DeleteBucketQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteBucketQueueJobDefinition)}'");
        }

        if (!storageClientStore.TryGetClient(definition.StorageId, out var storage))
        {
            Log.Warning("Could not delete bucket {BucketName} because Storage#{StorageId} was not found. Marking the queue job as completed.",
                definition.BucketName,
                definition.StorageId);

            return QueueJobResult.Success;
        }

        try
        {
            await storage.DeleteBucket(
                bucketName: definition.BucketName,
                cancellationToken: cancellationToken);
        }
        catch (AmazonS3Exception e) when (e.ErrorCode == "BucketNotEmpty")
        {
            Log.Warning("Bucket '{BucketName}' in Storage#{StorageId} is not yet empty (eventual consistency). Scheduling soft retry.",
                definition.BucketName,
                definition.StorageId);

            return QueueJobResult.NeedsRetry(
                maxAttempts: 3,
                delay: TimeSpan.FromDays(1));
        }

        Log.Information("Bucket '{BucketName}' in Storage#{StorageId} was deleted.",
            definition.BucketName,
            definition.StorageId);

        return QueueJobResult.Success;
    }
}