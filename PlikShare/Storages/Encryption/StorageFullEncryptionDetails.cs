namespace PlikShare.Storages.Encryption;

public record StorageFullEncryptionDetails(
    byte[] Salt,
    byte[] VerifyHash,
    List<byte[]> EncryptedDeks,
    byte[] RecoveryVerifyHash);
