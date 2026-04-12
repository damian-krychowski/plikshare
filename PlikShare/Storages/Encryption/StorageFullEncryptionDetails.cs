namespace PlikShare.Storages.Encryption;

public record StorageFullEncryptionDetails(
    byte[] Salt,
    byte[] VerifyHash,
    byte[] PublicKey,
    byte[] EncryptedPrivateKey,
    List<byte[]> EncryptedDeks);
