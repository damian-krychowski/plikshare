namespace PlikShare.Core.Encryption;

/// <summary>
/// A metadata value in its on-wire / at-rest form: plaintext when the workspace is not
/// full-encrypted, or a <see cref="EncryptableMetadataExtensions.ReservedPrefix"/>-prefixed
/// base64 envelope when it is. Carried unchanged through cache, audit-log JSON, and other
/// passthrough storage layers that do not own a <see cref="WorkspaceEncryptionSession"/>
/// and therefore cannot decode.
///
/// Decoding to plaintext requires a session — see
/// <see cref="EncryptableMetadataExtensions.DecodeEncryptableMetadata"/>.
///
/// The point of this type (vs raw <c>string</c>) is to prevent treating an encrypted
/// envelope as plaintext: comparison, substring, display, logging. <see cref="ToString"/>
/// masks ciphertext to <c>[encrypted]</c> so an accidental log call cannot leak base64.
///
/// Zero allocation overhead — single-field <c>readonly record struct</c>, fits inline
/// in any container.
/// </summary>
public readonly record struct EncodedMetadataValue(string Encoded)
{
    public bool IsEncrypted => Encoded.StartsWith(
        EncryptableMetadataExtensions.ReservedPrefix, 
        StringComparison.Ordinal);

    public override string ToString() => IsEncrypted
        ? "[encrypted]"
        : Encoded;
}
