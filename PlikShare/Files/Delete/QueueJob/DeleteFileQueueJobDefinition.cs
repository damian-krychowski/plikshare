using PlikShare.Files.Id;

namespace PlikShare.Files.Delete.QueueJob;

public class DeleteFileQueueJobDefinition
{
    public required int StorageId { get; init; }
    public required string BucketName { get; init; }
    public required FileExtId FileExternalId { get; init; }
    public required string KeySecretPart { get; init; }
}
