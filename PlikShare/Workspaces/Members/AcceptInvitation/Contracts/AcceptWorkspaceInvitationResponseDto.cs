namespace PlikShare.Workspaces.Members.AcceptInvitation.Contracts;

public class AcceptWorkspaceInvitationResponseDto
{
    public required long WorkspaceCurrentSizeInBytes { get; init; }
    public required long? WorkspaceMaxSizeInBytes { get; init; }
}