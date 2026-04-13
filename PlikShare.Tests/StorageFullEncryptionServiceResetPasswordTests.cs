using System.Security.Cryptography;
using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;

namespace PlikShare.Tests;

public class StorageFullEncryptionServiceResetPasswordTests
{
    [Fact]
    public void TryReset_WithCorrectCodeAndNewPassword_ReturnsOk()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: original.RecoveryCode,
            newPassword: "new",
            currentDetails: original.Details);

        Assert.Equal(StorageFullEncryptionService.ResetPasswordResultCode.Ok, result.Code);
        Assert.NotNull(result.NewDetails);
    }

    [Fact]
    public void TryReset_PreservesRecoveryVerifyHash_SoSameCodeStillWorks()
    {
        // Reset must NOT touch RecoveryVerifyHash — the recovery code is
        // permanent per our design (D2). Regenerating it would invalidate
        // the user's saved code.
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: original.RecoveryCode,
            newPassword: "new",
            currentDetails: original.Details);

        Assert.Equal(original.Details.RecoveryVerifyHash, result.NewDetails!.RecoveryVerifyHash);
    }

    [Fact]
    public void TryReset_GeneratesNewSaltAndVerifyHash()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: original.RecoveryCode,
            newPassword: "new",
            currentDetails: original.Details);

        Assert.NotEqual(original.Details.Salt, result.NewDetails!.Salt);
        Assert.NotEqual(original.Details.VerifyHash, result.NewDetails.VerifyHash);
    }

    [Fact]
    public void TryReset_NewPasswordUnlocksNewDek_EqualToHkdfDerivedFromSeed()
    {
        // The critical invariant after reset: unwrapping EncryptedDeks[0] with
        // the NEW password must produce the same DEK that HKDF derives from
        // the recovery seed. Otherwise files written before reset can't be
        // read after reset.
        const string newPassword = "new-password";
        var original = StorageFullEncryptionService.GenerateDetails("old");
        RecoveryCodeCodec.TryDecode(original.RecoveryCode, out var recoveryBytes);
        var expectedDek = HkdfDekDerivation.DeriveDek(recoveryBytes, version: 0);

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: original.RecoveryCode,
            newPassword: newPassword,
            currentDetails: original.Details);

        var newKek = new byte[32];
        Rfc2898DeriveBytes.Pbkdf2(
            password: newPassword,
            salt: result.NewDetails!.Salt,
            destination: newKek,
            iterations: 650_000,
            hashAlgorithm: HashAlgorithmName.SHA256);

        var dekFromNewPasswordWrap = UnwrapAesGcm(newKek, result.NewDetails.EncryptedDeks[0]);

        Assert.Equal(expectedDek, dekFromNewPasswordWrap);
    }

    [Fact]
    public void TryReset_OldPasswordNoLongerUnlocks()
    {
        const string oldPassword = "old";
        var original = StorageFullEncryptionService.GenerateDetails(oldPassword);

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: original.RecoveryCode,
            newPassword: "new",
            currentDetails: original.Details);

        var oldKek = new byte[32];
        Rfc2898DeriveBytes.Pbkdf2(
            password: oldPassword,
            salt: result.NewDetails!.Salt,    // new salt
            destination: oldKek,
            iterations: 650_000,
            hashAlgorithm: HashAlgorithmName.SHA256);

        // AES-GCM decryption with wrong key throws AuthenticationTagMismatchException,
        // which derives from CryptographicException. Use ThrowsAny to accept either.
        Assert.ThrowsAny<CryptographicException>(
            () => UnwrapAesGcm(oldKek, result.NewDetails.EncryptedDeks[0]));
    }

    [Fact]
    public void TryReset_WithWrongWordCount_ReturnsMalformed()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: "abandon abandon abandon",
            newPassword: "new",
            currentDetails: original.Details);

        Assert.Equal(
            StorageFullEncryptionService.ResetPasswordResultCode.MalformedRecoveryCode,
            result.Code);
    }

    [Fact]
    public void TryReset_WithUnknownWord_ReturnsMalformed()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");
        var bad = string.Join(' ', Enumerable.Repeat("abandon", 22).Append("notaword").Append("art"));

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: bad,
            newPassword: "new",
            currentDetails: original.Details);

        Assert.Equal(
            StorageFullEncryptionService.ResetPasswordResultCode.MalformedRecoveryCode,
            result.Code);
    }

    [Fact]
    public void TryReset_WithBadChecksum_ReturnsMalformed()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");
        // 23x abandon + "ability" has valid vocab but wrong checksum for all-zero entropy.
        var bad = string.Join(' ', Enumerable.Repeat("abandon", 23).Append("ability"));

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: bad,
            newPassword: "new",
            currentDetails: original.Details);

        Assert.Equal(
            StorageFullEncryptionService.ResetPasswordResultCode.MalformedRecoveryCode,
            result.Code);
    }

    [Fact]
    public void TryReset_WithValidCodeFromDifferentStorage_ReturnsInvalid()
    {
        // User pastes a valid BIP39 code — but it's the recovery code for
        // a different storage. Must not leak any info, must not accept.
        var storageA = StorageFullEncryptionService.GenerateDetails("a");
        var storageB = StorageFullEncryptionService.GenerateDetails("b");

        var result = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: storageB.RecoveryCode,
            newPassword: "new",
            currentDetails: storageA.Details);

        Assert.Equal(
            StorageFullEncryptionService.ResetPasswordResultCode.InvalidRecoveryCode,
            result.Code);
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
