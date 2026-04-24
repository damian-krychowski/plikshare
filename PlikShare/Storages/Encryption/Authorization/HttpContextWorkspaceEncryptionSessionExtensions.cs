using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption.Authorization;

public static class HttpContextWorkspaceEncryptionSessionExtensions
{
    extension(HttpContext httpContext)
    {
        public WorkspaceEncryptionSession? TryGetWorkspaceEncryptionSession()
        {
            return httpContext.Items[WorkspaceEncryptionSession.HttpContextName]
                as WorkspaceEncryptionSession;
        }

        public EncryptableMetadata ToEncryptable(string value)
        {
            var wes = httpContext.TryGetWorkspaceEncryptionSession();
            return wes.ToEncryptableMetadata(value);
        }
    }
}
