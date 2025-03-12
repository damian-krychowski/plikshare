using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Workspaces.DeleteBucket;

public class DeleteBucketJobExecutor(
    StorageClientStore storageClientStore) : IQueueNormalJobExecutor
{
    public string JobType => DeleteBucketQueueJobType.Value;
    public int Priority => QueueJobPriority.ExtremelyLow;

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

        await storage.DeleteBucket(
            bucketName: definition.BucketName,
            cancellationToken: cancellationToken);
        
        Log.Information("Bucket '{BucketName}' in Storage#{StorageId} was deleted.",
            definition.BucketName,
            definition.StorageId);
        
        return QueueJobResult.Success;
    }
}