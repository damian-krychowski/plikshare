namespace PlikShare.Core.Encryption;

public sealed class NoMetadataEncryptionSeed : IMetadataEncryptionSeed
{
    public static NoMetadataEncryptionSeed Instance { get; } = new();

    public IMetadataEncryptionSeed DeriveNew()
    {
        return Instance;
    }

    public EncryptableMetadata ToEncryptableMetadata(string value)
    {
        return new EncryptableMetadata(
            Value: value,
            EncryptionMode: NoMetadataEncryption.Instance);
    }

    public EncodedMetadataValue EncodeMetadata(string value)
    {
        return new EncodedMetadataValue(value);
    }

    public void Dispose()
    {
    }
}