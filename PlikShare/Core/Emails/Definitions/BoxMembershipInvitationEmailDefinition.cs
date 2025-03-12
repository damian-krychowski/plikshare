namespace PlikShare.Core.Emails.Definitions;

public record BoxMembershipInvitationEmailDefinition(
    string InviterEmail,
    string BoxName,
    string? InvitationCode);