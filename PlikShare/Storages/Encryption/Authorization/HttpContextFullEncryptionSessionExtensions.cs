using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption.Authorization;

public static class HttpContextFullEncryptionSessionExtensions
{
    public static FullEncryptionSession? TryGetFullEncryptionSession(this HttpContext httpContext)
    {
        return httpContext.Items[FullEncryptionSession.HttpContextName]
            as FullEncryptionSession;
    }
}
