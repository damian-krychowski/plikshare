using PlikShare.Core.Encryption;

namespace PlikShare.Workspaces.Encryption;

public sealed class AllUserWorkspaceEncryptionSessions(
    Dictionary<int, WorkspaceEncryptionSession> sessionsByInternalId) : IDisposable
{
    public const string HttpContextName = "UserWorkspaceEncryptionSessions";

    public static AllUserWorkspaceEncryptionSessions Empty { get; } = new(
        sessionsByInternalId: new Dictionary<int, WorkspaceEncryptionSession>());

    private bool _disposed;
    
    public WorkspaceEncryptionSession? TryGet(int workspaceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return sessionsByInternalId.GetValueOrDefault(workspaceId);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var session in sessionsByInternalId.Values)
            session.Dispose();
    }
}
