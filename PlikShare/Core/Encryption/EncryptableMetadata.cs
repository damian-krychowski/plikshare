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
};