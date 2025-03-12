namespace PlikShare.Core.Emails.Definitions;

public record BoxMembershipInvitationRejectedEmailDefinition(
    string InviteeEmail, 
    string BoxName);