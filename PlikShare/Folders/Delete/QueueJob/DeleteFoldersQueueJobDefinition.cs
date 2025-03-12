namespace PlikShare.Folders.Delete.QueueJob;

public class DeleteFoldersQueueJobDefinition
{
    public required int[] FolderIds { get; init; }
    public required int WorkspaceId { get; init; }
    public required DateTimeOffset DeletedAt { get; init; }
}