namespace PlikShare.Storages.Encryption;

/// <summary>
/// Storage-level state for a full-encrypted storage. The Storage DEK itself is NOT stored here —
/// it lives per-user in <c>sek_storage_encryption_keys</c> (wrapped to each storage admin's
/// X25519 public key). The only piece of storage-scoped material still persisted on the storage
/// row is <see cref="RecoveryVerifyHash"/>, which lets the recovery-code reset flow verify a
/// caller-provided recovery seed without needing any wrap.
/// </summary>
public record StorageFullEncryptionDetails(
    byte[] RecoveryVerifyHash);
