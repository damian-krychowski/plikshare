namespace PlikShare.Core.Encryption;

public static class EncryptableMetadataExtensions
{
    extension(WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        public string DecodeEncryptableMetadata(EncodedMetadataValue encoded)
        {
            return workspaceEncryptionSession.DecodeEncryptableMetadata(
                encoded.Encoded);
        }

        public string DecodeEncryptableMetadata(string encoded)
        {
            if (workspaceEncryptionSession is null)
                return encoded;

            return AesGcmMetadataV1.Decode(
                encoded,
                workspaceEncryptionSession);
        }
    }

    extension(EncryptableMetadata metadata)
    {
        /// <summary>
        /// Returns the on-wire / at-rest form of this metadata wrapped in
        /// <see cref="EncodedMetadataValue"/>:
        ///   - <see cref="NoMetadataEncryption"/>: the raw value verbatim.
        ///   - <see cref="AesGcmMetadataV1Encryption"/>: base64 of the encrypted envelope
        ///     prefixed with <see cref="ReservedPrefix"/>.
        /// Boundaries that need a plain <c>string</c> (SQLite TEXT parameter, JSON writer)
        /// extract it via <see cref="EncodedMetadataValue.Encoded"/>.
        /// </summary>
        public EncodedMetadataValue Encode()
        {
            var raw = metadata.EncryptionMode switch
            {
                NoMetadataEncryption => metadata.Value,

                AesGcmMetadataV1Encryption aes => AesGcmMetadataV1.Encode(
                    value: metadata.Value,
                    aesInput: aes.Input),

                _ => throw new InvalidOperationException(
                    $"Unsupported metadata encryption mode '{metadata.EncryptionMode.GetType().Name}'.")
            };

            return new EncodedMetadataValue(raw);
        }
    }
}