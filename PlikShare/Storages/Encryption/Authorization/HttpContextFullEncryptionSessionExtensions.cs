using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption.Authorization;

public static class HttpContextFullEncryptionSessionExtensions
{
    public static WorkspaceEncryptionSession? TryGetWorkspaceEncryptionSession(this HttpContext httpContext)
    {
        return httpContext.Items[WorkspaceEncryptionSession.HttpContextName]
            as WorkspaceEncryptionSession;
    }
}
