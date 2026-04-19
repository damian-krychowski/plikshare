namespace PlikShare.Core.Encryption;

public static class SecureBytesSerializationExtensions
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
                static (span, enc) => enc.FastEncryptBytes(span));
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
            var plaintextLength = masterEncryption.GetFastDecryptedLength(
                encryptedSecureBytes);

            return SecureBytes.Create(
                length: plaintextLength,
                state: new DecryptState
                {
                    Encryption = masterEncryption,
                    EncryptedDek = encryptedSecureBytes
                },
                initializer: static (output, s) => s.Encryption.FastDecryptBytes(
                    s.EncryptedDek, 
                    output));
        }
    }

    private readonly ref struct DecryptState
    {
        public required IMasterDataEncryption Encryption { get; init; }
        public required byte[] EncryptedDek { get; init; }
    }
}