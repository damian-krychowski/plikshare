namespace PlikShare.Core.Encryption;

public readonly record struct EncryptableMetadata(
    string Value,
    MetadataEncryptionMode EncryptionMode)
{
    public override string ToString()
    {
        return EncryptionMode is NoMetadataEncryption
            ? Value
            : "[encrypted]";
    }

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
        var raw = EncryptionMode switch
        {
            NoMetadataEncryption => Value,

            AesGcmMetadataV1Encryption aes => AesGcmMetadataV1.Encode(
                value: Value,
                aesInput: aes.Input),

            _ => throw new InvalidOperationException(
                $"Unsupported metadata encryption mode '{EncryptionMode.GetType().Name}'.")
        };

        return new EncodedMetadataValue(raw);
    }
}