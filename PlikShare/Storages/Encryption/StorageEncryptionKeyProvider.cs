using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption;

public class StorageEncryptionKeyProvider
{
    private readonly Dictionary<byte, StorageEncryptionKey> _keys;
    private readonly byte _latestVersion;

    public StorageEncryptionKeyProvider(IList<string> ikms)
    {
        _keys = new Dictionary<byte, StorageEncryptionKey>();

        for (byte i = 0; i < ikms.Count; i++)
        {
            _keys.Add(
                key: i,
                value: new StorageEncryptionKey(
                    Version: i,
                    Ikm: Convert.FromBase64String(ikms[i])));
        }

        _latestVersion = (byte)(ikms.Count - 1);
    }

    public StorageEncryptionKey GetEncryptionKey(byte version)
    {
        if(_keys.TryGetValue(version, out var key))
            return key;

        throw new EncryptionKeyNotFoundException(
            $"Could not find storage encryption key with version '{version}'");
    }

    public byte GetLatestKeyVersion() => _latestVersion;

    public class EncryptionKeyNotFoundException(string message) : Exception(message);

    public byte[] GetRandomSalt() => Aes256GcmStreaming.GenerateSalt();
    public byte[] GenerateRandomNoncePrefix() => Aes256GcmStreaming.GenerateNoncePrefix();
}