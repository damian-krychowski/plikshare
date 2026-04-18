using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class UserKeyPairTests
{
    [Fact]
    public void Generate_ReturnsKeysOfExpectedSize()
    {
        using var keypair = UserKeyPair.Generate();

        Assert.Equal(UserKeyPair.PublicKeySize, keypair.PublicKey.Length);
        Assert.Equal(UserKeyPair.PrivateKeySize, keypair.PrivateKey.Length);
    }

    [Fact]
    public void Generate_ProducesDifferentKeypairsEachTime()
    {
        using var a = UserKeyPair.Generate();
        using var b = UserKeyPair.Generate();

        Assert.NotEqual(a.PublicKey, b.PublicKey);
        AssertSecureBytesNotEqual(a.PrivateKey, b.PrivateKey);
    }

    [Fact]
    public void SealTo_OpenSealed_RoundTripRecoversPlaintext()
    {
        using var keypair = UserKeyPair.Generate();
        var plaintext = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);

        using var recovered = UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_);

        AssertSecureBytesEqual(plaintext, recovered);
    }

    [Fact]
    public void SealTo_OpenSealed_RoundTripForRandom32ByteDek()
    {
        using var keypair = UserKeyPair.Generate();
        var dek = new byte[32];
        RandomNumberGenerator.Fill(dek);

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, dek);

        using var recovered = UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_);

        AssertSecureBytesEqual(dek, recovered);
    }

    [Fact]
    public void SealTo_ProducesDifferentCiphertextsForSamePlaintext()
    {
        using var keypair = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        var a = UserKeyPair.SealTo(keypair.PublicKey, plaintext);
        var b = UserKeyPair.SealTo(keypair.PublicKey, plaintext);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void OpenSealed_WithWrongPrivateKey_Throws()
    {
        using var recipient = UserKeyPair.Generate();
        using var attacker = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3 };

        var sealed_ = UserKeyPair.SealTo(recipient.PublicKey, plaintext);

        Assert.Throws<InvalidOperationException>(() =>
            UserKeyPair.OpenSealed(attacker.PrivateKey, sealed_));
    }

    [Fact]
    public void OpenSealed_WithTamperedCiphertext_Throws()
    {
        using var keypair = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);
        sealed_[^5] ^= 0x01;

        Assert.Throws<InvalidOperationException>(() =>
            UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_));
    }

    [Fact]
    public void OpenSealed_WithTamperedEphemeralKey_Throws()
    {
        using var keypair = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3 };

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);
        sealed_[0] ^= 0x01;

        Assert.Throws<InvalidOperationException>(() =>
            UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_));
    }

    [Fact]
    public void OpenSealed_WithTruncatedPayload_Throws()
    {
        using var keypair = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3 };

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);
        var truncated = sealed_[..10];

        Assert.Throws<ArgumentException>(() =>
            UserKeyPair.OpenSealed(keypair.PrivateKey, truncated));
    }

    [Fact]
    public void SealTo_WithWrongSizePublicKey_Throws()
    {
        var plaintext = new byte[] { 1, 2, 3 };

        Assert.Throws<ArgumentException>(() =>
            UserKeyPair.SealTo(new byte[16], plaintext));

        Assert.Throws<ArgumentException>(() =>
            UserKeyPair.SealTo(new byte[64], plaintext));
    }

    [Fact]
    public void OpenSealed_WithWrongSizePrivateKey_Throws()
    {
        using var keypair = UserKeyPair.Generate();
        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, new byte[] { 1, 2, 3 });

        using var tooShort = SecureBytes.CopyFrom(new byte[16]);
        using var tooLong = SecureBytes.CopyFrom(new byte[64]);

        Assert.Throws<ArgumentException>(() =>
            UserKeyPair.OpenSealed(tooShort, sealed_));

        Assert.Throws<ArgumentException>(() =>
            UserKeyPair.OpenSealed(tooLong, sealed_));
    }

    [Fact]
    public void SealTo_PayloadSizeIsPlaintextPlusOverhead()
    {
        using var keypair = UserKeyPair.Generate();
        var plaintext = new byte[100];

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);

        Assert.Equal(100 + 60, sealed_.Length);
    }

    [Fact]
    public void SealTo_DifferentRecipients_ProduceDifferentCiphertexts()
    {
        using var recipientA = UserKeyPair.Generate();
        using var recipientB = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3 };

        var sealedA = UserKeyPair.SealTo(recipientA.PublicKey, plaintext);
        var sealedB = UserKeyPair.SealTo(recipientB.PublicKey, plaintext);

        Assert.NotEqual(sealedA, sealedB);
    }

    [Fact]
    public void MultipleSealsToSameRecipient_AllOpenCorrectly()
    {
        using var keypair = UserKeyPair.Generate();
        var dek = new byte[32];
        RandomNumberGenerator.Fill(dek);

        var sealedA = UserKeyPair.SealTo(keypair.PublicKey, dek);
        var sealedB = UserKeyPair.SealTo(keypair.PublicKey, dek);
        var sealedC = UserKeyPair.SealTo(keypair.PublicKey, dek);

        using var recoveredA = UserKeyPair.OpenSealed(keypair.PrivateKey, sealedA);
        using var recoveredB = UserKeyPair.OpenSealed(keypair.PrivateKey, sealedB);
        using var recoveredC = UserKeyPair.OpenSealed(keypair.PrivateKey, sealedC);

        AssertSecureBytesEqual(dek, recoveredA);
        AssertSecureBytesEqual(dek, recoveredB);
        AssertSecureBytesEqual(dek, recoveredC);
    }

    private static void AssertSecureBytesEqual(byte[] expected, SecureBytes actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        var copy = new byte[actual.Length];
        actual.CopyTo(copy);
        Assert.Equal(expected, copy);
    }

    private static void AssertSecureBytesNotEqual(SecureBytes a, SecureBytes b)
    {
        var aCopy = new byte[a.Length];
        var bCopy = new byte[b.Length];
        a.CopyTo(aCopy);
        b.CopyTo(bCopy);
        Assert.NotEqual(aCopy, bCopy);
    }
}