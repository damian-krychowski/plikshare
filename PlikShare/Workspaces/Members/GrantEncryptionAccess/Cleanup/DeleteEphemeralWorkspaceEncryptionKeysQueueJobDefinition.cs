namespace PlikShare.Workspaces.Members.GrantEncryptionAccess.Cleanup;

public class DeleteEphemeralWorkspaceEncryptionKeysQueueJobDefinition
{
    public required int WorkspaceId { get; init; }
    public required int UserId { get; init; }
}
