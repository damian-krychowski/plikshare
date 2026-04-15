namespace PlikShare.Storages.Encryption;

/// <summary>
/// Storage-level state for a full-encrypted storage. The Storage DEK itself is NOT stored here —
/// it lives per-user in <c>sek_storage_encryption_keys</c> (wrapped to each storage admin's
/// X25519 public key). Only storage-scoped invariants are persisted on the storage row:
///
/// * <see cref="RecoveryVerifyHash"/> — lets the recovery-code reset flow verify a
///   caller-provided recovery seed without needing any wrap.
/// * <see cref="LatestStorageDekVersion"/> — authoritative pointer to the "current" Storage
///   DEK version for this storage. New files are always encrypted under the Workspace DEK
///   derived from this version. Bumped atomically by the rotation flow in the same
///   transaction that inserts the new <c>sek_*</c> and <c>wek_*</c> wrap rows, so callers
///   never observe a half-rotated state.
/// </summary>
public record StorageFullEncryptionDetails(
    byte[] RecoveryVerifyHash,
    int LatestStorageDekVersion);
