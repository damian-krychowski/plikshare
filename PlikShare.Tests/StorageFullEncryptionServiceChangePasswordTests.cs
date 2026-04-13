using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;

namespace PlikShare.Tests;

public class StorageFullEncryptionServiceChangePasswordTests
{
    [Fact]
    public void TryChange_WithCorrectOldPassword_ReturnsOkWithProtectedKek()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: "old",
            newPassword: "new",
            currentDetails: original.Details,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            dataProtectionPurpose: "test");

        Assert.Equal(StorageFullEncryptionService.ChangePasswordResultCode.Ok, result.Code);
        Assert.NotNull(result.NewDetails);
        Assert.NotNull(result.ProtectedKek);
    }

    [Fact]
    public void TryChange_WithWrongOldPassword_ReturnsInvalidOldPassword()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: "wrong",
            newPassword: "new",
            currentDetails: original.Details,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            dataProtectionPurpose: "test");

        Assert.Equal(StorageFullEncryptionService.ChangePasswordResultCode.InvalidOldPassword, result.Code);
        Assert.Null(result.NewDetails);
        Assert.Null(result.ProtectedKek);
    }

    [Fact]
    public void TryChange_PreservesRecoveryVerifyHash_SoRecoveryCodeStillWorks()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: "old",
            newPassword: "new",
            currentDetails: original.Details,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            dataProtectionPurpose: "test");

        Assert.Equal(original.Details.RecoveryVerifyHash, result.NewDetails!.RecoveryVerifyHash);
    }

    [Fact]
    public void TryChange_NewPasswordUnlocksSameDekAsRecoveryHkdf()
    {
        // Critical invariant: after change-password, the DEK unwrapped with the
        // new password must still equal HKDF(recoveryBytes, v0). Otherwise the
        // recovery path would give a different key than the password path.
        const string newPassword = "new-password";
        var original = StorageFullEncryptionService.GenerateDetails("old");
        RecoveryCodeCodec.TryDecode(original.RecoveryCode, out var recoveryBytes);
        var expectedDek = HkdfDekDerivation.DeriveDek(recoveryBytes, version: 0);

        var result = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: "old",
            newPassword: newPassword,
            currentDetails: original.Details,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            dataProtectionPurpose: "test");

        var newKek = new byte[32];
        Rfc2898DeriveBytes.Pbkdf2(
            password: newPassword,
            salt: result.NewDetails!.Salt,
            destination: newKek,
            iterations: 650_000,
            hashAlgorithm: HashAlgorithmName.SHA256);

        var dekFromNewWrap = UnwrapAesGcm(newKek, result.NewDetails.EncryptedDeks[0]);

        Assert.Equal(expectedDek, dekFromNewWrap);
    }

    [Fact]
    public void TryChange_OldPasswordNoLongerUnlocksAfterChange()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: "old",
            newPassword: "new",
            currentDetails: original.Details,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            dataProtectionPurpose: "test");

        var oldKek = new byte[32];
        Rfc2898DeriveBytes.Pbkdf2(
            password: "old",
            salt: result.NewDetails!.Salt,
            destination: oldKek,
            iterations: 650_000,
            hashAlgorithm: HashAlgorithmName.SHA256);

        Assert.ThrowsAny<CryptographicException>(
            () => UnwrapAesGcm(oldKek, result.NewDetails.EncryptedDeks[0]));
    }

    [Fact]
    public void TryChange_ThenReset_WithSameRecoveryCode_Works()
    {
        // End-to-end invariant: change-password, then reset via the original
        // recovery code — user should still be able to recover.
        var original = StorageFullEncryptionService.GenerateDetails("first");

        var afterChange = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: "first",
            newPassword: "second",
            currentDetails: original.Details,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            dataProtectionPurpose: "test");

        var afterReset = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: original.RecoveryCode,
            newPassword: "third",
            currentDetails: afterChange.NewDetails!);

        Assert.Equal(StorageFullEncryptionService.ResetPasswordResultCode.Ok, afterReset.Code);
    }

    [Fact]
    public void TryChange_ProtectedKekIsValidBase64()
    {
        var original = StorageFullEncryptionService.GenerateDetails("old");

        var result = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: "old",
            newPassword: "new",
            currentDetails: original.Details,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            dataProtectionPurpose: "test");

        // Must round-trip through base64 decode without throwing.
        var bytes = Convert.FromBase64String(result.ProtectedKek!);
        Assert.NotEmpty(bytes);
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
