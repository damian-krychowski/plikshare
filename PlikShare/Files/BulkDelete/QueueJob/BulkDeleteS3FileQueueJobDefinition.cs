using PlikShare.Storages;

namespace PlikShare.Files.BulkDelete.QueueJob;

public class BulkDeleteS3FileQueueJobDefinition
{
    public required int StorageId {get; init;}
    public required string BucketName {get; init;}
    public required StorageObjectKey[] ObjectKeys {get; init;}
}