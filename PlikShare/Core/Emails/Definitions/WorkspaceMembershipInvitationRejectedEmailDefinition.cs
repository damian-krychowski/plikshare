namespace PlikShare.Core.Emails.Definitions;

public class WorkspaceMembershipInvitationRejectedEmailDefinition
{
    public required string MemberEmail { get; init; }
    public required string WorkspaceName { get; init; }
}