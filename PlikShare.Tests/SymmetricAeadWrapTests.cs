using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class SymmetricAeadWrapTests
{
    private static byte[] RandomKek() => RandomNumberGenerator.GetBytes(SymmetricAeadWrap.KekSize);
    private static byte[] RandomPayload(int size = 32) => RandomNumberGenerator.GetBytes(size);

    [Fact]
    public void Wrap_ProducesBlobOfExpectedSize()
    {
        var kek = RandomKek();
        var payload = RandomPayload();

        var wrapped = SymmetricAeadWrap.Wrap(kek, payload);

        var expectedSize = SymmetricAeadWrap.NonceSize + payload.Length + SymmetricAeadWrap.TagSize;
        Assert.Equal(expectedSize, wrapped.Length);
    }

    [Fact]
    public void Wrap_Unwrap_RoundtripRecoversPayload()
    {
        var kek = RandomKek();
        var payload = RandomPayload();

        var wrapped = SymmetricAeadWrap.Wrap(kek, payload);
        using var unwrapped = SymmetricAeadWrap.Unwrap(kek, wrapped);

        AssertSecureBytesEqual(payload, unwrapped);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void Wrap_Unwrap_RoundtripWorksForVariousPayloadSizes(int size)
    {
        var kek = RandomKek();
        var payload = RandomPayload(size);

        var wrapped = SymmetricAeadWrap.Wrap(kek, payload);
        using var unwrapped = SymmetricAeadWrap.Unwrap(kek, wrapped);

        AssertSecureBytesEqual(payload, unwrapped);
    }

    [Fact]
    public void Wrap_ProducesDifferentCiphertextsForSameInputs()
    {
        // Random nonce on each wrap → output differs even for identical KEK + payload.
        var kek = RandomKek();
        var payload = RandomPayload();

        var a = SymmetricAeadWrap.Wrap(kek, payload);
        var b = SymmetricAeadWrap.Wrap(kek, payload);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Unwrap_WithWrongKek_Throws()
    {
        var kek = RandomKek();
        var wrongKek = RandomKek();
        var payload = RandomPayload();

        var wrapped = SymmetricAeadWrap.Wrap(kek, payload);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            SymmetricAeadWrap.Unwrap(wrongKek, wrapped));
    }

    [Fact]
    public void Unwrap_WithTamperedCiphertext_Throws()
    {
        var kek = RandomKek();
        var payload = RandomPayload();

        var wrapped = SymmetricAeadWrap.Wrap(kek, payload);
        wrapped[SymmetricAeadWrap.NonceSize] ^= 0x01;  // flip a bit in ciphertext

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            SymmetricAeadWrap.Unwrap(kek, wrapped));
    }

    [Fact]
    public void Unwrap_WithTamperedTag_Throws()
    {
        var kek = RandomKek();
        var payload = RandomPayload();

        var wrapped = SymmetricAeadWrap.Wrap(kek, payload);
        wrapped[^1] ^= 0x01;  // flip a bit in tag

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            SymmetricAeadWrap.Unwrap(kek, wrapped));
    }

    [Fact]
    public void Unwrap_WithTamperedNonce_Throws()
    {
        var kek = RandomKek();
        var payload = RandomPayload();

        var wrapped = SymmetricAeadWrap.Wrap(kek, payload);
        wrapped[0] ^= 0x01;  // flip a bit in nonce

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            SymmetricAeadWrap.Unwrap(kek, wrapped));
    }

    [Fact]
    public void Wrap_WithWrongKekSize_Throws()
    {
        var payload = RandomPayload();

        Assert.Throws<ArgumentException>(() =>
            SymmetricAeadWrap.Wrap(new byte[16], payload));

        Assert.Throws<ArgumentException>(() =>
            SymmetricAeadWrap.Wrap(new byte[64], payload));
    }

    [Fact]
    public void Wrap_WithEmptyPayload_Throws()
    {
        var kek = RandomKek();

        Assert.Throws<ArgumentException>(() =>
            SymmetricAeadWrap.Wrap(kek, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Unwrap_WithWrongKekSize_Throws()
    {
        var wrapped = new byte[SymmetricAeadWrap.NonceSize + 32 + SymmetricAeadWrap.TagSize];

        Assert.Throws<ArgumentException>(() =>
            SymmetricAeadWrap.Unwrap(new byte[16], wrapped));
    }

    [Fact]
    public void Unwrap_WithTooShortBlob_Throws()
    {
        var kek = RandomKek();
        var tooShort = new byte[SymmetricAeadWrap.NonceSize + SymmetricAeadWrap.TagSize];

        Assert.Throws<ArgumentException>(() =>
            SymmetricAeadWrap.Unwrap(kek, tooShort));
    }

    [Fact]
    public void WrapUnwrap_SamePayload_TwoDifferentKeks_BothOpen()
    {
        // Realistic: the same private key is wrapped once with password-KEK and once with recovery-KEK.
        // Both wraps must open independently.
        var passwordKek = RandomKek();
        var recoveryKek = RandomKek();
        var payload = RandomPayload();

        var passwordWrap = SymmetricAeadWrap.Wrap(passwordKek, payload);
        var recoveryWrap = SymmetricAeadWrap.Wrap(recoveryKek, payload);

        using var fromPassword = SymmetricAeadWrap.Unwrap(passwordKek, passwordWrap);
        using var fromRecovery = SymmetricAeadWrap.Unwrap(recoveryKek, recoveryWrap);

        AssertSecureBytesEqual(payload, fromPassword);
        AssertSecureBytesEqual(payload, fromRecovery);
    }

    private static void AssertSecureBytesEqual(byte[] expected, SecureBytes actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        var copy = new byte[actual.Length];
        actual.CopyTo(copy);
        Assert.Equal(expected, copy);
    }
}
