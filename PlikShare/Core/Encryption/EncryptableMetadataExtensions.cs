using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Turns a plaintext string into an <see cref="EncryptableMetadata"/> carrying the right
/// <see cref="MetadataEncryptionMode"/> for the current request context, and encodes the
/// combined payload to the string the SQLite parameter binder consumes.
///
/// Mode resolution:
///   - workspace encryption session is null → <see cref="NoMetadataEncryption"/>:
///     <see cref="Encode"/> returns the raw string.
///   - workspace encryption session is present → <see cref="AesGcmV1MetadataEncryption"/>
///     using the latest Workspace DEK version; <see cref="Encode"/> generates a fresh
///     per-call nonce, AES-256-GCM encrypts the value, and returns <see cref="ReservedPrefix"/>
///     followed by base64 of the envelope:
///     <c>[format(1) | key_version(1) | chain_steps_count(1) | N × step_salt(32) | nonce(12) | ciphertext | tag(16)]</c>.
///
/// <para>
/// <b>Chain steps</b> mirror the file frame V2 design (see <c>Aes256GcmStreamingV2</c>):
/// each step records a 32-byte salt that derives a sub-DEK via HKDF from the parent DEK.
/// Currently every encode writes <c>chain_steps_count = 0</c> — direct workspace DEK use,
/// no sub-derivation. Future box-scoped writes will set <c>chain_steps_count = 1</c> with
/// a single 32-byte box salt; nested scopes can extend this further. Decode parses any count
/// and walks the salts; today it rejects counts &gt; 0 because the box derivation chain is
/// not yet implemented.
/// </para>
///
/// The <see cref="ReservedPrefix"/> gives encrypted values a self-identifying namespace
/// marker: any tool, db dump, or decode routine can check the first characters to tell
/// whether a metadata column value is an encrypted envelope or plaintext. User-supplied
/// metadata is rejected at the request validator layer if it starts with the prefix.
///
/// Column stays TEXT for both modes — consistent column type, no split between BLOB and
/// TEXT depending on workspace.
/// </summary>
public static class EncryptableMetadataExtensions
{
    private const byte FormatTagAesGcmV1 = 0x01;
    private const int StackAllocThresholdBytes = 512;
    private const int StepSaltSize = 32;
    private const int HeaderFixedSize = 3; // format + key_version + chain_steps_count

    /// <summary>
    /// Namespace marker prepended to every encrypted metadata string bound to SQLite.
    /// A value stored in a metadata column starts with <see cref="ReservedPrefix"/> if
    /// and only if it is an encrypted envelope. User-supplied plaintext metadata must
    /// NOT start with this prefix — request validation and <see cref="ToEncryptableMetadata"/>
    /// both reject such input so the prefix remains an unambiguous format signal for
    /// tooling, db dumps, and decode routines.
    /// </summary>
    public const string ReservedPrefix = "pse:";

