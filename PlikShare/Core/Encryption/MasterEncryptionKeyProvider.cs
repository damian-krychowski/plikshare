namespace PlikShare.Core.Encryption;

public class MasterEncryptionKeyProvider(IList<string> encryptionPasswords)
{
    private readonly List<MasterEncryptionKey> _masterKeys = encryptionPasswords
        .Select((password, index) => ToEncryptionKey(index, password))
        .ToList();

    private static MasterEncryptionKey ToEncryptionKey(int index, string password)
    {
        var keyId = index + 1;

        if (keyId > byte.MaxValue)
            throw new InvalidOperationException("To many encryption passwords. Only 255 are supported;");

        return new MasterEncryptionKey(
            (byte)keyId,
            password);
    }

    public MasterEncryptionKey GetCurrentEncryptionKey() => _masterKeys.Last();
    public MasterEncryptionKey GetEncryptionKeyById(byte keyId) => _masterKeys[keyId - 1];
}