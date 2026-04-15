namespace PlikShare.Core.Encryption;

/// <summary>
/// Per-request holder for the unwrapped Workspace DEKs of a full-encrypted workspace,
/// keyed by the Storage DEK version each was derived from. Files record their
/// <c>StorageDekVersion</c> in the V2 header; consumers call <see cref="GetDekForVersion"/>.
///
/// Populated by <see cref="Storages.Encryption.Authorization.ValidateWorkspaceEncryptionSessionFilter"/>
/// after eagerly loading every wrap row in <c>wek_workspace_encryption_keys</c> for the
/// caller and unsealing each with the X25519 private key read from their
/// <see cref="UserEncryptionSessionCookie"/>.
///
/// Also populated by presigned-URL validation filters, which carry the same
/// <see cref="WorkspaceDekEntry"/> list inside the DataProtection-sealed URL payload so
/// unauthenticated direct upload/download requests can still encrypt/decrypt file bytes
/// without an ambient user session.
/// </summary>
public sealed class WorkspaceEncryptionSession
{
    public const string HttpContextName = "WorkspaceEncryptionSession";
    public WorkspaceDekEntry[] Entries { get; }

    public WorkspaceEncryptionSession(WorkspaceDekEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Length == 0)
            throw new ArgumentException(
                "WorkspaceEncryptionSession requires at least one Workspace DEK entry.",
                nameof(entries));

        Entries = entries;
    }

    public byte[] GetDekForVersion(int storageDekVersion)
    {
        var entry = Entries.FirstOrDefault(
            entry => entry.StorageDekVersion == storageDekVersion);

        if (entry is not null)
            return entry.Dek;

        throw new WorkspaceDekForVersionNotAvailableException(
            requestedStorageDekVersion: storageDekVersion,
            availableStorageDekVersions: Entries.Select(e => e.StorageDekVersion).ToArray());
    }
}
