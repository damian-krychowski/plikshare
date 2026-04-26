using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Deterministic DEK derivation from a recovery seed.
/// For the same recoveryBytes and version this function always returns the same DEK —
/// so a recovery code alone (without the database) is sufficient to reconstruct the DEK.
/// </summary>
public static class StorageDekDerivation
{
    public const int DekSize = 32;

    private static readonly byte[] InfoPrefix = "plikshare-dek\0"u8.ToArray();

    public static byte[] DeriveDek(ReadOnlySpan<byte> recoveryBytes, uint version)
    {
        Span<byte> info = stackalloc byte[InfoPrefix.Length + sizeof(uint)];
        InfoPrefix.CopyTo(info);
        BinaryPrimitives.WriteUInt32BigEndian(info[InfoPrefix.Length..], version);

        var dek = new byte[DekSize];

        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: recoveryBytes,
            output: dek,
            salt: [],
            info: info);

        return dek;
    }
}
