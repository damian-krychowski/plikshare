using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Files.Delete.QueueJob;

public class DeleteFileQueueJobExecutor(StorageClientStore storageClientStore) : IQueueNormalJobExecutor
{
    public string JobType => DeleteFileQueueJobType.Value;
    public int Priority => QueueJobPriority.Low;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<DeleteFileQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteFileQueueJobDefinition)}'");
        }

        if (!storageClientStore.TryGetClient(definition.StorageId, out var storage))
        {
            Log.Warning("Could not delete file '{FileExternalId}' because Storage#{StorageId} was not found. Marking delete file job as completed.",
                definition.FileExternalId,
                definition.StorageId);

            return QueueJobResult.Success;
        }

        await storage.DeleteFile(
            bucketName: definition.BucketName,
            key: new FileKey
            {
                FileExternalId = definition.FileExternalId,
                KeySecretPart = definition.KeySecretPart
            },
            cancellationToken: cancellationToken);

        return QueueJobResult.Success;
    }
}
