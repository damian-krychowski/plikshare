using PlikShare.Core.Encryption;
public readonly record struct EncryptableMetadata(
    string Value,
    MetadataEncryptionMode EncryptionMode);
