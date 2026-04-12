namespace PlikShare.Storages.Encryption;

public class FullEncryptionKeyProvider
{
    private readonly Dictionary<byte, StorageEncryptionKey> _encryptedDeks;
    private readonly byte _latestVersion;
    
    public FullEncryptionKeyProvider(
        StorageFullEncryptionDetails encryptionDetails)
    {
        _encryptedDeks = new Dictionary<byte, StorageEncryptionKey>();

        for (byte i = 0; i < encryptionDetails.EncryptedDeks.Count; i++)
        {
            _encryptedDeks.Add(
                key: i,
                value: new StorageEncryptionKey(
                    Version: i,
                    Ikm: encryptionDetails.EncryptedDeks[i]));
        }

        _latestVersion = (byte)(encryptionDetails.EncryptedDeks.Count - 1);
    }

    public StorageEncryptionKey GetEncryptionKey(
        byte version,
        ReadOnlySpan<byte> kek)
    {
        if (!_encryptedDeks.TryGetValue(version, out var encryptedDek))
            throw new EncryptionKeyNotFoundException(
                $"Could not find encryption key with version '{version}'");

        var dek = StorageFullEncryptionService.Decrypt(
            kek: kek,
            encryptedData: encryptedDek.Ikm);

        return new StorageEncryptionKey(
            Version: version, 
            Ikm: dek);
    }

    public byte GetLatestKeyVersion() => _latestVersion;

    public class EncryptionKeyNotFoundException(string message) : Exception(message);

}