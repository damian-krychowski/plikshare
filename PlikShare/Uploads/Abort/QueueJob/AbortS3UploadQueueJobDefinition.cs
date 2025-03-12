using PlikShare.Files.Id;

namespace PlikShare.Uploads.Abort.QueueJob;

public class AbortS3UploadQueueJobDefinition
{
    public required int StorageId{get; init;}
    public required string BucketName{get; init;}
    public required FileExtId FileExternalId{get; init;}
    public required string S3KeySecretPart{get; init;}
    public required string S3UploadId{get; init;}
    public required long FileSizeInBytes { get; init; }
    public required List<string> PartETags { get; init; }
};