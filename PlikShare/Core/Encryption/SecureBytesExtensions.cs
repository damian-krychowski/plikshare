using PlikShare.Storages;
using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

public static class SecureBytesExtensions
{
    extension(SecureBytes secureBytes)
    {
        /// <summary>
        /// Converts an in-memory secure-bytes into its wire form. The secure plaintext is read
        /// only inside the pinned SecureBytes buffer — it is encrypted by
        /// <paramref name="masterEncryption"/> directly from that span, never copied
        /// to an unpinned heap byte[].
        /// </summary>
        public byte[] ToWire(
            IMasterDataEncryption masterEncryption)
        {
            return secureBytes.Use(
                masterEncryption,
                static (span, enc) => enc.EncryptBytes(span));
        }

        public void HkdfDeriveKey(ReadOnlySpan<byte> salt, Span<byte> output)
        {
            secureBytes.Use(
                state: new HkdfInput
                {
                    Salt = salt,
                    Output = output
                },
                action: static (ikmSpan, state) =>
                {
                    HKDF.DeriveKey(
                        hashAlgorithmName: HashAlgorithmName.SHA256,
                        ikm: ikmSpan,
                        output: state.Output,
                        salt: state.Salt,
                        info: null);
                });
        }
    }

    extension(SecureBytes)
    {
        /// <summary>
        /// Converts a wire secure-bytes back into its in-memory form. The plaintext is
        /// written directly into a freshly allocated pinned SecureBytes buffer —
        /// AesGcm.Decrypt writes straight into the pinned memory, so plaintext never
        /// lands on the unpinned heap.
        /// </summary>
        public static SecureBytes FromWire(
            byte[] encryptedSecureBytes,
            IMasterDataEncryption masterEncryption)
        {
            var plaintextLength = masterEncryption.GetDecryptedLength(
                encryptedSecureBytes);

            return SecureBytes.Create(
                length: plaintextLength,
                state: new DecryptState
                {
                    Encryption = masterEncryption,
                    EncryptedDek = encryptedSecureBytes
                },
                initializer: static (output, s) => s.Encryption.DecryptBytes(
                    s.EncryptedDek, 
                    output));
        }
    }

    private readonly ref struct DecryptState
    {
        public required IMasterDataEncryption Encryption { get; init; }
        public required byte[] EncryptedDek { get; init; }
    }

    private readonly ref struct HkdfInput
    {
        public ReadOnlySpan<byte> Salt { get; init; }
        public Span<byte> Output { get; init; }
    }
}