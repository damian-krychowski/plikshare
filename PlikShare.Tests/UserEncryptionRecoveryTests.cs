using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class UserEncryptionRecoveryTests
{
    [Fact]
    public void GenerateRecoverySeed_ReturnsExpectedSize()
    {
        var seed = UserEncryptionRecovery.GenerateRecoverySeed();
        Assert.Equal(UserEncryptionRecovery.RecoverySeedSize, seed.Length);
    }

    [Fact]
    public void GenerateRecoverySeed_ProducesDifferentSeedsEachCall()
    {
        var a = UserEncryptionRecovery.GenerateRecoverySeed();
        var b = UserEncryptionRecovery.GenerateRecoverySeed();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DeriveRecoveryKek_ReturnsExpectedSize()
    {
        var seed = UserEncryptionRecovery.GenerateRecoverySeed();
        var kek = UserEncryptionRecovery.DeriveRecoveryKek(seed);
        Assert.Equal(UserEncryptionRecovery.RecoveryKekSize, kek.Length);
    }

    [Fact]
    public void DeriveRecoveryKek_IsDeterministic()
    {
        var seed = UserEncryptionRecovery.GenerateRecoverySeed();

        var kek1 = UserEncryptionRecovery.DeriveRecoveryKek(seed);
        var kek2 = UserEncryptionRecovery.DeriveRecoveryKek(seed);

        Assert.Equal(kek1, kek2);
    }

    [Fact]
    public void DeriveRecoveryKek_DifferentSeeds_ProduceDifferentKeks()
    {
        var seedA = UserEncryptionRecovery.GenerateRecoverySeed();
        var seedB = UserEncryptionRecovery.GenerateRecoverySeed();

        var kekA = UserEncryptionRecovery.DeriveRecoveryKek(seedA);
        var kekB = UserEncryptionRecovery.DeriveRecoveryKek(seedB);

        Assert.NotEqual(kekA, kekB);
    }

    [Fact]
    public void DeriveRecoveryKek_WithWrongSeedSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            UserEncryptionRecovery.DeriveRecoveryKek(new byte[16]));

        Assert.Throws<ArgumentException>(() =>
            UserEncryptionRecovery.DeriveRecoveryKek(new byte[64]));
    }

    [Fact]
    public void ComputeVerifyHash_IsDeterministic()
    {
        var seed = UserEncryptionRecovery.GenerateRecoverySeed();

        var h1 = UserEncryptionRecovery.ComputeVerifyHash(seed);
        var h2 = UserEncryptionRecovery.ComputeVerifyHash(seed);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Verify_MatchingSeed_ReturnsTrue()
    {
        var seed = UserEncryptionRecovery.GenerateRecoverySeed();
        var hash = UserEncryptionRecovery.ComputeVerifyHash(seed);

        Assert.True(UserEncryptionRecovery.Verify(seed, hash));
    }

    [Fact]
    public void Verify_DifferentSeed_ReturnsFalse()
    {
        var seedA = UserEncryptionRecovery.GenerateRecoverySeed();
        var seedB = UserEncryptionRecovery.GenerateRecoverySeed();
        var hashA = UserEncryptionRecovery.ComputeVerifyHash(seedA);

        Assert.False(UserEncryptionRecovery.Verify(seedB, hashA));
    }

    [Fact]
    public void UserRecoveryKek_AndStorageRecoveryKek_AreIndependent()
    {
        // Domain separation: same seed bytes must produce different KEKs
        // across user-level and storage-level recovery. We reuse the HkdfDekDerivation
        // for storage recovery, which uses info "plikshare-dek\0". User recovery uses
        // "plikshare-user-encryption-recovery-kek\0". Different info → different output.
        var seed = UserEncryptionRecovery.GenerateRecoverySeed();

        var userKek = UserEncryptionRecovery.DeriveRecoveryKek(seed);
        var storageDek = HkdfDekDerivation.DeriveDek(seed, version: 0);

        Assert.NotEqual(userKek, storageDek);
    }

    [Fact]
    public void UserRecoveryVerifyHash_AndStorageRecoveryVerifyHash_AreIndependent()
    {
        // Same domain-separation check for verify hashes.
        var seed = UserEncryptionRecovery.GenerateRecoverySeed();

        var userHash = UserEncryptionRecovery.ComputeVerifyHash(seed);
        var storageHash = RecoveryVerifyHash.Compute(seed);

        Assert.NotEqual(userHash, storageHash);
    }

    [Fact]
    public void EndToEndScenario_WrapRecoverUnwrap()
    {
        // Simulates the full flow: generate keypair, wrap private key with recovery KEK,
        // later use recovery code to unwrap and set a new password.
        var keypair = UserKeyPair.Generate();
        var recoverySeed = UserEncryptionRecovery.GenerateRecoverySeed();

        // Setup: wrap private key with recovery KEK
        var recoveryKek = UserEncryptionRecovery.DeriveRecoveryKek(recoverySeed);
        var recoveryWrappedPrivateKey = WrappedPrivateKey.Wrap(recoveryKek, keypair.PrivateKey);
        var recoveryVerifyHash = UserEncryptionRecovery.ComputeVerifyHash(recoverySeed);

        // Some time later: user pastes recovery code → we decode → verify hash matches
        var pastedSeed = recoverySeed;  // in reality decoded from BIP-39 mnemonic
        Assert.True(UserEncryptionRecovery.Verify(pastedSeed, recoveryVerifyHash));

        // Unwrap private key
        var rederivedKek = UserEncryptionRecovery.DeriveRecoveryKek(pastedSeed);
        var recoveredPrivateKey = WrappedPrivateKey.Unwrap(rederivedKek, recoveryWrappedPrivateKey);

        Assert.Equal(keypair.PrivateKey, recoveredPrivateKey);
    }
}
