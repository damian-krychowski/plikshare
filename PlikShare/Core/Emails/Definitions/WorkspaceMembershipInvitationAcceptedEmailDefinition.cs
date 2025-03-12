namespace PlikShare.Core.Emails.Definitions;

public class WorkspaceMembershipInvitationAcceptedEmailDefinition
{
    public required string InviteeEmail {get;init;}
    public required string WorkspaceName { get; init; }
}