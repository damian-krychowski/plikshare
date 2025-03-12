using PlikShare.Files.Id;

namespace PlikShare.Files.Delete.QueueJob;

public class DeleteS3FileQueueJobDefinition
{
    public required int StorageId {get; init;}
    public required string BucketName {get; init;}
    public required FileExtId FileExternalId {get; init;}
    public required string S3KeySecretPart { get; init; }
}