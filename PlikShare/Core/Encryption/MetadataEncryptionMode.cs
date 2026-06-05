using System.Security.Cryptography;
using PlikShare.Core.Encryption;

/// <summary>
/// Polymorphic descriptor of how a single metadata field (folder name, file name,
/// upload name, comment body, …) should be encoded before being bound to its SQLite
/// parameter.
///
/// The choice between modes is made at request-entry time based on whether the caller
/// has an unlocked <see cref="WorkspaceEncryptionSession"/>. See
/// <c>EncryptableMetadataExtensions.ToEncryptableMetadata</c> for the resolution rule.
/// Consumers pair this mode with a plaintext value inside <see cref="EncryptableMetadata"/>
/// and hand both to the command-builder extension <c>WithEncryptableParameter</c>, which
/// does the actual <c>Encode</c> and binds the resulting string.
///
/// This type intentionally mirrors the simpler shape of
/// <see cref="FileEncryptionMode"/>: metadata values are short, single-shot strings that
/// do not need key-derivation chains, nonce prefixes, or multi-chunk streaming. Keeping
/// the metadata mode independent avoids conflating the two surfaces and leaves room for
/// both to evolve on their own version axes.
/// </summary>
public abstract record MetadataEncryptionMode;

/// <summary>
/// Store the plaintext value verbatim as TEXT. Used whenever the workspace is NOT
/// full-encrypted (managed-encryption or no-encryption storage) — the DB already sees
/// no user content in cleartext elsewhere, so the DB admin hardening that
/// <see cref="AesGcmMetadataV1Encryption"/> provides would be cosmetic there.
/// </summary>
public sealed record NoMetadataEncryption: MetadataEncryptionMode
{
    public static NoMetadataEncryption Instance { get; } = new();
}

/// <summary>
/// AES-256-GCM encryption keyed by a Workspace DEK (owned by a
/// <see cref="WorkspaceEncryptionSession"/>), with a fresh per-call 12-byte random nonce.
///
/// Produces the on-disk envelope:
/// <c>[format(1) | key_version(1) | chain_steps_count(1) | N × step_salt(32) | nonce(12) | ciphertext | tag(16)]</c>.
/// base64-encoded when bound to the TEXT column. The leading <c>format_version</c> byte
/// (currently <c>0x01</c>) leaves room for future envelope format upgrades; the
/// <c>key_version</c> byte records which Workspace DEK version the payload was sealed
/// under, so decrypt can pick the matching DEK from the session
///
/// Nonce and envelope bytes are produced transiently at encrypt time (stackalloc /
/// ArrayPool) rather than stored on this record — the record only carries the inputs
/// that are stable for the lifetime of the operation (<see cref="Ikm"/>,
/// <see cref="KeyVersion"/>).
///
/// <see cref="Ikm"/> is NOT owned by this record: it is borrowed from the enclosing
/// <see cref="WorkspaceEncryptionSession"/> and must not be disposed here.
/// </summary>
public sealed record AesGcmMetadataV1Encryption(
    MetadataAesInputsV1 Input) : MetadataEncryptionMode;

public sealed class MetadataAesInputsV1 : IDisposable
{
    private int _disposed;

    public required byte[] MetadataKey { get; init; }
    public required byte KeyVersion { get; init; }

    //this chain in contrary to file aes inputs does not contain workspace salt,
    //but it does contain metadata salt - that is a small inconsistency but yeap, sorry :)
    //workspace salt is not here because there is no point to copy it into every metadata
    //when db is lost we cannot reproduce metadata anyway, and that was the only reason
    //why all salts are included in files version of this property
    public required IReadOnlyList<byte[]> ChainStepSalts { get; init; }

    public void Deconstruct(
        out byte[] metadataKey,
        out byte keyVersion,
        out IReadOnlyList<byte[]> chainStepSalts)
    {
        metadataKey = MetadataKey;
        keyVersion = KeyVersion;
        chainStepSalts = ChainStepSalts;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        CryptographicOperations.ZeroMemory(MetadataKey);
    }

    public static MetadataAesInputsV1 Prepare(
        SecureBytes ikm,
        byte keyVersion,
        IReadOnlyList<byte[]> chainStepSalts)
    {
        var fileKey = new byte[Aes256GcmStreamingV2.DerivedKeySize];

        ikm.DeriveKey(
            chainStepSalts: chainStepSalts,
            output: fileKey);

        return new MetadataAesInputsV1
        {
            MetadataKey = fileKey,
            KeyVersion = keyVersion,
            ChainStepSalts = chainStepSalts,
        };
    }

    public static MetadataAesInputsV1 Prepare(
        FullEncryptionSeed seed)
    {
        var metadataKey = new byte[KeyDerivationChain.DerivedKeySize];
        seed.Key.CopyTo(metadataKey);
        
        return new MetadataAesInputsV1
        {
            MetadataKey = metadataKey,
            KeyVersion = seed.IkmKeyVersion,
            ChainStepSalts = seed.ChainStepSalts
        };
    }
}

public static class NoMetadataEncryptionExtensions
{
    extension(NoMetadataEncryption)
    {
        public static EncryptableMetadata Prepare(string value)
        {
            return new EncryptableMetadata(
                Value: value,
                EncryptionMode: NoMetadataEncryption.Instance);
        }
    }
}