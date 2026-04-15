using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class UserKeyPairTests
{
    [Fact]
    public void Generate_ReturnsKeysOfExpectedSize()
    {
        var keypair = UserKeyPair.Generate();

        Assert.Equal(UserKeyPair.PublicKeySize, keypair.PublicKey.Length);
        Assert.Equal(UserKeyPair.PrivateKeySize, keypair.PrivateKey.Length);
    }

    [Fact]
    public void Generate_ProducesDifferentKeypairsEachTime()
    {
        var a = UserKeyPair.Generate();
        var b = UserKeyPair.Generate();

        Assert.NotEqual(a.PublicKey, b.PublicKey);
        Assert.NotEqual(a.PrivateKey, b.PrivateKey);
    }

    [Fact]
    public void SealTo_OpenSealed_RoundTripRecoversPlaintext()
    {
        var keypair = UserKeyPair.Generate();
        var plaintext = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);
        var recovered = UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_);

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void SealTo_OpenSealed_RoundTripForRandom32ByteDek()
    {
        // Typical use: wrapping a 32-byte DEK (Workspace DEK, Storage DEK, etc.).
        var keypair = UserKeyPair.Generate();
        var dek = new byte[32];
        RandomNumberGenerator.Fill(dek);

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, dek);
        var recovered = UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_);

        Assert.Equal(dek, recovered);
    }

    [Fact]
    public void SealTo_ProducesDifferentCiphertextsForSamePlaintext()
    {
        // Ephemeral key on each seal → nonce + ciphertext differ even for identical inputs.
        // Non-deterministic encryption is a property sealed-box offers over naive symmetric wrap.
        var keypair = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        var a = UserKeyPair.SealTo(keypair.PublicKey, plaintext);
        var b = UserKeyPair.SealTo(keypair.PublicKey, plaintext);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void OpenSealed_WithWrongPrivateKey_Throws()
    {
        var recipient = UserKeyPair.Generate();
        var attacker = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3 };

        var sealed_ = UserKeyPair.SealTo(recipient.PublicKey, plaintext);

        Assert.Throws<InvalidOperationException>(() =>
            UserKeyPair.OpenSealed(attacker.PrivateKey, sealed_));
    }

    [Fact]
    public void OpenSealed_WithTamperedCiphertext_Throws()
    {
        var keypair = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);

        // Flip a bit in the ciphertext region (after ephemeral public key + nonce).
        sealed_[^5] ^= 0x01;

        Assert.Throws<InvalidOperationException>(() =>
            UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_));
    }

    [Fact]
    public void OpenSealed_WithTamperedEphemeralKey_Throws()
    {
        var keypair = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3 };

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);

        // Flip a bit in the ephemeral public key region (first 32 bytes).
        sealed_[0] ^= 0x01;

        Assert.Throws<InvalidOperationException>(() =>
            UserKeyPair.OpenSealed(keypair.PrivateKey, sealed_));
    }

    [Fact]
    public void OpenSealed_WithTruncatedPayload_Throws()
    {
        var keypair = UserKeyPair.Generate();
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
        var keypair = UserKeyPair.Generate();
        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, new byte[] { 1, 2, 3 });

        Assert.Throws<ArgumentException>(() =>
            UserKeyPair.OpenSealed(new byte[16], sealed_));

        Assert.Throws<ArgumentException>(() =>
            UserKeyPair.OpenSealed(new byte[64], sealed_));
    }

    [Fact]
    public void SealTo_PayloadSizeIsPlaintextPlusOverhead()
    {
        // Overhead: ephemeral public key (32) + nonce (12) + AEAD tag (16) = 60 bytes.
        var keypair = UserKeyPair.Generate();
        var plaintext = new byte[100];

        var sealed_ = UserKeyPair.SealTo(keypair.PublicKey, plaintext);

        Assert.Equal(100 + 60, sealed_.Length);
    }

    [Fact]
    public void SealTo_DifferentRecipients_ProduceDifferentCiphertexts()
    {
        var recipientA = UserKeyPair.Generate();
        var recipientB = UserKeyPair.Generate();
        var plaintext = new byte[] { 1, 2, 3 };

        var sealedA = UserKeyPair.SealTo(recipientA.PublicKey, plaintext);
        var sealedB = UserKeyPair.SealTo(recipientB.PublicKey, plaintext);

        Assert.NotEqual(sealedA, sealedB);
    }

    [Fact]
    public void MultipleSealsToSameRecipient_AllOpenCorrectly()
    {
        // A realistic scenario: wrap the same Workspace DEK to the same user multiple times
        // (e.g., rotation, re-wrap). Each wrap is independent and openable.
        var keypair = UserKeyPair.Generate();
        var dek = new byte[32];
        RandomNumberGenerator.Fill(dek);

        var sealedA = UserKeyPair.SealTo(keypair.PublicKey, dek);
        var sealedB = UserKeyPair.SealTo(keypair.PublicKey, dek);
        var sealedC = UserKeyPair.SealTo(keypair.PublicKey, dek);

        Assert.Equal(dek, UserKeyPair.OpenSealed(keypair.PrivateKey, sealedA));
        Assert.Equal(dek, UserKeyPair.OpenSealed(keypair.PrivateKey, sealedB));
        Assert.Equal(dek, UserKeyPair.OpenSealed(keypair.PrivateKey, sealedC));
    }
}
