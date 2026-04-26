using PlikShare.Workspaces.Id;

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
///
/// OWNERSHIP: This session owns every <see cref="SecureBytes"/> DEK in <see cref="Entries"/>
/// and disposes them in <see cref="Dispose"/>. Session lifetime is bound to the HTTP response
/// via <see cref="HttpResponse.RegisterForDispose"/>.
/// </summary>
public sealed class WorkspaceEncryptionSession : IDisposable
{
    public const string HttpContextName = "WorkspaceEncryptionSession";
    public int WorkspaceId{ get; }
    public WorkspaceDekEntry[] Entries { get; }

    private bool _disposed;
    private readonly int _latestIndex;

    public WorkspaceEncryptionSession(
        int workspaceId,
        WorkspaceDekEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Length == 0)
            throw new ArgumentException(
                "WorkspaceEncryptionSession requires at least one Workspace DEK entry.",
                nameof(entries));

        WorkspaceId = workspaceId;
        Entries = entries;

        _latestIndex = GetLatestIndex(entries);
    }

    private int GetLatestIndex(WorkspaceDekEntry[] entries)
    {
        var latestIndex = 0;
        var latestVersion = entries[0].StorageDekVersion;

        for (var i = 1; i < entries.Length; i++)
        {
            if (entries[i].StorageDekVersion <= latestVersion) 
                continue;

            latestVersion = entries[i].StorageDekVersion;
            latestIndex = i;
        }

        return latestIndex;
    }

    /// <summary>
    /// Returns the unwrapped Workspace DEK with the highest Storage DEK version.
    /// Used when encrypting new content, which should always use the latest key.
    ///
    /// OWNERSHIP: The returned <see cref="SecureBytes"/> is owned by this session and
    /// MUST NOT be disposed by the caller.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The session has already been disposed.</exception>
    public WorkspaceDekEntry GetLatestDek()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Entries[_latestIndex];
    }

    /// <summary>
    /// Returns the unwrapped Workspace DEK matching the given Storage DEK version.
    ///
    /// OWNERSHIP: The returned <see cref="SecureBytes"/> is owned by this session and
    /// MUST NOT be disposed by the caller. It remains valid until the session itself
    /// is disposed (typically at the end of the HTTP request). Callers should use
    /// <see cref="SecureBytes.Use"/> to read the DEK bytes within a local scope.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The session has already been disposed.</exception>
    /// <exception cref="WorkspaceDekForVersionNotAvailableException">
    /// No entry matches the requested <paramref name="storageDekVersion"/>.
    /// </exception>
    public SecureBytes GetDekForVersion(int storageDekVersion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].StorageDekVersion == storageDekVersion)
                return Entries[i].Dek;
        }

        throw new WorkspaceDekForVersionNotAvailableException(
            requestedStorageDekVersion: storageDekVersion,
            availableStorageDekVersions: Entries.Select(e => e.StorageDekVersion).ToArray());
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in Entries)
            entry.Dispose();
    }
}