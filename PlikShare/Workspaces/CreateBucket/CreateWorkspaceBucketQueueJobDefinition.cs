namespace PlikShare.Workspaces.CreateBucket;

public record CreateWorkspaceBucketQueueJobDefinition(
    int WorkspaceId,
    string BucketName,
    int StorageId);