namespace PlikShare.Core.Encryption;

public interface IMetadataEncryptionSeed: IDisposable
{
    IMetadataEncryptionSeed DeriveNew();
    EncodedMetadataValue EncodeMetadata(string value);
}

