using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class EncryptionPasswordKdfTests
{
    // Lower-cost params for tests to keep the suite fast.
    private static readonly EncryptionPasswordKdf.Params TestParams = new(
        TimeCost: 1,
        MemoryCostKb: 1024,
        Parallelism: 1);

    [Fact]
    public void GenerateSalt_ReturnsExpectedSize()
    {
        var salt = EncryptionPasswordKdf.GenerateSalt();
        Assert.Equal(EncryptionPasswordKdf.SaltSize, salt.Length);
    }

    [Fact]
    public void GenerateSalt_ProducesDifferentSaltsEachCall()
    {
        var a = EncryptionPasswordKdf.GenerateSalt();
        var b = EncryptionPasswordKdf.GenerateSalt();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DeriveKek_ReturnsExpectedSize()
    {
        var salt = EncryptionPasswordKdf.GenerateSalt();
        var kek = EncryptionPasswordKdf.DeriveKek("hunter2", salt, TestParams);
        Assert.Equal(EncryptionPasswordKdf.KekSize, kek.Length);
    }

    [Fact]
    public void DeriveKek_IsDeterministic_SameInputsProduceSameKek()
    {
        var salt = EncryptionPasswordKdf.GenerateSalt();

        var kek1 = EncryptionPasswordKdf.DeriveKek("hunter2", salt, TestParams);
        var kek2 = EncryptionPasswordKdf.DeriveKek("hunter2", salt, TestParams);

        Assert.Equal(kek1, kek2);
    }

    [Fact]
    public void DeriveKek_DifferentPasswords_ProduceDifferentKeks()
    {
        var salt = EncryptionPasswordKdf.GenerateSalt();

        var kek1 = EncryptionPasswordKdf.DeriveKek("hunter2", salt, TestParams);
        var kek2 = EncryptionPasswordKdf.DeriveKek("hunter3", salt, TestParams);

        Assert.NotEqual(kek1, kek2);
    }

    [Fact]
    public void DeriveKek_DifferentSalts_ProduceDifferentKeks()
    {
        var saltA = EncryptionPasswordKdf.GenerateSalt();
        var saltB = EncryptionPasswordKdf.GenerateSalt();

        var kekA = EncryptionPasswordKdf.DeriveKek("hunter2", saltA, TestParams);
        var kekB = EncryptionPasswordKdf.DeriveKek("hunter2", saltB, TestParams);

        Assert.NotEqual(kekA, kekB);
    }

    [Fact]
    public void DeriveKek_DifferentParams_ProduceDifferentKeks()
    {
        var salt = EncryptionPasswordKdf.GenerateSalt();
        var paramsA = new EncryptionPasswordKdf.Params(TimeCost: 1, MemoryCostKb: 1024, Parallelism: 1);
        var paramsB = new EncryptionPasswordKdf.Params(TimeCost: 2, MemoryCostKb: 1024, Parallelism: 1);

        var kekA = EncryptionPasswordKdf.DeriveKek("hunter2", salt, paramsA);
        var kekB = EncryptionPasswordKdf.DeriveKek("hunter2", salt, paramsB);

        Assert.NotEqual(kekA, kekB);
    }

    [Fact]
    public void DeriveKek_WithWrongSaltSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            EncryptionPasswordKdf.DeriveKek("hunter2", new byte[16], TestParams));

        Assert.Throws<ArgumentException>(() =>
            EncryptionPasswordKdf.DeriveKek("hunter2", new byte[64], TestParams));
    }

    [Fact]
    public void ComputeVerifyHash_IsDeterministic()
    {
        var kek = new byte[32];
        Array.Fill(kek, (byte)0x42);

        var h1 = EncryptionPasswordKdf.ComputeVerifyHash(kek);
        var h2 = EncryptionPasswordKdf.ComputeVerifyHash(kek);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeVerifyHash_DifferentKeks_ProduceDifferentHashes()
    {
        var kekA = new byte[32];
        Array.Fill(kekA, (byte)0x42);
        var kekB = new byte[32];
        Array.Fill(kekB, (byte)0x43);

        var hA = EncryptionPasswordKdf.ComputeVerifyHash(kekA);
        var hB = EncryptionPasswordKdf.ComputeVerifyHash(kekB);

        Assert.NotEqual(hA, hB);
    }

    [Fact]
    public void Verify_WithMatchingKek_ReturnsTrue()
    {
        var kek = new byte[32];
        Array.Fill(kek, (byte)0x42);

        var hash = EncryptionPasswordKdf.ComputeVerifyHash(kek);

        Assert.True(EncryptionPasswordKdf.Verify(kek, hash));
    }

    [Fact]
    public void Verify_WithDifferentKek_ReturnsFalse()
    {
        var kekA = new byte[32];
        Array.Fill(kekA, (byte)0x42);
        var kekB = new byte[32];
        Array.Fill(kekB, (byte)0x43);

        var hashA = EncryptionPasswordKdf.ComputeVerifyHash(kekA);

        Assert.False(EncryptionPasswordKdf.Verify(kekB, hashA));
    }

    [Fact]
    public void Params_SerializeDeserialize_Roundtrip()
    {
        var original = new EncryptionPasswordKdf.Params(
            TimeCost: 3,
            MemoryCostKb: 65536,
            Parallelism: 1);

        var serialized = EncryptionPasswordKdf.SerializeParams(original);
        var deserialized = EncryptionPasswordKdf.DeserializeParams(serialized);

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Params_Default_HasExpectedStrength()
    {
        // Sanity check — defaults should not regress below production-safe levels.
        var defaults = EncryptionPasswordKdf.Params.Default;

        Assert.True(defaults.TimeCost >= 2, "TimeCost should be at least 2");
        Assert.True(defaults.MemoryCostKb >= 19 * 1024, "MemoryCostKb should be at least 19 MiB (OWASP min)");
        Assert.True(defaults.Parallelism >= 1);
    }
}
