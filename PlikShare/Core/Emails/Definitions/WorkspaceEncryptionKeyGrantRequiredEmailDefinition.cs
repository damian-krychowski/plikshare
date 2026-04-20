namespace PlikShare.Core.Emails.Definitions;

public record WorkspaceEncryptionKeyGrantRequiredEmailDefinition(
    string InviteeEmail,
    string WorkspaceName);
