using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption;

/// <summary>
/// Generates the material needed to create a new full-encrypted storage:
/// a random recovery seed, the deterministic v0 Storage DEK derived from that seed,
/// and the recovery verify hash used later to validate a caller-provided recovery code.
/// The caller is responsible for wrapping the DEK to the creator's X25519 public key
/// (via <see cref="UserKeyPair.SealTo"/>) and inserting it into <c>sek_storage_encryption_keys</c>,
/// then disposing the result so the plaintext DEK buffer is wiped.
/// </summary>
public static class StorageFullEncryptionService
{
    private const int RecoverySeedSize = 32;

    public static GenerateDetailsResult GenerateDetails()
    {
        Span<byte> recoveryBytes = stackalloc byte[RecoverySeedSize];

        try
        {
            RandomNumberGenerator.Fill(recoveryBytes);

            // Storage DEK v0 is deterministically derived from the recovery seed so the
            // recovery code alone (without the database) is sufficient to reconstruct
            // the DEK for offline file recovery.
            var dek = StorageDekDerivation.DeriveDek(
                recoveryBytes,
                version: 0);

            var recoveryVerifyHash = RecoveryVerifyHash.Compute(
                recoveryBytes);

            var recoveryCode = RecoveryCodeCodec.Encode(
                recoveryBytes);

            var details = new StorageFullEncryptionDetails(
                RecoveryVerifyHash: recoveryVerifyHash,
                LatestStorageDekVersion: 0);

            return new GenerateDetailsResult(
                Details: details,
                Dek: dek,
                RecoveryCode: recoveryCode);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryBytes);
        }
    }

    /// <summary>
    /// Output of <see cref="GenerateDetails"/>. The caller MUST dispose this result
    /// after sealing <see cref="Dek"/> to the creator's public key — disposal wipes
    /// the underlying secure buffer.
    /// </summary>
    public sealed record GenerateDetailsResult(
        StorageFullEncryptionDetails Details,
        SecureBytes Dek,
        string RecoveryCode) : IDisposable
    {
        public void Dispose() => Dek.Dispose();
    }
}
