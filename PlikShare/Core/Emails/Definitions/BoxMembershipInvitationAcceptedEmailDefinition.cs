namespace PlikShare.Core.Emails.Definitions;

public record BoxMembershipInvitationAcceptedEmailDefinition(
    string InviteeEmail, 
    string BoxName);