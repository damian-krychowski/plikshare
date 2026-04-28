using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class RecoveryVerifyHashTests
{
    [Fact]
    public void Compute_ReturnsExactly32Bytes()
    {
        var seed = new byte[32];
        RandomNumberGenerator.Fill(seed);

        var hash = RecoveryVerifyHash.Compute(seed);

        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        var seed = new byte[32];
        RandomNumberGenerator.Fill(seed);

        var h1 = RecoveryVerifyHash.Compute(seed);
        var h2 = RecoveryVerifyHash.Compute(seed);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Compute_DifferentSeeds_ProduceDifferentHashes()
    {
        var seedA = new byte[32];
        var seedB = new byte[32];
        RandomNumberGenerator.Fill(seedA);
        RandomNumberGenerator.Fill(seedB);

        var hashA = RecoveryVerifyHash.Compute(seedA);
        var hashB = RecoveryVerifyHash.Compute(seedB);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void Verify_CorrectSeed_ReturnsTrue()
    {
        var seed = new byte[32];
        RandomNumberGenerator.Fill(seed);
        var expected = RecoveryVerifyHash.Compute(seed);

        Assert.True(RecoveryVerifyHash.Verify(seed, expected));
    }

    [Fact]
    public void Verify_WrongSeed_ReturnsFalse()
    {
        var seedA = new byte[32];
        var seedB = new byte[32];
        RandomNumberGenerator.Fill(seedA);
        RandomNumberGenerator.Fill(seedB);

        var expected = RecoveryVerifyHash.Compute(seedA);

        Assert.False(RecoveryVerifyHash.Verify(seedB, expected));
    }

    [Fact]
    public void Verify_WrongHashSize_ReturnsFalse()
    {
        var seed = new byte[32];
        RandomNumberGenerator.Fill(seed);

        Assert.False(RecoveryVerifyHash.Verify(seed, new byte[16]));
        Assert.False(RecoveryVerifyHash.Verify(seed, new byte[33]));
    }

    [Fact]
    public void Compute_DomainSeparated_DifferentFromDekDerivation()
    {
        // RecoveryVerifyHash and HkdfDekDerivation both use the same seed as IKM.
        // Domain separation via info tags must ensure that the verify hash never
        // coincides with a derived DEK, even though they start from the same seed.
        var seed = new byte[32];
        RandomNumberGenerator.Fill(seed);

        var verifyHash = RecoveryVerifyHash.Compute(seed);
        using var dekV0 = StorageDekDerivation.DeriveDek(seed, 0);

        var dekV0Bytes = new byte[dekV0.Length];
        dekV0.CopyTo(dekV0Bytes);

        Assert.NotEqual(verifyHash, dekV0Bytes);
    }
}
