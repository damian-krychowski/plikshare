using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.S3;

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
    
    public static StorageEncryption GetStorageEncryption(
        StorageEncryptionType encryptionType,
        string? encryptionDetailsJson)
    {
        return encryptionType switch
        {
            StorageEncryptionType.None =>
                NoStorageEncryption.Instance,

            StorageEncryptionType.Managed => new ManagedStorageEncryption(
                details: Json.Deserialize<StorageManagedEncryptionDetails>(encryptionDetailsJson!)
                         ?? throw new InvalidOperationException("Managed encryption details cannot be null.")),

            StorageEncryptionType.Full => new FullStorageEncryption(
                Details: Json.Deserialize<StorageFullEncryptionDetails>(encryptionDetailsJson!)
                         ?? throw new InvalidOperationException("Full encryption details cannot be null.")),

            _ => throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null)
        };
    }

    extension(StorageEncryption encryption)
    {
        public StorageEncryptionType Type
        {
            get
            {
                return encryption switch
                {
                    NoStorageEncryption => StorageEncryptionType.None,
                    ManagedStorageEncryption => StorageEncryptionType.Managed,
                    FullStorageEncryption => StorageEncryptionType.Full,

                    _ => throw new InvalidOperationException(
                        $"Unsupported storage encryption type '{encryption.GetType().Name}'.")
                };
            }
        }

        public byte[]? EncryptJson(IDerivedMasterDataEncryption derivedEncryption)
        {
            return encryption switch
            {
                NoStorageEncryption => null,

                ManagedStorageEncryption managed => derivedEncryption.EncryptJson(managed.Details),

                FullStorageEncryption full => derivedEncryption.EncryptJson(full.Details),

                _ => throw new InvalidOperationException(
                    $"Unsupported storage encryption type '{encryption.GetType().Name}'.")
            };
        }
    }
}