    extension(WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        public EncryptableMetadata ToEncryptableMetadata(string value)
        {
            if (value.StartsWith(ReservedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Metadata value must not start with reserved prefix '{ReservedPrefix}'. " +
                    "Request validation should have rejected this input before reaching the encryption layer.");

            if (workspaceEncryptionSession is null)
                return new EncryptableMetadata(
                    Value: value,
                    EncryptionMode: NoMetadataEncryption.Instance);

            var latest = workspaceEncryptionSession.GetLatestDek();

            return new EncryptableMetadata(
                Value: value,
                EncryptionMode: new AesGcmV1MetadataEncryption(
                    Ikm: latest.Dek,
                    KeyVersion: (byte)latest.StorageDekVersion));
        }

        /// <summary>
        /// Direct shortcut equivalent to <c>session.ToEncryptableMetadata(value).Encode()</c>
        /// without constructing the intermediate <see cref="EncryptableMetadata"/> /
        /// <see cref="AesGcmV1MetadataEncryption"/> objects. Use this at call sites that
        /// only need the at-rest <see cref="EncodedMetadataValue"/> (e.g. audit log refs)
        /// and don't pass the value through the <c>EncryptableMetadata</c> abstraction.
        /// For SQLite parameter binding via <c>WithEncryptableParameter</c>, prefer
        /// <see cref="ToEncryptableMetadata"/> — the binder owns the encode call.
        /// </summary>
        public EncodedMetadataValue Encode(string value)
        {
            if (value.StartsWith(ReservedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Metadata value must not start with reserved prefix '{ReservedPrefix}'. " +
                    "Request validation should have rejected this input before reaching the encryption layer.");

            if (workspaceEncryptionSession is null)
                return new EncodedMetadataValue(value);

            var latest = workspaceEncryptionSession.GetLatestDek();

            return new EncodedMetadataValue(EncodeAesGcmV1(
                value: value,
                ikm: latest.Dek,
                keyVersion: (byte)latest.StorageDekVersion));
        }

        /// <summary>
        /// Inverse of <see cref="Encode"/>. Plaintext passthrough when the session is null
        /// (workspace has no full encryption). Otherwise parses the base64 envelope
        /// <c>[format(1) | key_version(1) | chain_steps_count(1) | N × step_salt(32) | nonce(12) | ciphertext | tag(16)]</c>,
        /// picks the matching Workspace DEK by key_version, walks the chain of step salts
        /// through HKDF to derive the final DEK, and AES-256-GCM decrypts into a UTF-8 string.
        ///
        /// <para>
        /// For <c>chain_steps_count == 0</c> the workspace DEK is used directly (today's
        /// workspace-scoped writes). For count &gt; 0 the envelope's step salts are fed into
        /// <see cref="KeyDerivationChain.Derive(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
        /// in order — the terminal DEK (future box scope) then decrypts the payload.
        /// </para>
        ///
        /// Throws on malformed input, unsupported format byte, or unknown key version —
        /// those indicate either corruption or a stale client key bundle and should not be
        /// silently recovered from.
        /// </summary>
        public string DecodeEncryptableMetadata(EncodedMetadataValue encoded)
            => workspaceEncryptionSession.DecodeEncryptableMetadata(encoded.Encoded);

        public string DecodeEncryptableMetadata(string encoded)
        {
            if (workspaceEncryptionSession is null)
                return encoded;

            if (!encoded.StartsWith(ReservedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Encrypted metadata value must start with reserved prefix '{ReservedPrefix}'.");

            var base64Length = encoded.Length - ReservedPrefix.Length;
            var maxEnvelopeLength = (base64Length / 4) * 3;
            var minEnvelopeLength = HeaderFixedSize + SymmetricAeadWrap.NonceSize + SymmetricAeadWrap.TagSize;

            if (maxEnvelopeLength < minEnvelopeLength)
                throw new InvalidOperationException(
                    $"Encrypted metadata envelope too short (decoded max {maxEnvelopeLength} bytes, need {minEnvelopeLength}).");

            var rentedEnvelope = maxEnvelopeLength > StackAllocThresholdBytes
                ? ArrayPool<byte>.Shared.Rent(maxEnvelopeLength)
                : null;

            try
            {
                var envelopeBuffer = rentedEnvelope is null
                    ? stackalloc byte[maxEnvelopeLength]
                    : rentedEnvelope.AsSpan(0, maxEnvelopeLength);

                var base64Span = encoded.AsSpan(ReservedPrefix.Length);

                if (!Convert.TryFromBase64Chars(base64Span, envelopeBuffer, out var envelopeLength))
                    throw new InvalidOperationException(
                        "Malformed base64 in encrypted metadata.");

                if (envelopeLength < minEnvelopeLength)
                    throw new InvalidOperationException(
                        $"Encrypted metadata envelope too short ({envelopeLength} bytes, need {minEnvelopeLength}).");

                var envelope = envelopeBuffer[..envelopeLength];

                if (envelope[0] != FormatTagAesGcmV1)
                    throw new InvalidOperationException(
                        $"Unsupported metadata encryption format '0x{envelope[0]:X2}'.");

                var keyVersion = envelope[1];
                var chainStepsCount = envelope[2];
                var saltsLength = chainStepsCount * StepSaltSize;
                var expectedMinLength = HeaderFixedSize + saltsLength + SymmetricAeadWrap.NonceSize + SymmetricAeadWrap.TagSize;

                if (envelopeLength < expectedMinLength)
                    throw new InvalidOperationException(
                        $"Encrypted metadata envelope too short for declared chain ({envelopeLength} bytes, need {expectedMinLength}).");

                var workspaceDek = workspaceEncryptionSession.GetDekForVersion(keyVersion);

                var nonceStart = HeaderFixedSize + saltsLength;
                var nonce = envelope.Slice(nonceStart, SymmetricAeadWrap.NonceSize);
                var ctStart = nonceStart + SymmetricAeadWrap.NonceSize;
                var plaintextLength = envelopeLength - ctStart - SymmetricAeadWrap.TagSize;
                var ciphertext = envelope.Slice(ctStart, plaintextLength);
                var tag = envelope.Slice(ctStart + plaintextLength, SymmetricAeadWrap.TagSize);

                // For chain_steps_count > 0, run HKDF over the declared salts to produce the
                // terminal DEK (future box scope). For count == 0 the workspace DEK decrypts
                // directly — it is owned by the session, so only the derived DEK gets disposed.
                var dek = chainStepsCount == 0
                    ? workspaceDek
                    : workspaceDek.Use(
                        state: new DeriveChainInput { ChainSalts = envelope.Slice(HeaderFixedSize, saltsLength) },
                        action: static (dekSpan, s) => KeyDerivationChain.Derive(dekSpan, s.ChainSalts));

                try
                {
                    var rentedPlaintext = plaintextLength > StackAllocThresholdBytes
                        ? ArrayPool<byte>.Shared.Rent(plaintextLength)
                        : null;

                    try
                    {
                        var plaintext = rentedPlaintext is null
                            ? stackalloc byte[plaintextLength]
                            : rentedPlaintext.AsSpan(0, plaintextLength);

                        dek.Use(
                            state: new DecryptState
                            {
                                Nonce = nonce,
                                Ciphertext = ciphertext,
                                Tag = tag,
                                Plaintext = plaintext
                            },
                            action: static (keySpan, s) =>
                            {
                                using var gcm = new AesGcm(keySpan, SymmetricAeadWrap.TagSize);

                                gcm.Decrypt(
                                    nonce: s.Nonce,
                                    ciphertext: s.Ciphertext,
                                    tag: s.Tag,
                                    plaintext: s.Plaintext);
                            });

                        return Encoding.UTF8.GetString(plaintext);
                    }
                    finally
                    {
                        if (rentedPlaintext is not null)
                            ArrayPool<byte>.Shared.Return(rentedPlaintext);
                    }
                }
                finally
                {
                    if (chainStepsCount > 0)
                        dek.Dispose();
                }
            }
            finally
            {
                if (rentedEnvelope is not null)
                    ArrayPool<byte>.Shared.Return(rentedEnvelope);
            }
        }
    }

    extension(EncryptableMetadata metadata)
    {
        /// <summary>
        /// Returns the on-wire / at-rest form of this metadata wrapped in
        /// <see cref="EncodedMetadataValue"/>:
        ///   - <see cref="NoMetadataEncryption"/>: the raw value verbatim.
        ///   - <see cref="AesGcmV1MetadataEncryption"/>: base64 of the encrypted envelope
        ///     prefixed with <see cref="ReservedPrefix"/>.
        /// Boundaries that need a plain <c>string</c> (SQLite TEXT parameter, JSON writer)
        /// extract it via <see cref="EncodedMetadataValue.Encoded"/>.
        /// </summary>
        public EncodedMetadataValue Encode()
        {
            var raw = metadata.EncryptionMode switch
            {
                NoMetadataEncryption => metadata.Value,

                AesGcmV1MetadataEncryption aes => EncodeAesGcmV1(
                    metadata.Value,
                    aes.Ikm,
                    aes.KeyVersion),

                _ => throw new InvalidOperationException(
                    $"Unsupported metadata encryption mode '{metadata.EncryptionMode.GetType().Name}'.")
            };

            return new EncodedMetadataValue(raw);
        }
    }

    private static string EncodeAesGcmV1(
        string value, 
        SecureBytes ikm, 
        byte keyVersion)
    {
        var utf8Length = Encoding.UTF8.GetByteCount(value);

        // Workspace-scoped path always writes chain_steps_count = 0 — no step salts.
        // When box scope arrives, this method (or a sibling) will write count = 1 with
        // a 32-byte box salt slice between the header and the nonce.
        const int chainStepsCount = 0;
        const int saltsLength = chainStepsCount * StepSaltSize;
        var envelopeLength = HeaderFixedSize + saltsLength + SymmetricAeadWrap.NonceSize + utf8Length + SymmetricAeadWrap.TagSize;

        // Envelope and plaintext are transient scratch. Stackalloc while small enough
        // (typical for names), ArrayPool when larger (comment / note bodies can be many KB).
        // AesGcm.Encrypt does not officially document support for overlapping plaintext /
        // ciphertext spans, so we keep them separate.
        var rentedEnvelope = envelopeLength > StackAllocThresholdBytes
            ? ArrayPool<byte>.Shared.Rent(envelopeLength)
            : null;

        var rentedPlaintext = utf8Length > StackAllocThresholdBytes
            ? ArrayPool<byte>.Shared.Rent(utf8Length)
            : null;

        try
        {
            var envelope = rentedEnvelope is null
                ? stackalloc byte[envelopeLength]
                : rentedEnvelope.AsSpan(0, envelopeLength);

            envelope[0] = FormatTagAesGcmV1;
            envelope[1] = keyVersion;
            envelope[2] = chainStepsCount;

            var nonceStart = HeaderFixedSize + saltsLength;
            var nonceSpan = envelope.Slice(nonceStart, SymmetricAeadWrap.NonceSize);
            RandomNumberGenerator.Fill(nonceSpan);

            var ctStart = nonceStart + SymmetricAeadWrap.NonceSize;
            var ctSpan = envelope.Slice(ctStart, utf8Length);
            var tagSpan = envelope.Slice(ctStart + utf8Length, SymmetricAeadWrap.TagSize);

            var plaintext = rentedPlaintext is null
                ? stackalloc byte[utf8Length]
                : rentedPlaintext.AsSpan(0, utf8Length);

            Encoding.UTF8.GetBytes(value, plaintext);

            ikm.Use(
                state: new EncryptState
                {
                    Plaintext = plaintext,
                    Nonce = nonceSpan,
                    Ciphertext = ctSpan,
                    Tag = tagSpan
                },
                action: static (keySpan, s) =>
                {
                    using var gcm = new AesGcm(keySpan, SymmetricAeadWrap.TagSize);
                    gcm.Encrypt(
                        nonce: s.Nonce,
                        plaintext: s.Plaintext,
                        ciphertext: s.Ciphertext,
                        tag: s.Tag);
                });

            return string.Concat(ReservedPrefix, Convert.ToBase64String(envelope));
        }
        finally
        {
            if (rentedEnvelope is not null)
                ArrayPool<byte>.Shared.Return(rentedEnvelope);

            if (rentedPlaintext is not null)
                ArrayPool<byte>.Shared.Return(rentedPlaintext);
        }
    }

    private readonly ref struct EncryptState
    {
        public required ReadOnlySpan<byte> Plaintext { get; init; }
        public required ReadOnlySpan<byte> Nonce { get; init; }
        public required Span<byte> Ciphertext { get; init; }
        public required Span<byte> Tag { get; init; }
    }

    private readonly ref struct DecryptState
    {
        public required ReadOnlySpan<byte> Nonce { get; init; }
        public required ReadOnlySpan<byte> Ciphertext { get; init; }
        public required ReadOnlySpan<byte> Tag { get; init; }
        public required Span<byte> Plaintext { get; init; }
    }

    private readonly ref struct DeriveChainInput
    {
        public required ReadOnlySpan<byte> ChainSalts { get; init; }
    }
}