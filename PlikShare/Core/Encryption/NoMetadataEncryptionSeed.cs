namespace PlikShare.Core.Encryption;

public sealed class NoMetadataEncryptionSeed : IMetadataEncryptionSeed
{
    public static NoMetadataEncryptionSeed Instance { get; } = new();

    public IMetadataEncryptionSeed DeriveNew()
    {
        return Instance;
    }
    
    public EncodedMetadataValue EncodeMetadata(string value)
    {
        return new EncodedMetadataValue(value);
    }

    public void Dispose()
    {
    }
}