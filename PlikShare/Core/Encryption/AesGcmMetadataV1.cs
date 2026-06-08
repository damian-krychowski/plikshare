using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Turns a plaintext string into an <see cref="EncodedMetadataValue"/> and encodes the
/// payload to the string the SQLite parameter binder consumes.
///
/// Mode resolution:
///   - no workspace encryption → plaintext: <see cref="Encode"/> returns the raw string.
///   - workspace encryption present → AES-256-GCM using the latest Workspace DEK version;
///     <see cref="Encode"/> generates a fresh 32-byte chain salt and a fresh nonce, derives
///     a per-value metadata DEK via <c>HKDF(workspaceDek, chainSalt)</c>, AES-256-GCM
///     encrypts the value, and returns <see cref="ReservedPrefix"/> followed by base64 of
///     the envelope:
///     <c>[format(1) | key_version(1) | chain_steps_count(1) | N × step_salt(32) | nonce(12) | ciphertext | tag(16)]</c>.
///
/// <para>
/// <b>Chain steps</b>: each step records a 32-byte salt that derives a sub-DEK via HKDF
/// from the parent DEK. Every encode writes <c>chain_steps_count = 1</c> with a fresh
/// random salt, so each encrypted value has its own derived metadata DEK, never the
/// workspace DEK directly. Decode parses the count and walks the salts in order.
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
public static class AesGcmMetadataV1
{
    /// <summary>
    /// Namespace marker prepended to every encrypted metadata string bound to SQLite.
    /// A value stored in a metadata column starts with <see cref="ReservedPrefix"/> if
    /// and only if it is an encrypted envelope. User-supplied plaintext metadata must
    /// NOT start with this prefix — request validation rejects such input so the prefix
    /// remains an unambiguous format signal for tooling, db dumps, and decode routines.
    /// </summary>
    public const string ReservedPrefix = "pse:";

    private const byte FormatTagAesGcmV1 = 0x01;
    private const int StackAllocThresholdBytes = 512;
    private const int HeaderFixedSize = 3; // format + key_version + chain_steps_count

