using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption;

public static class StorageManagedEncryptionService
{
    private const int RecoverySeedSize = 32;

    public static GenerateDetailsResult GenerateDetails()
    {
        Span<byte> recoveryBytes = stackalloc byte[RecoverySeedSize];

        try
        {
            RandomNumberGenerator.Fill(recoveryBytes);

            // IKM_v0 is deterministically derived from the recovery seed, so the
            // recovery code alone (used by an offline tool) is sufficient to
            // reconstruct the IKM if the database is ever lost.
            using var ikmV0 = StorageDekDerivation.DeriveDek(
                recoveryBytes,
                version: 0);

            var recoveryCode = RecoveryCodeCodec.Encode(
                recoveryBytes);

            var ikmV0Base64 = ikmV0.Use(static span => Convert.ToBase64String(span));

            var details = new StorageManagedEncryptionDetails(
                Ikms: [ikmV0Base64]);

            return new GenerateDetailsResult(details, recoveryCode);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryBytes);
        }
    }

    public record GenerateDetailsResult(
        StorageManagedEncryptionDetails Details,
        string RecoveryCode);
}
