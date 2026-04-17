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

public abstract record StorageEncryption;

public sealed record NoStorageEncryption : StorageEncryption
{
    public static NoStorageEncryption Instance { get; } = new();
}

public sealed record ManagedStorageEncryption : StorageEncryption
{
    private readonly Dictionary<byte, byte[]> _decodedIkms;

    public StorageManagedEncryptionDetails Details { get; }
    public byte LatestKeyVersion { get; }

    public ManagedStorageEncryption(StorageManagedEncryptionDetails details)
    {
        Details = details;
        _decodedIkms = new Dictionary<byte, byte[]>();

        for (byte i = 0; i < details.Ikms.Count; i++)
            _decodedIkms[i] = Convert.FromBase64String(details.Ikms[i]);

        LatestKeyVersion = (byte)(details.Ikms.Count - 1);
    }

    public byte[] GetEncryptionKey(byte version)
    {
        if (_decodedIkms.TryGetValue(version, out var ikm))
            return ikm;

        throw new EncryptionKeyNotFoundException(
            $"Could not find storage encryption key with version '{version}'");
    }

    public class EncryptionKeyNotFoundException(string message) : Exception(message);
}

public sealed record FullStorageEncryption(
    StorageFullEncryptionDetails Details) : StorageEncryption;