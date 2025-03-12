using Amazon.S3;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using PlikShare.Uploads.Algorithm;
using Serilog;

namespace PlikShare.Uploads.Abort.QueueJob;

public class AbortS3UploadQueueJobExecutor(
    StorageClientStore storageClientStore) : IQueueNormalJobExecutor
{
    public string JobType => AbortS3UploadQueueJobType.Value;
    public int Priority => QueueJobPriority.ExtremelyLow;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<AbortS3UploadQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(AbortS3UploadQueueJobDefinition)}'");
        }
        
        if (!storageClientStore.TryGetClient(definition.StorageId, out var storage))
        {
            Log.Warning("Could not abort file upload of File '{FileExternalId}' because Storage#{StorageId} was not found. Marking the queue job as completed.",
                definition.FileExternalId,
                definition.StorageId);

            return QueueJobResult.Success;
        }

        var uploadAlgorithm = storage.ResolveUploadAlgorithm(
            fileSizeInBytes: definition.FileSizeInBytes);

        //only in multi step chunk upload we need to cancel startet operation on s3 side
        if (uploadAlgorithm.Algorithm != UploadAlgorithm.MultiStepChunkUpload)
            return QueueJobResult.Success;

        try
        {
            await storage.AbortMultiPartUpload(
                bucketName: definition.BucketName,
                key: new S3FileKey
                {
                    FileExternalId = definition.FileExternalId,
                    S3KeySecretPart = definition.S3KeySecretPart
                },
                uploadId: definition.S3UploadId,
                partETags: definition.PartETags,
                cancellationToken: cancellationToken);
        }
        catch (AmazonS3Exception amazonS3Exception)
        {
            if (amazonS3Exception.ErrorCode != "NoSuchUpload") 
                throw;
            
            Log.Warning("Could not abort Upload '{@Upload}' because it was not found. Operation finished.",
                definition);
        }

        return QueueJobResult.Success;
    }
}
