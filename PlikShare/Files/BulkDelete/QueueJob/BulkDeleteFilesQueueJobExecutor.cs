using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Files.BulkDelete.QueueJob;

public class BulkDeleteFilesQueueJobExecutor(StorageClientStore storageClientStore) : IQueueLongRunningJobExecutor
{
    public string JobType => BulkDeleteFilesQueueJobType.Value;
    public int Priority => QueueJobPriority.Low;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<BulkDeleteFilesQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(BulkDeleteFilesQueueJobDefinition)}'");
        }

        if (!storageClientStore.TryGetClient(definition.StorageId, out var storage))
        {
            Log.Warning("Could not delete files (count: {FilesToDeleteCount}) because Storage#{StorageId} was not found. Marking the queue job as completed.",
                definition.FileKeys.Length,
                definition.StorageId);

            return QueueJobResult.Success;
        }

        await storage.DeleteFiles(
            bucketName: definition.BucketName,
            keys: definition.FileKeys,
            cancellationToken: cancellationToken);

        return QueueJobResult.Success;
    }
}
