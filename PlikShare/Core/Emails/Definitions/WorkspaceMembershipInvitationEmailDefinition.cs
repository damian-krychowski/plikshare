namespace PlikShare.Core.Emails.Definitions;

public record WorkspaceMembershipInvitationEmailDefinition(
    string InviterEmail,
    string WorkspaceName,
    string? InvitationCode);