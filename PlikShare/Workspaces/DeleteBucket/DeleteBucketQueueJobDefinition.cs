namespace PlikShare.Workspaces.DeleteBucket;

public class DeleteBucketQueueJobDefinition
{
    public required string BucketName{get;init;}
    public required int StorageId { get; init; }
}