namespace PlikShare.Core.Emails.Definitions;

public record WorkspaceEncryptionKeyGrantApprovedEmailDefinition(
    string OwnerEmail,
    string WorkspaceName);
