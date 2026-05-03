using PlikShare.Files.Id;
using PlikShare.Storages;

namespace PlikShare.Uploads.Abort.QueueJob;

/// <summary>
/// Queue-job payload for aborting an in-flight multipart upload. Identifies the
/// target object (storage, bucket, file external id, key secret part) and
/// carries a backend-specific <see cref="AbortHandle"/> describing what state
/// each storage type needs to clean up.
/// </summary>
public class AbortMultipartUploadQueueJobDefinition
{
    public required int StorageId { get; init; }
    public required string BucketName { get; init; }
    public required FileExtId FileExternalId { get; init; }
    public required string KeySecretPart { get; init; }
    public required MultipartUploadAbortHandle AbortHandle { get; init; }
}
