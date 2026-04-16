using System.Security.Cryptography;
using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption;

public static class StorageManagedEncryptionService
{
    private const int RecoverySeedSize = 32;

    public static GenerateDetailsResult GenerateDetails()
    {
        Span<byte> recoveryBytes = stackalloc byte[RecoverySeedSize];
        byte[]? ikmV0 = null;

        try
        {
            RandomNumberGenerator.Fill(recoveryBytes);

            // IKM_v0 is deterministically derived from the recovery seed, so the
            // recovery code alone (used by an offline tool) is sufficient to
            // reconstruct the IKM if the database is ever lost.
            ikmV0 = HkdfDekDerivation.DeriveDek(
                recoveryBytes,
                 version: 0);

            var recoveryCode = RecoveryCodeCodec.Encode(
                recoveryBytes);

            var details = new StorageManagedEncryptionDetails(
                Ikms: [Convert.ToBase64String(ikmV0)]);

            return new GenerateDetailsResult(details, recoveryCode);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryBytes);

            if (ikmV0 != null)
                CryptographicOperations.ZeroMemory(ikmV0);
        }
    }

    public record GenerateDetailsResult(
        StorageManagedEncryptionDetails Details,
        string RecoveryCode);
}
