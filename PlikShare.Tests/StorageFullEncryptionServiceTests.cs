using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;

namespace PlikShare.Tests;

public class StorageFullEncryptionServiceTests
{
    [Fact]
    public void GenerateDetails_ReturnsValidRecoveryCode()
    {
        var result = StorageFullEncryptionService.GenerateDetails();

        var decode = RecoveryCodeCodec.TryDecode(result.RecoveryCode, out var recoveryBytes);

        Assert.Equal(RecoveryCodeCodec.DecodeResult.Ok, decode);
        Assert.Equal(32, recoveryBytes.Length);
    }

    [Fact]
    public void GenerateDetails_RecoveryVerifyHash_MatchesTheReturnedCode()
    {
        var result = StorageFullEncryptionService.GenerateDetails();

        RecoveryCodeCodec.TryDecode(result.RecoveryCode, out var recoveryBytes);

        Assert.True(RecoveryVerifyHash.Verify(recoveryBytes, result.Details.RecoveryVerifyHash));
    }

    [Fact]
    public void GenerateDetails_ReturnedDek_IsDerivedFromRecoverySeed()
    {
        // The core recovery invariant: the DEK handed to the caller must equal the DEK
        // that HKDF derives from the recovery seed. If this breaks, files encrypted by
        // the freshly wrapped DEK would not decrypt under an offline recovery-code flow.
        var result = StorageFullEncryptionService.GenerateDetails();

        RecoveryCodeCodec.TryDecode(result.RecoveryCode, out var recoveryBytes);
        var dekFromRecoverySeed = HkdfDekDerivation.DeriveDek(recoveryBytes, version: 0);

        Assert.Equal(dekFromRecoverySeed, result.Dek);
    }

    [Fact]
    public void GenerateDetails_Dek_IsThirtyTwoBytes()
    {
        var result = StorageFullEncryptionService.GenerateDetails();
        Assert.Equal(32, result.Dek.Length);
    }

    [Fact]
    public void GenerateDetails_TwoCalls_ProduceIndependentRecoveryMaterial()
    {
        var a = StorageFullEncryptionService.GenerateDetails();
        var b = StorageFullEncryptionService.GenerateDetails();

        Assert.NotEqual(a.RecoveryCode, b.RecoveryCode);
        Assert.NotEqual(a.Details.RecoveryVerifyHash, b.Details.RecoveryVerifyHash);
        Assert.NotEqual(a.Dek, b.Dek);
    }
}
