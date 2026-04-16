using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Recovery-code path for the user's X25519 private key — parallel to <see cref="EncryptionPasswordKdf"/>.
/// Encoding of recovery bytes to/from a 24-word BIP-39 mnemonic is shared with <see cref="RecoveryCodeCodec"/>.
///
/// Flow:
/// - Setup: generate seed (32 random bytes); store verify hash; wrap private key with recovery KEK.
/// - Reset: user pastes code; decode → seed; verify hash matches; derive recovery KEK; unwrap private key;
///   user sets new encryption password; re-wrap private key with new password-derived KEK.
///
/// The recovery seed is never stored. Its verify hash lets us confirm a pasted code without needing
/// the seed itself. Independent of the recovery mechanism for the storage (different domain labels).
/// </summary>
public static class UserEncryptionRecovery
{
    public const int RecoverySeedSize = 32;
    public const int RecoveryKekSize = 32;

    private static readonly byte[] KekInfo =
        "plikshare-user-encryption-recovery-kek\0"u8.ToArray();

    private static readonly byte[] VerifyInfo =
        "plikshare-user-encryption-recovery-verify\0"u8.ToArray();

    public static byte[] GenerateRecoverySeed()
        => RandomNumberGenerator.GetBytes(RecoverySeedSize);

    public static byte[] DeriveRecoveryKek(ReadOnlySpan<byte> recoverySeed)
    {
        if (recoverySeed.Length != RecoverySeedSize)
            throw new ArgumentException(
                $"Recovery seed must be {RecoverySeedSize} bytes, got {recoverySeed.Length}.",
                nameof(recoverySeed));

        var kek = new byte[RecoveryKekSize];

        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: recoverySeed,
            output: kek,
            salt: [],
            info: KekInfo);

        return kek;
    }

    public static byte[] ComputeVerifyHash(ReadOnlySpan<byte> recoverySeed)
    {
        if (recoverySeed.Length != RecoverySeedSize)
            throw new ArgumentException(
                $"Recovery seed must be {RecoverySeedSize} bytes, got {recoverySeed.Length}.",
                nameof(recoverySeed));

        Span<byte> authKey = stackalloc byte[32];

        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: recoverySeed,
            output: authKey,
            salt: [],
            info: VerifyInfo);

        return SHA256.HashData(authKey);
    }

    public static bool Verify(ReadOnlySpan<byte> recoverySeed, ReadOnlySpan<byte> expectedHash)
    {
        var actual = ComputeVerifyHash(recoverySeed);

        return CryptographicOperations.FixedTimeEquals(
            actual, 
            expectedHash);
    }
}
