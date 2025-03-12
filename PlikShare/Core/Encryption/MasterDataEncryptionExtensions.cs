using PlikShare.Core.Utils;

namespace PlikShare.Core.Encryption;

public static class MasterDataEncryptionExtensions
{
    public static byte[] EncryptJson<T>(this IMasterDataEncryption encryption, T item)
    {
        var plainText = Json.Serialize(
            item);

        return encryption.Encrypt(
            plainText);
    }

    public static string EncryptToBase64(this IMasterDataEncryption encryption, string plainText)
    {
        var bytes = encryption.Encrypt(
            plainText);

        return Convert.ToBase64String(
            bytes);
    }

    public static T DecryptJson<T>(this IMasterDataEncryption encryption, byte[] versionedEncryptedBytes)
    {
        var plainText = encryption.Decrypt(
            versionedEncryptedBytes);

        var item = Json.Deserialize<T>(
            plainText);

        if (item is null)
            throw new InvalidOperationException(
                $"Decryption and deserialization of object into {typeof(T)} failed.");

        return item;
    }
    
    public static string DecryptFromBase64(this IMasterDataEncryption encryption, string base64EncryptedText)
    {
        var encryptedBytes = Convert.FromBase64String(
            base64EncryptedText);

        return encryption.Decrypt(
            encryptedBytes);
    }
    
    public static string? DecryptIfNotNull(this IMasterDataEncryption encryption, byte[]? versionedEncryptedBytes)
    {
        return versionedEncryptedBytes is null
            ? null
            : encryption.Decrypt(versionedEncryptedBytes);
    }

    public static byte[] EncryptJson<T>(this IDerivedMasterDataEncryption encryption, T item)
    {
        var plainText = Json.Serialize(
            item);

        return encryption.Encrypt(
            plainText);
    }

    public static string EncryptToBase64(this IDerivedMasterDataEncryption encryption, string plainText)
    {
        var bytes = encryption.Encrypt(
            plainText);

        return Convert.ToBase64String(
            bytes);
    }

    public static T DecryptJson<T>(this IDerivedMasterDataEncryption encryption, byte[] versionedEncryptedBytes)
    {
        var plainText = encryption.Decrypt(
            versionedEncryptedBytes);

        var item = Json.Deserialize<T>(
            plainText);

        if (item is null)
            throw new InvalidOperationException(
                $"Decryption and deserialization of object into {typeof(T)} failed.");

        return item;
    }

    public static string DecryptFromBase64(this IDerivedMasterDataEncryption encryption, string base64EncryptedText)
    {
        var encryptedBytes = Convert.FromBase64String(
            base64EncryptedText);

        return encryption.Decrypt(
            encryptedBytes);
    }

    public static string? DecryptIfNotNull(this IDerivedMasterDataEncryption encryption, byte[]? versionedEncryptedBytes)
    {
        return versionedEncryptedBytes is null
            ? null
            : encryption.Decrypt(versionedEncryptedBytes);
    }
}