namespace PlikShare.Core.Encryption;

public static class EncryptionSeedExtensions
{
    extension(EncryptionSeed seed)
    {
        /// <summary>
        /// Wraps <paramref name="value"/> in an <see cref="EncryptableMetadata"/> whose
        /// encryption mode is keyed by a per-value <see cref="MetadataAesInputsV1"/> derived
        /// from this seed. Symmetric to <c>session.ToEncryptableMetadata(value)</c> but the
        /// caller only needs the seed — no workspace DEK / session required.
        ///
        /// <para>Every call generates a fresh <c>metadataSalt</c> inside
        /// <see cref="MetadataAesInputsV1.Prepare(EncryptionSeed)"/>, so multiple values
        /// produced under the same seed each get their own metadata key. A decoder walks the
        /// envelope's chain <c>[seed.Salt, metadataSalt]</c> from the workspace DEK and
        /// arrives at the same key.</para>
        ///
        /// Rejects values starting with the <see cref="EncryptableMetadataExtensions.ReservedPrefix"/>
        /// for the same reason as the session-based path: the prefix is the unambiguous
        /// signal that a column holds an encrypted envelope, and accepting smuggled
        /// plaintext here would corrupt that invariant.
        /// </summary>
        public EncryptableMetadata ToEncryptableMetadata(string value)
        {
            if (value.StartsWith(AesGcmMetadataV1.ReservedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Metadata value must not start with reserved prefix '{AesGcmMetadataV1.ReservedPrefix}'. " +
                    "Request validation should have rejected this input before reaching the encryption layer.");

            return new EncryptableMetadata(
                Value: value,
                EncryptionMode: new AesGcmMetadataV1Encryption(
                    Input: MetadataAesInputsV1.Prepare(seed)));
        }
    }
}
