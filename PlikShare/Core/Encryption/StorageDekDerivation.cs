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

    public static SecureBytes DeriveDek(ReadOnlySpan<byte> recoveryBytes, uint version)
    {
        return SecureBytes.Create(
            length: DekSize,
            state: new Input
            {
                RecoveryBytes = recoveryBytes,
                Version = version
            },
            initializer: static (output, state) =>
            {
                Span<byte> info = stackalloc byte[InfoPrefix.Length + sizeof(uint)];
                InfoPrefix.CopyTo(info);

                BinaryPrimitives.WriteUInt32BigEndian(
                    destination: info[InfoPrefix.Length..], 
                    value: state.Version);
                
                HKDF.DeriveKey(
                    hashAlgorithmName: HashAlgorithmName.SHA256,
                    ikm: state.RecoveryBytes,
                    output: output,
                    salt: [],
                    info: info);
            });
    }

    private readonly ref struct Input
    {
        public required ReadOnlySpan<byte> RecoveryBytes { get; init; }
        public required uint Version { get; init; }
    }
}
