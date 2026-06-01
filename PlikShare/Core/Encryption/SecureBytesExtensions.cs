using System.Buffers;

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

        public void DeriveKey(IReadOnlyList<byte[]> chainStepSalts, Span<byte> output)
        {
            var totalLength = 0;
            for (var i = 0; i < chainStepSalts.Count; i++)
                totalLength += chainStepSalts[i].Length;

            var rented = totalLength > 512
                ? ArrayPool<byte>.Shared.Rent(totalLength)
                : null;

            try
            {
                var flattened = rented is null
                    ? stackalloc byte[totalLength]
                    : rented.AsSpan(0, totalLength);

                var offset = 0;
                for (var i = 0; i < chainStepSalts.Count; i++)
                {
                    chainStepSalts[i].CopyTo(flattened[offset..]);
                    offset += chainStepSalts[i].Length;
                }

                secureBytes.DeriveKey(
                    flattened, 
                    output);
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        public void DeriveKey(ReadOnlySpan<byte> chainStepSalts, Span<byte> output)
        {
            secureBytes.Use(
                state: new HkdfInput
                {
                    ChainStepSalts = chainStepSalts,
                    Output = output
                },
                action: static (ikmSpan, state) =>
                {
                    KeyDerivationChain.Derive(
                        startingDek: ikmSpan,
                        chainSalts: state.ChainStepSalts,
                        output: state.Output);
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
        public ReadOnlySpan<byte> ChainStepSalts { get; init; }
        public Span<byte> Output { get; init; }
    }
}