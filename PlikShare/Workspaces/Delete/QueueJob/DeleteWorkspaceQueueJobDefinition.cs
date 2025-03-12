namespace PlikShare.Workspaces.Delete.QueueJob;

public class DeleteWorkspaceQueueJobDefinition
{
    public required int WorkspaceId { get; init; }
    public required DateTimeOffset DeletedAt { get; init; }
}