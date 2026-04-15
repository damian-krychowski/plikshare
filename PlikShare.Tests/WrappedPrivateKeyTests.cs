using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class WrappedPrivateKeyTests
{
    private static byte[] RandomKek() => RandomNumberGenerator.GetBytes(WrappedPrivateKey.KekSize);
    private static byte[] RandomPrivateKey() => RandomNumberGenerator.GetBytes(UserKeyPair.PrivateKeySize);

    [Fact]
    public void Wrap_ProducesBlobOfExpectedSize()
    {
        var kek = RandomKek();
        var privateKey = RandomPrivateKey();

        var wrapped = WrappedPrivateKey.Wrap(kek, privateKey);

        var expectedSize = WrappedPrivateKey.NonceSize + UserKeyPair.PrivateKeySize + WrappedPrivateKey.TagSize;
        Assert.Equal(expectedSize, wrapped.Length);
    }

    [Fact]
    public void Wrap_Unwrap_RoundtripRecoversPrivateKey()
    {
        var kek = RandomKek();
        var privateKey = RandomPrivateKey();

        var wrapped = WrappedPrivateKey.Wrap(kek, privateKey);
        var unwrapped = WrappedPrivateKey.Unwrap(kek, wrapped);

        Assert.Equal(privateKey, unwrapped);
    }

    [Fact]
    public void Wrap_ProducesDifferentCiphertextsForSameInputs()
    {
        // Random nonce on each wrap → output differs even for identical KEK + private key.
        var kek = RandomKek();
        var privateKey = RandomPrivateKey();

        var a = WrappedPrivateKey.Wrap(kek, privateKey);
        var b = WrappedPrivateKey.Wrap(kek, privateKey);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Unwrap_WithWrongKek_Throws()
    {
        var kek = RandomKek();
        var wrongKek = RandomKek();
        var privateKey = RandomPrivateKey();

        var wrapped = WrappedPrivateKey.Wrap(kek, privateKey);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            WrappedPrivateKey.Unwrap(wrongKek, wrapped));
    }

    [Fact]
    public void Unwrap_WithTamperedCiphertext_Throws()
    {
        var kek = RandomKek();
        var privateKey = RandomPrivateKey();

        var wrapped = WrappedPrivateKey.Wrap(kek, privateKey);
        wrapped[WrappedPrivateKey.NonceSize] ^= 0x01;  // flip a bit in ciphertext

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            WrappedPrivateKey.Unwrap(kek, wrapped));
    }

    [Fact]
    public void Unwrap_WithTamperedTag_Throws()
    {
        var kek = RandomKek();
        var privateKey = RandomPrivateKey();

        var wrapped = WrappedPrivateKey.Wrap(kek, privateKey);
        wrapped[^1] ^= 0x01;  // flip a bit in tag

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            WrappedPrivateKey.Unwrap(kek, wrapped));
    }

    [Fact]
    public void Unwrap_WithTamperedNonce_Throws()
    {
        var kek = RandomKek();
        var privateKey = RandomPrivateKey();

        var wrapped = WrappedPrivateKey.Wrap(kek, privateKey);
        wrapped[0] ^= 0x01;  // flip a bit in nonce

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            WrappedPrivateKey.Unwrap(kek, wrapped));
    }

    [Fact]
    public void Wrap_WithWrongKekSize_Throws()
    {
        var privateKey = RandomPrivateKey();

        Assert.Throws<ArgumentException>(() =>
            WrappedPrivateKey.Wrap(new byte[16], privateKey));

        Assert.Throws<ArgumentException>(() =>
            WrappedPrivateKey.Wrap(new byte[64], privateKey));
    }

    [Fact]
    public void Wrap_WithWrongPrivateKeySize_Throws()
    {
        var kek = RandomKek();

        Assert.Throws<ArgumentException>(() =>
            WrappedPrivateKey.Wrap(kek, new byte[16]));

        Assert.Throws<ArgumentException>(() =>
            WrappedPrivateKey.Wrap(kek, new byte[64]));
    }

    [Fact]
    public void Unwrap_WithWrongBlobSize_Throws()
    {
        var kek = RandomKek();

        Assert.Throws<ArgumentException>(() =>
            WrappedPrivateKey.Unwrap(kek, new byte[30]));

        Assert.Throws<ArgumentException>(() =>
            WrappedPrivateKey.Unwrap(kek, new byte[100]));
    }

    [Fact]
    public void WrapUnwrap_SamePrivateKey_TwoDifferentKeks_BothOpen()
    {
        // Realistic: the same private key is wrapped once with password-KEK and once with recovery-KEK.
        // Both wraps must open independently.
        var passwordKek = RandomKek();
        var recoveryKek = RandomKek();
        var privateKey = RandomPrivateKey();

        var passwordWrap = WrappedPrivateKey.Wrap(passwordKek, privateKey);
        var recoveryWrap = WrappedPrivateKey.Wrap(recoveryKek, privateKey);

        Assert.Equal(privateKey, WrappedPrivateKey.Unwrap(passwordKek, passwordWrap));
        Assert.Equal(privateKey, WrappedPrivateKey.Unwrap(recoveryKek, recoveryWrap));
    }
}
