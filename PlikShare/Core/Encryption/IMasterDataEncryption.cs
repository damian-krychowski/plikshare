namespace PlikShare.Core.Encryption;

public interface IMasterDataEncryption
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] versionedEncryptedBytes);

    IDerivedMasterDataEncryption NewDerived();
    IDerivedMasterDataEncryption DerivedFrom(byte[] versionedEncryptedBytes);
    IDerivedMasterDataEncryption DeserializeDerived(byte[] serialized);
}