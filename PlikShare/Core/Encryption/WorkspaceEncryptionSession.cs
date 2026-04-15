namespace PlikShare.Core.Encryption;

/// <summary>
/// Per-request holder for the unwrapped Workspace DEK of a full-encrypted workspace.
/// Populated by <see cref="Storages.Encryption.Authorization.ValidateWorkspaceEncryptionSessionFilter"/>
/// after loading the caller's wrap from <c>wek_workspace_encryption_keys</c> and unsealing it
/// with the X25519 private key read from the caller's <see cref="UserEncryptionSessionCookie"/>.
///
/// Also populated by presigned-URL validation filters, which carry the Workspace DEK inside
/// the DataProtection-sealed URL payload so unauthenticated direct upload/download requests
/// can still encrypt/decrypt file bytes without an ambient user session.
/// </summary>
public class WorkspaceEncryptionSession
{
    public const string HttpContextName = "WorkspaceEncryptionSession";

    public required byte[] WorkspaceDek { get; init; }
}
