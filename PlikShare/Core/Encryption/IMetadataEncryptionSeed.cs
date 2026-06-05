namespace PlikShare.Core.Encryption;

public interface IMetadataEncryptionSeed: IDisposable
{
    IMetadataEncryptionSeed DeriveNew();
    EncryptableMetadata ToEncryptableMetadata(string value);
    EncodedMetadataValue EncodeMetadata(string value);
}

