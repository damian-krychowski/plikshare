using PlikShare.Users.Id;

namespace PlikShare.Workspaces.Members.CreateInvitation.Contracts;

public class CreateWorkspaceMemberInvitationResponseDto
{
    public required List<WorkspaceInvitationMember> Members { get; set; }

    public record WorkspaceInvitationMember(
        string Email, 
        UserExtId ExternalId);
}