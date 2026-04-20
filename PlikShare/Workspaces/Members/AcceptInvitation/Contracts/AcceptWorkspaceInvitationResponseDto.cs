namespace PlikShare.Workspaces.Members.AcceptInvitation.Contracts;

public class AcceptWorkspaceInvitationResponseDto
{
    public required long WorkspaceCurrentSizeInBytes { get; init; }
    public required long? WorkspaceMaxSizeInBytes { get; init; }
    public required string StorageEncryptionType { get; init; }
    public required bool IsPendingKeyGrant { get; init; }
}