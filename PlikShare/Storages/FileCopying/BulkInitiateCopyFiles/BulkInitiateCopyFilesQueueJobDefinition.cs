namespace PlikShare.Storages.FileCopying.BulkInitiateCopyFiles;

public class BulkInitiateCopyFilesQueueJobDefinition
{
    public required FileIdAndHandler[] Files { get; init; }
    public required int SourceWorkspaceId { get; init; }
    public required int DestinationWorkspaceId { get; init; }
    public required string UserIdentityType { get; init; }
    public required string UserIdentity { get; init; }


    public class FileIdAndHandler
    {
        public required int Id { get; init; }
        public required CopyFileQueueOnCompletedActionDefinition OnCompleted { get; init; }
    }
}