    /// <summary>
    /// Inverse of <see cref="Encode"/>. Plaintext passthrough when the session is null
    /// (workspace has no full encryption). Otherwise parses the base64 envelope
    /// <c>[format(1) | key_version(1) | chain_steps_count(1) | N × step_salt(32) | nonce(12) | ciphertext | tag(16)]</c>,
    /// picks the matching Workspace DEK by key_version, walks the chain salts through
    /// <see cref="KeyDerivationChain.Derive(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte})"/>
    /// to produce the terminal DEK, and AES-256-GCM decrypts into a UTF-8 string.
    ///
    /// Throws on malformed input, unsupported format byte, or unknown key version —
    /// those indicate either corruption or a stale client key bundle and should not be
    /// silently recovered from.
    /// </summary>
    public static string Decode(
        string encoded,
        WorkspaceEncryptionSession workspaceEncryptionSession)
    {
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
            var saltsLength = chainStepsCount * KeyDerivationChain.StepSaltSize;
            var expectedMinLength = HeaderFixedSize + saltsLength + SymmetricAeadWrap.NonceSize + SymmetricAeadWrap.TagSize;

            if (envelopeLength < expectedMinLength)
                throw new InvalidOperationException(
                    $"Encrypted metadata envelope too short for declared chain ({envelopeLength} bytes, need {expectedMinLength}).");

            var workspaceDek = workspaceEncryptionSession.GetDekForVersion(keyVersion);

            var nonceStart = HeaderFixedSize + saltsLength;
            var ctStart = nonceStart + SymmetricAeadWrap.NonceSize;
            var plaintextLength = envelopeLength - ctStart - SymmetricAeadWrap.TagSize;
            var tagStart = ctStart + plaintextLength;

            var rentedPlaintext = plaintextLength > StackAllocThresholdBytes
                ? ArrayPool<byte>.Shared.Rent(plaintextLength)
                : null;

            try
            {
                var plaintext = rentedPlaintext is null
                    ? stackalloc byte[plaintextLength]
                    : rentedPlaintext.AsSpan(0, plaintextLength);

                workspaceDek.Use(
                    state: new DecryptState
                    {
                        Nonce = envelope.Slice(nonceStart, SymmetricAeadWrap.NonceSize),
                        Ciphertext = envelope.Slice(ctStart, plaintextLength),
                        Tag = envelope.Slice(tagStart, SymmetricAeadWrap.TagSize),

                        ChainSalts = chainStepsCount == 0
                            ? []
                            : envelope.Slice(HeaderFixedSize, saltsLength),

                        Plaintext = plaintext
                    },
                    action: static (keySpan, state) =>
                    {
                        Span<byte> derivedKey = stackalloc byte[KeyDerivationChain.DerivedKeySize];

                        try
                        {
                            KeyDerivationChain.Derive(
                                startingDek: keySpan,
                                chainSalts: state.ChainSalts,
                                output: derivedKey);

                            using var gcm = new AesGcm(
                                derivedKey,
                                SymmetricAeadWrap.TagSize);

                            gcm.Decrypt(
                                nonce: state.Nonce,
                                ciphertext: state.Ciphertext,
                                tag: state.Tag,
                                plaintext: state.Plaintext);
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(derivedKey);
                        }
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
            if (rentedEnvelope is not null)
                ArrayPool<byte>.Shared.Return(rentedEnvelope);
        }
    }

    public static EncodedMetadataValue Encode(
        string value,
        byte keyVersion,
        ReadOnlySpan<byte> metadataKey,
        IReadOnlyList<byte[]> chainStepSalts)
    {
        var saltsLength = chainStepSalts.Aggregate(
            seed: 0,
            func: (length, bytes) => length + bytes.Length);

        var rentedSalts = saltsLength > StackAllocThresholdBytes
            ? ArrayPool<byte>.Shared.Rent(saltsLength)
            : null;

        try
        {
            var saltsBuffer = rentedSalts is null
                ? stackalloc byte[saltsLength]
                : rentedSalts.AsSpan(0, saltsLength);

            var saltsOffset = 0;
            foreach (var salt in chainStepSalts)
            {
                salt.CopyTo(saltsBuffer.Slice(saltsOffset, salt.Length));
                saltsOffset += salt.Length;
            }

            return Encode(
                value: value,
                keyVersion: keyVersion,
                metadataKey: metadataKey,
                chainStepSalts: saltsBuffer);
        }
        finally
        {
            if (rentedSalts is not null)
                ArrayPool<byte>.Shared.Return(rentedSalts);
        }
    }

    public static EncodedMetadataValue Encode(
        string value,
        byte keyVersion,
        ReadOnlySpan<byte> metadataKey,
        ReadOnlySpan<byte> chainStepSalts)
    {
        ThrowIfMetadataKeyZeroed(metadataKey);

        if (chainStepSalts.Length == 0)
            throw new ArgumentException(
                "Chain step salts must contain at least one salt — encrypting with zero chain " +
                "steps would derive directly from the workspace DEK and defeat per-value key derivation.",
                nameof(chainStepSalts));

        if (chainStepSalts.Length % KeyDerivationChain.StepSaltSize != 0)
            throw new ArgumentException(
                $"Chain step salts length ({chainStepSalts.Length} bytes) must be a multiple of " +
                $"{KeyDerivationChain.StepSaltSize} (StepSaltSize).",
                nameof(chainStepSalts));

        var chainStepsCount = chainStepSalts.Length / KeyDerivationChain.StepSaltSize;

        if (chainStepsCount > byte.MaxValue)
            throw new ArgumentException(
                $"Chain step count ({chainStepsCount}) exceeds the maximum of {byte.MaxValue}.",
                nameof(chainStepSalts));

        var utf8Length = Encoding.UTF8.GetByteCount(value);

        var saltsLength = chainStepSalts.Length;

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
            envelope[2] = (byte)chainStepsCount;

            chainStepSalts.CopyTo(envelope.Slice(HeaderFixedSize, saltsLength));

            var nonceStart = HeaderFixedSize + saltsLength;
            var nonceSpan = envelope.Slice(nonceStart, SymmetricAeadWrap.NonceSize);
            RandomNumberGenerator.Fill(nonceSpan);

            var ctStart = nonceStart + SymmetricAeadWrap.NonceSize;
            var ciphertextSpan = envelope.Slice(ctStart, utf8Length);
            var tagSpan = envelope.Slice(ctStart + utf8Length, SymmetricAeadWrap.TagSize);

            var plaintext = rentedPlaintext is null
                ? stackalloc byte[utf8Length]
                : rentedPlaintext.AsSpan(0, utf8Length);

            Encoding.UTF8.GetBytes(value, plaintext);

            using var gcm = new AesGcm(
                key: metadataKey,
                tagSizeInBytes: SymmetricAeadWrap.TagSize);

            gcm.Encrypt(
                nonce: nonceSpan,
                plaintext: plaintext,
                ciphertext: ciphertextSpan,
                tag: tagSpan);

            var encodedValue = string.Concat(
                ReservedPrefix,
                Convert.ToBase64String(envelope));

            return new EncodedMetadataValue(encodedValue);
        }
        finally
        {
            if (rentedEnvelope is not null)
                ArrayPool<byte>.Shared.Return(rentedEnvelope);

            if (rentedPlaintext is not null)
                ArrayPool<byte>.Shared.Return(rentedPlaintext);
        }
    }

    private static void ThrowIfMetadataKeyZeroed(ReadOnlySpan<byte> metadataKey)
    {
        for (var i = 0; i < metadataKey.Length; i++)
        {
            if (metadataKey[i] != 0)
                return;
        }

        throw new ObjectDisposedException(
            objectName: nameof(metadataKey),
            message: "MetadataKey is all zeros — this metadataKey has already been consumed by an encode call.");
    }

    private readonly ref struct DecryptState
    {
        public required ReadOnlySpan<byte> Nonce { get; init; }
        public required ReadOnlySpan<byte> Ciphertext { get; init; }
        public required ReadOnlySpan<byte> Tag { get; init; }
        public required ReadOnlySpan<byte> ChainSalts { get; init; }
        public required Span<byte> Plaintext { get; init; }
    }
}