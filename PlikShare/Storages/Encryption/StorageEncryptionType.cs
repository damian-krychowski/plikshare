using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;

namespace PlikShare.Storages.Encryption;

public enum StorageEncryptionType
{
    None = 0,
    Managed = 1
}

public static class StorageEncryptionExtensions
{
    public static string ToDbValue(this StorageEncryptionType encryptionType)
    {
        return encryptionType switch
        {
            StorageEncryptionType.None => "none",
            StorageEncryptionType.Managed => "managed",
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
            _ => throw new ArgumentOutOfRangeException(nameof(dbValue), dbValue, null)
        };
    }

    public static StorageManagedEncryptionDetails? GetEncryptionDetails(
        StorageEncryptionType encryptionType,
        string encryptionDetailsJson)
    {
        return encryptionType switch
        {
            StorageEncryptionType.None => null,
            
            StorageEncryptionType.Managed => Json.Deserialize<StorageManagedEncryptionDetails>(
                encryptionDetailsJson),

            _ => throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null)
        };
    }

    public static StorageEncryptionKeyProvider? PrepareEncryptionKeyProvider(
        StorageManagedEncryptionDetails? encryptionDetails)
    {
        switch (encryptionDetails)
        {
            case null:
                return null;

            case var managedEncryptionDetails:
                return new StorageEncryptionKeyProvider(managedEncryptionDetails.Ikms);
        }
    }

    public static StorageManagedEncryptionDetails? PrepareEncryptionDetails(
        StorageEncryptionType encryptionType)
    {
        return encryptionType switch
        {
            StorageEncryptionType.None => null,
            StorageEncryptionType.Managed => new StorageManagedEncryptionDetails(
                Ikms:
                [
                    Convert.ToBase64String(Aes256GcmStreaming.GenerateIkm())
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null)
        };
    }
}