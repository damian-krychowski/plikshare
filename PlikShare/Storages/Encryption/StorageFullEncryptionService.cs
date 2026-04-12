using System.Security.Cryptography;

namespace PlikShare.Storages.Encryption;

public static class StorageFullEncryptionService
{
    private const int Pbkdf2Iterations = 650_000;
    private const int SaltSize = 32;
    private const int KekSize = 32;
    private const int AuthKeySize = 32;
    private const int DekSize = 32;
    private const int RsaKeySize = 2048;
    private const int AesGcmNonceSize = 12;
    private const int AesGcmTagSize = 16;

    private static readonly byte[] AuthKeyContext = "plikshare-full-auth"u8.ToArray();

    public static StorageFullEncryptionDetails GenerateDetails(string masterPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        Span<byte> kek = stackalloc byte[KekSize];
        Span<byte> dek = stackalloc byte[DekSize];
        byte[]? privateKey = null;

        try
        {
            DeriveKek(masterPassword, salt, kek);

            using var rsa = RSA.Create(RsaKeySize);
            var publicKey = rsa.ExportSubjectPublicKeyInfo();
            privateKey = rsa.ExportPkcs8PrivateKey();

            var encryptedPrivateKey = EncryptAesGcm(kek, privateKey);

            RandomNumberGenerator.Fill(dek);

            var encryptedDek = EncryptAesGcm(kek, dek);

            return new StorageFullEncryptionDetails(
                Salt: salt,
                VerifyHash: ComputeVerifyHash(kek),
                PublicKey: publicKey,
                EncryptedPrivateKey: encryptedPrivateKey,
                EncryptedDeks: [encryptedDek]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(dek);

            if (privateKey != null)
                CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    private static void DeriveKek(
        string masterPassword,
        ReadOnlySpan<byte> salt,
        Span<byte> kek)
    {
        Rfc2898DeriveBytes.Pbkdf2(
            password: masterPassword,
            salt: salt,
            destination: kek,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256);
    }

    private static byte[] ComputeVerifyHash(ReadOnlySpan<byte> kek)
    {
        Span<byte> authKey = stackalloc byte[AuthKeySize];

        try
        {
            DeriveAuthKey(kek, authKey);
            return SHA256.HashData(authKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(authKey);
        }
    }

    private static void DeriveAuthKey(
        ReadOnlySpan<byte> kek,
        Span<byte> authKey)
    {
        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: kek,
            output: authKey,
            salt: [],
            info: AuthKeyContext);
    }

    private static byte[] EncryptAesGcm(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext)
    {
        var result = new byte[AesGcmNonceSize + AesGcmTagSize + plaintext.Length];

        var nonce = result.AsSpan(0, AesGcmNonceSize);
        var tag = result.AsSpan(AesGcmNonceSize, AesGcmTagSize);
        var ciphertext = result.AsSpan(AesGcmNonceSize + AesGcmTagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, AesGcmTagSize);

        aes.Encrypt(
            nonce: nonce,
            plaintext: plaintext,
            ciphertext: ciphertext,
            tag: tag);

        return result;
    }

    private static byte[] DecryptAesGcm(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> encryptedData)
    {
        var nonce = encryptedData.Slice(0, AesGcmNonceSize);
        var tag = encryptedData.Slice(AesGcmNonceSize, AesGcmTagSize);
        var ciphertext = encryptedData.Slice(AesGcmNonceSize + AesGcmTagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, AesGcmTagSize);

        aes.Decrypt(
            nonce: nonce,
            ciphertext: ciphertext,
            tag: tag,
            plaintext: plaintext);

        return plaintext;
    }

    public static byte[] Decrypt(
        ReadOnlySpan<byte> kek,
        ReadOnlySpan<byte> encryptedData)
    {
        return DecryptAesGcm(kek, encryptedData);
    }
}