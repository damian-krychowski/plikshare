using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption;

public static class StorageFullEncryptionService
{
    private const int Pbkdf2Iterations = 650_000;
    private const int SaltSize = 32;
    private const int KekSize = 32;
    private const int AuthKeySize = 32;
    private const int RecoverySeedSize = 32;
    private const int RsaKeySize = 2048;
    private const int AesGcmNonceSize = 12;
    private const int AesGcmTagSize = 16;

    private static readonly byte[] AuthKeyContext = "plikshare-full-auth"u8.ToArray();

    public static GenerateDetailsResult GenerateDetails(string masterPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        Span<byte> kek = stackalloc byte[KekSize];
        Span<byte> recoveryBytes = stackalloc byte[RecoverySeedSize];
        byte[]? dek = null;

        try
        {
            DeriveKek(masterPassword, salt, kek);

            RandomNumberGenerator.Fill(recoveryBytes);

            // DEK_v0 is deterministically derived from the recovery seed so the
            // recovery code alone (without the database) is sufficient to
            // reconstruct the DEK for offline file recovery.
            dek = HkdfDekDerivation.DeriveDek(recoveryBytes, version: 0);

            var encryptedDek = EncryptAesGcm(kek, dek);

            var verifyHash = ComputeVerifyHash(kek);
            var recoveryVerifyHash = RecoveryVerifyHash.Compute(recoveryBytes);
            var recoveryCode = RecoveryCodeCodec.Encode(recoveryBytes);

            var details = new StorageFullEncryptionDetails(
                Salt: salt,
                VerifyHash: verifyHash,
                EncryptedDeks: [encryptedDek],
                RecoveryVerifyHash: recoveryVerifyHash);

            return new GenerateDetailsResult(details, recoveryCode);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(recoveryBytes);

            if (dek != null)
                CryptographicOperations.ZeroMemory(dek);
        }
    }

    public record GenerateDetailsResult(
        StorageFullEncryptionDetails Details,
        string RecoveryCode);

    public static ResetPasswordResult TryResetMasterPasswordWithRecoveryCode(
        string recoveryCode,
        string newPassword,
        StorageFullEncryptionDetails currentDetails)
    {
        var decode = RecoveryCodeCodec.TryDecode(recoveryCode, out var recoveryBytes);

        try
        {
            switch (decode)
            {
                case RecoveryCodeCodec.DecodeResult.Ok:
                    break;
                case RecoveryCodeCodec.DecodeResult.WrongWordCount:
                case RecoveryCodeCodec.DecodeResult.UnknownWord:
                    return new ResetPasswordResult(ResetPasswordResultCode.MalformedRecoveryCode);
                case RecoveryCodeCodec.DecodeResult.InvalidChecksum:
                    return new ResetPasswordResult(ResetPasswordResultCode.MalformedRecoveryCode);
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!RecoveryVerifyHash.Verify(recoveryBytes, currentDetails.RecoveryVerifyHash))
                return new ResetPasswordResult(ResetPasswordResultCode.InvalidRecoveryCode);

            var newSalt = RandomNumberGenerator.GetBytes(SaltSize);

            Span<byte> newKek = stackalloc byte[KekSize];
            byte[]? dek = null;

            try
            {
                DeriveKek(newPassword, newSalt, newKek);

                // DEK_v0 is deterministically derived from the recovery seed.
                // We re-wrap it with the new password KEK, matching the invariant
                // maintained by GenerateDetails (recovery path == password path).
                dek = HkdfDekDerivation.DeriveDek(recoveryBytes, version: 0);

                var newEncryptedDek = EncryptAesGcm(newKek, dek);
                var newVerifyHash = ComputeVerifyHash(newKek);

                var newDetails = new StorageFullEncryptionDetails(
                    Salt: newSalt,
                    VerifyHash: newVerifyHash,
                    EncryptedDeks: [newEncryptedDek],

                    RecoveryVerifyHash: currentDetails.RecoveryVerifyHash
                );

                return new ResetPasswordResult(
                    Code: ResetPasswordResultCode.Ok,
                    NewDetails: newDetails);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(newKek);
                if (dek != null)
                    CryptographicOperations.ZeroMemory(dek);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryBytes);
        }
    }

    public enum ResetPasswordResultCode
    {
        Ok,
        MalformedRecoveryCode,
        InvalidRecoveryCode
    }

    public record ResetPasswordResult(
        ResetPasswordResultCode Code,
        StorageFullEncryptionDetails? NewDetails = null);

    public static ChangePasswordResult TryChangeMasterPassword(
        string oldPassword,
        string newPassword,
        StorageFullEncryptionDetails currentDetails,
        IDataProtectionProvider dataProtectionProvider,
        string dataProtectionPurpose)
    {
        Span<byte> oldKek = stackalloc byte[KekSize];
        Span<byte> newKek = stackalloc byte[KekSize];
        byte[]? dek = null;

        try
        {
            DeriveKek(oldPassword, currentDetails.Salt, oldKek);

            if (!CryptographicOperations.FixedTimeEquals(
                    ComputeVerifyHash(oldKek),
                    currentDetails.VerifyHash))
            {
                return new ChangePasswordResult(ChangePasswordResultCode.InvalidOldPassword);
            }

            dek = DecryptAesGcm(oldKek, currentDetails.EncryptedDeks[0]);

            var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
            DeriveKek(newPassword, newSalt, newKek);

            var newEncryptedDek = EncryptAesGcm(newKek, dek);
            var newVerifyHash = ComputeVerifyHash(newKek);

            var newDetails = new StorageFullEncryptionDetails(
                Salt: newSalt,
                VerifyHash: newVerifyHash,
                EncryptedDeks: [newEncryptedDek],
                RecoveryVerifyHash: currentDetails.RecoveryVerifyHash);

            var protector = dataProtectionProvider.CreateProtector(dataProtectionPurpose);
            var protectedKek = protector.Protect(newKek.ToArray());

            return new ChangePasswordResult(
                Code: ChangePasswordResultCode.Ok,
                NewDetails: newDetails,
                ProtectedKek: Convert.ToBase64String(protectedKek));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(oldKek);
            CryptographicOperations.ZeroMemory(newKek);
            if (dek != null)
                CryptographicOperations.ZeroMemory(dek);
        }
    }

    public enum ChangePasswordResultCode
    {
        Ok,
        InvalidOldPassword
    }

    public record ChangePasswordResult(
        ChangePasswordResultCode Code,
        StorageFullEncryptionDetails? NewDetails = null,
        string? ProtectedKek = null);

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

    private static bool TryDeriveKek(
        string masterPassword,
        StorageFullEncryptionDetails details,
        Span<byte> kekOutput)
    {
        if (kekOutput.Length != KekSize)
            throw new ArgumentException(
                $"KEK output buffer must be {KekSize} bytes.", nameof(kekOutput));

        DeriveKek(
            masterPassword, 
            details.Salt, 
            kekOutput);

        var computedHash = ComputeVerifyHash(
            kekOutput);

        return CryptographicOperations.FixedTimeEquals(
            computedHash, 
            details.VerifyHash);
    }

    public static string? TryUnlockProtectedKek(
        string masterPassword,
        StorageFullEncryptionDetails details,
        IDataProtectionProvider dataProtectionProvider,
        string dataProtectionPurpose)
    {
        var kek = new byte[KekSize];

        try
        {
            if (!TryDeriveKek(masterPassword, details, kek))
                return null;

            var protector = dataProtectionProvider.CreateProtector(
                dataProtectionPurpose);

            var protectedKek = protector.Protect(kek);

            return Convert.ToBase64String(protectedKek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }
}