using PlikShare.Storages.Id;

namespace PlikShare.Storages.Encryption;

public static class FullEncryptionSessionCookie
{
    public const string NamePrefix = "FullEncryptionSession_";
    public const string Purpose = "FullEncryptionSession";

    public static string GetCookieName(StorageExtId storageExternalId) =>
        NamePrefix + storageExternalId.Value;
}
