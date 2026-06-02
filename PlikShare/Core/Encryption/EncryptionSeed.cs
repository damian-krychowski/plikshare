using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// A single-use intermediate DEK derived from the workspace DEK via one HKDF step:
/// <c>seed = HKDF(workspace_dek, salt)</c>. Designed to be handed to subsystems that need
/// to produce per-value encryption keys without ever holding the workspace DEK.
///
/// <para>Each per-value key is one further HKDF step from the seed:
/// <c>per_value_key = HKDF(seed, valueSalt)</c>. The resulting envelope's chain step salts
/// list both salts in order (<c>[seedSalt, valueSalt]</c>); a decoder walks the chain from
/// the workspace DEK and arrives at the same per_value_key without ever needing the seed.</para>
///
/// <para>Security shape:</para>
/// <list type="bullet">
///   <item>Seed leak compromises every value encrypted under that seed, but NOTHING else —
///   workspace DEK cannot be recovered from a seed (HKDF is one-way).</item>
///   <item>Per-value key leak compromises only that one value.</item>
///   <item>Seed scope is up to the caller — typically one trigger / one job batch.</item>
/// </list>
///
/// Single-use lifecycle: <see cref="Dispose"/> zeroes <see cref="Seed"/>. The encrypt path
/// that uses this instance is responsible for disposal at end of scope.
/// </summary>
public sealed class EncryptionSeed : IDisposable
{
    private int _disposed;

    /// <summary>
    /// Storage DEK version the parent workspace DEK was derived under. Carried so envelopes
    /// encrypted under this seed pick the right workspace DEK at decrypt time.
    /// </summary>
    public required byte KeyVersion { get; init; }

    /// <summary>
    /// 32-byte salt that produced <see cref="Seed"/> from the workspace DEK. NOT a secret —
    /// gets written into the envelope's chain step salts so a decoder can re-derive.
    /// </summary>
    public required byte[] Salt { get; init; }

    /// <summary>
    /// 32-byte derived seed. The secret — leak = loss of every value encrypted under it.
    /// </summary>
    public required byte[] Seed { get; init; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        CryptographicOperations.ZeroMemory(Seed);
    }

    public static EncryptionSeed DeriveNew(
        SecureBytes ikm,
        byte keyVersion)
    {
        var salt = RandomNumberGenerator.GetBytes(KeyDerivationChain.StepSaltSize);
        var seed = new byte[KeyDerivationChain.DerivedKeySize];

        ikm.DeriveKey(
            chainStepSalts: salt,
            output: seed);

        return new EncryptionSeed
        {
            KeyVersion = keyVersion,
            Salt = salt,
            Seed = seed
        };
    }

    public static EncryptionSeed DeriveNew(WorkspaceEncryptionSession session)
    {
        var latest = session.GetLatestDek();

        return DeriveNew(
            ikm: latest.Dek,
            keyVersion: (byte)latest.StorageDekVersion);
    }
}

/// <summary>
/// Wire form of <see cref="EncryptionSeed"/>. The plaintext seed is replaced by
/// <see cref="EncryptedSeed"/>: AES-GCM ciphertext under the process-wide master key
/// (via <see cref="IMasterDataEncryption"/>). Designed to be safely held in long-lived
/// caches and queue payloads — a heap dump yields only ciphertext, useless without the
/// master key (which lives in a mlocked <see cref="SecureBytes"/> for the process
/// lifetime). The other fields (<see cref="KeyVersion"/>, <see cref="Salt"/>) are not
/// secret and stay plaintext.
/// </summary>
public sealed class EncryptionSeedWire
{
    public required byte KeyVersion { get; init; }
    public required byte[] Salt { get; init; }

    /// <summary>
    /// The 32-byte seed encrypted with <see cref="IMasterDataEncryption.EncryptBytes"/>.
    /// Produced by <see cref="Prepare(SecureBytes, byte, IMasterDataEncryption)"/>: HKDF
    /// writes the plaintext seed into a stack-allocated buffer, master encryption reads
    /// from that buffer, the finally zeroes it — the raw seed never lands on the managed
    /// heap.
    /// </summary>
    public required byte[] EncryptedSeed { get; init; }

    public static EncryptionSeedWire Prepare(
        SecureBytes ikm,
        byte keyVersion,
        IMasterDataEncryption masterEncryption)
    {
        // Same trick as FileAesInputsV2Wire.Prepare — plaintext seed lives ONLY on this
        // stack frame: HKDF writes it, master encryption reads it, finally zeroes it.
        // No heap, no SecureBytes alloc. 32 bytes — well within stack budget.
        var salt = RandomNumberGenerator.GetBytes(KeyDerivationChain.StepSaltSize);
        Span<byte> seed = stackalloc byte[KeyDerivationChain.DerivedKeySize];

        try
        {
            ikm.DeriveKey(
                chainStepSalts: salt,
                output: seed);

            return new EncryptionSeedWire
            {
                KeyVersion = keyVersion,
                Salt = salt,
                EncryptedSeed = masterEncryption.EncryptBytes(seed)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    public static EncryptionSeedWire Prepare(
        WorkspaceEncryptionSession session,
        IMasterDataEncryption masterEncryption)
    {
        var latest = session.GetLatestDek();

        return Prepare(
            ikm: latest.Dek,
            keyVersion: (byte)latest.StorageDekVersion,
            masterEncryption: masterEncryption);
    }


    /// <summary>
    /// Reconstructs an <see cref="EncryptionSeed"/> from its wire form by master-decrypting
    /// the seed bytes into a freshly-allocated buffer. On AES-GCM tag failure (corrupted
    /// wire or wrong master key) <see cref="IMasterDataEncryption.DecryptBytes"/> throws
    /// before writing anything, so the buffer stays at its zero-initialized state and the
    /// partially-filled-plaintext class of bugs is impossible. The returned instance is
    /// single-use — caller disposes when done to zero <see cref="EncryptionSeed.Seed"/>.
    /// </summary>
    public EncryptionSeed Unwrap(
        IMasterDataEncryption masterEncryption)
    {
        var seed = new byte[KeyDerivationChain.DerivedKeySize];

        masterEncryption.DecryptBytes(
            versionedEncryptedBytes: EncryptedSeed,
            destination: seed);

        return new EncryptionSeed
        {
            KeyVersion = KeyVersion,
            Salt = Salt,
            Seed = seed
        };
    }
}
