namespace PlikShare.Core.Encryption;

/// <summary>
/// Derived encryption means that a set of records can be encrypt/decrypt with the same derived
/// key. In the current AES-GCM implementation means that all these records need to share the keyId and salt
/// so that the produced derived encryption key is the same for all of them.
///
/// The goal of this abstraction is to optimize processes, key derivation is costly, so if we need to decrypt
/// whole family of things it would mean that the price for derivation needs to be paid per each item.
/// Goal of IDerivedMasterDataEncryption is to pay this price only once and be able to reuse multiple times.
/// But still, each record should work fine if encrypted/decrypted with normal IMasterDataEncryption
/// so they need to contain all metadata stored inside encryptedBytes as normally
/// </summary>
public interface IDerivedMasterDataEncryption
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] versionedEncryptedBytes);
    byte[] Serialize();
}