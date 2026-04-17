namespace PlikShare.Storages.Encryption;

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
