using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption;

public record StorageEncryptionDetails
{
    public StorageManagedEncryptionDetails? Managed { get; }
    public StorageFullEncryptionDetails? Full { get; }

    private StorageEncryptionDetails(
        StorageManagedEncryptionDetails? managed,
        StorageFullEncryptionDetails? full)
    {
        Managed = managed;
        Full = full;
    }

    public static implicit operator StorageEncryptionDetails(
        StorageManagedEncryptionDetails managed)
    {
        return new StorageEncryptionDetails(
            managed: managed, 
            full: null);
    }

    public static implicit operator StorageEncryptionDetails(
        StorageFullEncryptionDetails full)
    {
        return new StorageEncryptionDetails(
            managed: null, 
            full: full);
    }
}

public static class StorageEncryptionDetailsExtensions
{
    extension(StorageEncryptionDetails? encryptionDetails)
    {
        public byte[]? EncryptJson(IDerivedMasterDataEncryption derivedEncryption)
        {
            if (encryptionDetails is null)
                return null;

            if (encryptionDetails.Managed is not null)
                return derivedEncryption.EncryptJson(encryptionDetails.Managed);

            if(encryptionDetails.Full is not null)
                return derivedEncryption.EncryptJson(encryptionDetails.Full);

            throw new InvalidOperationException(
                "StorageEncryptionDetails must have either Managed or Full set.");
        }
    }
}