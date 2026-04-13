using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;

namespace PlikShare.Tests;

public class StorageFullEncryptionServiceTests
{
    [Fact]
    public void GenerateDetails_ReturnsValidRecoveryCode()
    {
        var result = StorageFullEncryptionService.GenerateDetails("correct horse battery staple");

        var decode = RecoveryCodeCodec.TryDecode(result.RecoveryCode, out var recoveryBytes);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, decode);
        Assert.Equal(32, recoveryBytes.Length);
    }

    [Fact]
    public void GenerateDetails_RecoveryVerifyHash_MatchesTheReturnedCode()
    {
        var result = StorageFullEncryptionService.GenerateDetails("correct horse battery staple");

        RecoveryCodeCodec.TryDecode(result.RecoveryCode, out var recoveryBytes);

        Assert.True(RecoveryVerifyHash.Verify(recoveryBytes, result.Details.RecoveryVerifyHash));
    }

    [Fact]
    public void GenerateDetails_DekFromRecoveryPath_EqualsDekFromPasswordPath()
    {
        // The core recovery invariant: unwrapping EncryptedDeks[0] with the
        // password-derived KEK must produce the exact same DEK that HKDF
        // derives from the recovery seed. If this fails, password-unlocked
        // files and offline-recovered files would not decrypt identically.
        const string password = "correct horse battery staple";

        var result = StorageFullEncryptionService.GenerateDetails(password);

        RecoveryCodeCodec.TryDecode(result.RecoveryCode, out var recoveryBytes);
        var dekFromRecoverySeed = HkdfDekDerivation.DeriveDek(recoveryBytes, version: 0);

        var passwordKek = new byte[32];
        Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: result.Details.Salt,
            destination: passwordKek,
            iterations: 650_000,
            hashAlgorithm: HashAlgorithmName.SHA256);

        var dekFromPasswordWrap = UnwrapAesGcm(passwordKek, result.Details.EncryptedDeks[0]);

        Assert.Equal(dekFromRecoverySeed, dekFromPasswordWrap);
    }

    [Fact]
    public void GenerateDetails_TwoStoragesWithSamePassword_HaveDifferentRecoveryCodes()
    {
        var a = StorageFullEncryptionService.GenerateDetails("same password");
        var b = StorageFullEncryptionService.GenerateDetails("same password");

        Assert.NotEqual(a.RecoveryCode, b.RecoveryCode);
        Assert.NotEqual(a.Details.RecoveryVerifyHash, b.Details.RecoveryVerifyHash);
        Assert.NotEqual(a.Details.Salt, b.Details.Salt);
    }

    [Fact]
    public void GenerateDetails_ExistingFieldsStillPopulated()
    {
        // Regression guard: Phase 2 must not regress the pre-existing fields
        // (Salt, VerifyHash, PublicKey, EncryptedPrivateKey, EncryptedDeks[0]).
        var result = StorageFullEncryptionService.GenerateDetails("pw");

        Assert.Equal(32, result.Details.Salt.Length);
        Assert.Equal(32, result.Details.VerifyHash.Length);
        Assert.Single(result.Details.EncryptedDeks);
    }

    [Fact]
    public void TryUnlockProtectedKek_WithCorrectPassword_ReturnsProtectedKek()
    {
        const string password = "pw";
        var result = StorageFullEncryptionService.GenerateDetails(password);

        var dpp = new EphemeralDataProtectionProvider();

        var protectedKek = StorageFullEncryptionService.TryUnlockProtectedKek(
            masterPassword: password,
            details: result.Details,
            dataProtectionProvider: dpp,
            dataProtectionPurpose: "test");

        Assert.NotNull(protectedKek);
    }

    [Fact]
    public void TryUnlockProtectedKek_WithWrongPassword_ReturnsNull()
    {
        var result = StorageFullEncryptionService.GenerateDetails("correct");

        var dpp = new EphemeralDataProtectionProvider();

        var protectedKek = StorageFullEncryptionService.TryUnlockProtectedKek(
            masterPassword: "wrong",
            details: result.Details,
            dataProtectionProvider: dpp,
            dataProtectionPurpose: "test");

        Assert.Null(protectedKek);
    }

    private static byte[] UnwrapAesGcm(ReadOnlySpan<byte> key, ReadOnlySpan<byte> wrapped)
    {
        const int nonceSize = 12;
        const int tagSize = 16;

        var nonce = wrapped.Slice(0, nonceSize);
        var tag = wrapped.Slice(nonceSize, tagSize);
        var ciphertext = wrapped.Slice(nonceSize + tagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
