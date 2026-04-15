using PlikShare.Core.Utils;

namespace PlikShare.Storages.Encryption;

public enum StorageEncryptionType
{
    None = 0,
    Managed = 1,
    Full = 2
}

public static class StorageEncryptionExtensions
{
    public static string ToDbValue(this StorageEncryptionType encryptionType)
    {
        return encryptionType switch
        {
            StorageEncryptionType.None => "none",
            StorageEncryptionType.Managed => "managed",
            StorageEncryptionType.Full => "full",
            _ => throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null)
        };
    }

    public static StorageEncryptionType FromDbValue(string? dbValue)
    {
        return dbValue switch
        {
            null => StorageEncryptionType.None, //by default all storages does not have encryption
            "none" => StorageEncryptionType.None,
            "managed" => StorageEncryptionType.Managed,
            "full" => StorageEncryptionType.Full,
            _ => throw new ArgumentOutOfRangeException(nameof(dbValue), dbValue, null)
        };
    }

    public static StorageEncryptionDetails? GetEncryptionDetails(
        StorageEncryptionType encryptionType,
        string encryptionDetailsJson)
    {
        return encryptionType switch
        {
            StorageEncryptionType.None => null,

            StorageEncryptionType.Managed => Json.Deserialize<StorageManagedEncryptionDetails>(
                encryptionDetailsJson),

            StorageEncryptionType.Full => Json.Deserialize<StorageFullEncryptionDetails>(
                encryptionDetailsJson),

            _ => throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null)
        };
    }

    public static ManagedEncryptionKeyProvider? PrepareEncryptionKeyProvider(
        StorageEncryptionDetails? encryptionDetails)
    {
        if (encryptionDetails is null)
            return null;

        if (encryptionDetails.Managed is not null)
        {
            return new ManagedEncryptionKeyProvider(
                encryptionDetails.Managed.Ikms);
        }

        if (encryptionDetails.Full is not null)
            return null;

        throw new InvalidOperationException(
            "StorageEncryptionDetails must have either Managed or Full set.");
    }

}