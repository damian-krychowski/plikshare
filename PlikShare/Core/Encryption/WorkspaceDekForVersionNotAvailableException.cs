namespace PlikShare.Core.Encryption;

/// <summary>
/// Thrown by <see cref="WorkspaceEncryptionSession.GetDekForVersion"/> when the session does
/// not carry a Workspace DEK for the requested Storage DEK version. Practically this means
/// the caller is authenticated and has some wraps for the workspace, but lacks the wrap
/// matching the file or current-latest version in play — for example a rotation did not
/// backfill their wraps, or a legacy file references a version they were never given.
///
/// Handled globally by <see cref="Authorization.WorkspaceDekUnavailableExceptionHandler"/>
/// which maps it to HTTP 403 <c>workspace-encryption-access-denied</c>.
/// </summary>
public sealed class WorkspaceDekForVersionNotAvailableException : Exception
{
    public int RequestedStorageDekVersion { get; }
    public int[] AvailableStorageDekVersions { get; }

    public WorkspaceDekForVersionNotAvailableException(
        int requestedStorageDekVersion,
        int[] availableStorageDekVersions)
        : base(BuildMessage(requestedStorageDekVersion, availableStorageDekVersions))
    {
        RequestedStorageDekVersion = requestedStorageDekVersion;
        AvailableStorageDekVersions = availableStorageDekVersions;
    }

    private static string BuildMessage(int requested, int[] available) =>
        $"No Workspace DEK available for Storage DEK version {requested}. " +
        $"Session carries versions: [{string.Join(", ", available)}].";
}
