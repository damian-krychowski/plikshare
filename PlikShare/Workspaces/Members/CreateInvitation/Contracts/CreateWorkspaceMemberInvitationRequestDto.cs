namespace PlikShare.Workspaces.Members.CreateInvitation.Contracts;

public record CreateWorkspaceMemberInvitationRequestDto(
    List<string> MemberEmails,
    bool AllowShare);