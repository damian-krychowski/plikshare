using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Files.BulkDelete.QueueJob;

public class BulkDeleteS3FileQueueJobExecutor(StorageClientStore storageClientStore) : IQueueLongRunningJobExecutor
{
    public string JobType => BulkDeleteS3FileQueueJobType.Value;
    public int Priority => QueueJobPriority.Low;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<BulkDeleteS3FileQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(BulkDeleteS3FileQueueJobDefinition)}'");
        }

        if (!storageClientStore.TryGetClient(definition.StorageId, out var storage))
        {
            Log.Warning("Could not delete files (count: {FilesToDeleteCount}) because Storage#{StorageId} was not found. Marking the queue job as completed.",
                definition.S3FileKeys.Length,
                definition.StorageId);

            return QueueJobResult.Success;
        }

        await storage.DeleteFiles(
            bucketName: definition.BucketName,
            keys: definition.S3FileKeys,
            cancellationToken: cancellationToken);

        return QueueJobResult.Success;
    }
}
