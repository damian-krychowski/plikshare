namespace PlikShare.Core.Emails.Definitions;

public record UserInvitationEmailDefinition(
    string InviterEmail,
    string InvitationCode);