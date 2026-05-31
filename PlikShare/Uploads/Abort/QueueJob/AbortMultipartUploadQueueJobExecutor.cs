using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Uploads.Abort.QueueJob;

public class AbortMultipartUploadQueueJobExecutor(
    StorageClientStore storageClientStore) : IQueueNormalJobExecutor
{
    public static string StaticJobType => AbortMultipartUploadQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.ExtremelyLow;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<AbortMultipartUploadQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(AbortMultipartUploadQueueJobDefinition)}'");
        }

        if (!storageClientStore.TryGetClient(definition.StorageId, out var storage))
        {
            Log.Warning(
                "Could not abort multipart upload of File '{FileExternalId}' because Storage#{StorageId} was not found. Marking the queue job as completed.",
                definition.FileExternalId,
                definition.StorageId);

            return QueueJobResult.Success;
        }

        await storage.AbortMultipartUpload(
            bucketName: definition.BucketName,
            key: new FileKey
            {
                FileExternalId = definition.FileExternalId,
                KeySecretPart = definition.KeySecretPart
            },
            handle: definition.AbortHandle,
            cancellationToken: cancellationToken);

        return QueueJobResult.Success;
    }
}
