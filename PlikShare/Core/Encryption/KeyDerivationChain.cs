using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Walks a sequence of HKDF derivation steps from a starting DEK down to a terminal DEK.
/// Each step takes the previous DEK as IKM and a step salt (32 bytes) as salt; info is empty.
/// The chain is serialized to/from the DB as concatenated 32-byte salts, allowing recovery
/// from the file header alone.
/// </summary>
public static class KeyDerivationChain
{
    public const int StepSaltSize = 32;
    public const int DerivedKeySize = 32;

    public static SecureBytes Derive(
        ReadOnlySpan<byte> startingDek,
        IReadOnlyList<byte[]> stepSalts)
    {
        if (startingDek.Length != DerivedKeySize)
            throw new ArgumentException(
                $"Starting DEK must be {DerivedKeySize} bytes, got {startingDek.Length}.",
                nameof(startingDek));

        foreach (var salt in stepSalts)
        {
            if (salt.Length != StepSaltSize)
                throw new ArgumentException(
                    $"Each step salt must be {StepSaltSize} bytes, got {salt.Length}.",
                    nameof(stepSalts));
        }

        return SecureBytes.Create(
            length: DerivedKeySize,
            state: new ListChainInput
            {
                StartingDek = startingDek,
                StepSalts = stepSalts
            },
            initializer: static (output, state) =>
            {
                if (state.StepSalts.Count == 0)
                {
                    state.StartingDek.CopyTo(output);
                    return;
                }

                Span<byte> current = stackalloc byte[DerivedKeySize];
                Span<byte> next = stackalloc byte[DerivedKeySize];

                state.StartingDek.CopyTo(current);

                try
                {
                    var lastIndex = state.StepSalts.Count - 1;

                    for (var i = 0; i < lastIndex; i++)
                    {
                        HKDF.DeriveKey(
                            hashAlgorithmName: HashAlgorithmName.SHA256,
                            ikm: current,
                            output: next,
                            salt: state.StepSalts[i],
                            info: []);

                        next.CopyTo(current);
                    }

                    HKDF.DeriveKey(
                        hashAlgorithmName: HashAlgorithmName.SHA256,
                        ikm: current,
                        output: output,
                        salt: state.StepSalts[lastIndex],
                        info: []);
                }
                finally
                {
                    current.Clear();
                    next.Clear();
                }
            });
    }

    private readonly ref struct ListChainInput
    {
        public required ReadOnlySpan<byte> StartingDek { get; init; }
        public required IReadOnlyList<byte[]> StepSalts { get; init; }
    }

    /// <summary>
    /// Span-based variant returning the terminal DEK as a <see cref="SecureBytes"/> (pinned,
    /// mlocked, zeroed on dispose). <paramref name="chainSalts"/> carries the salts as a single contiguous span of
    /// <c>N * StepSaltSize</c> bytes — the layout used by metadata envelopes and file frame
    /// headers. When <paramref name="chainSalts"/> is empty, the starting DEK is copied
    /// through unchanged into a fresh <see cref="SecureBytes"/>.
    /// </summary>
    public static SecureBytes Derive(
        ReadOnlySpan<byte> startingDek,
        ReadOnlySpan<byte> chainSalts)
    {
        if (startingDek.Length != DerivedKeySize)
            throw new ArgumentException(
                $"Starting DEK must be {DerivedKeySize} bytes, got {startingDek.Length}.",
                nameof(startingDek));

        if (chainSalts.Length % StepSaltSize != 0)
            throw new ArgumentException(
                $"Chain salts length {chainSalts.Length} is not a multiple of {StepSaltSize}.",
                nameof(chainSalts));

        return SecureBytes.Create(
            length: DerivedKeySize,
            state: new ChainInput
            {
                StartingDek = startingDek,
                ChainSalts = chainSalts
            },
            initializer: static (output, state) =>
            {
                if (state.ChainSalts.IsEmpty)
                {
                    state.StartingDek.CopyTo(output);
                    return;
                }

                var stepCount = state.ChainSalts.Length / StepSaltSize;

                scoped Span<byte> current = stackalloc byte[DerivedKeySize];
                scoped Span<byte> next = stackalloc byte[DerivedKeySize];

                state.StartingDek.CopyTo(current);

                try
                {
                    for (var i = 0; i < stepCount - 1; i++)
                    {
                        HKDF.DeriveKey(
                            hashAlgorithmName: HashAlgorithmName.SHA256,
                            ikm: current,
                            output: next,
                            salt: state.ChainSalts.Slice(i * StepSaltSize, StepSaltSize),
                            info: []);

                        next.CopyTo(current);
                    }

                    HKDF.DeriveKey(
                        hashAlgorithmName: HashAlgorithmName.SHA256,
                        ikm: current,
                        output: output,
                        salt: state.ChainSalts.Slice((stepCount - 1) * StepSaltSize, StepSaltSize),
                        info: []);
                }
                finally
                {
                    current.Clear();
                    next.Clear();
                }
            });
    }

    private readonly ref struct ChainInput
    {
        public required ReadOnlySpan<byte> StartingDek { get; init; }
        public required ReadOnlySpan<byte> ChainSalts { get; init; }
    }

    public static byte[]? Serialize(IReadOnlyList<byte[]>? stepSalts)
    {
        if (stepSalts is null)
            return null;

        if (stepSalts.Count == 0)
            return null;

        var output = new byte[stepSalts.Count * StepSaltSize];
        var offset = 0;

        foreach (var salt in stepSalts)
        {
            if (salt.Length != StepSaltSize)
                throw new ArgumentException(
                    $"Each step salt must be {StepSaltSize} bytes, got {salt.Length}.",
                    nameof(stepSalts));

            salt.CopyTo(output, offset);
            offset += StepSaltSize;
        }

        return output;
    }

    public static IReadOnlyList<byte[]> Deserialize(byte[]? serialized)
    {
        if (serialized is null || serialized.Length == 0)
            return [];

        if (serialized.Length % StepSaltSize != 0)
            throw new InvalidOperationException(
                $"Serialized chain salts length {serialized.Length} is not a multiple of {StepSaltSize}.");

        var stepCount = serialized.Length / StepSaltSize;
        var output = new byte[stepCount][];

        for (var i = 0; i < stepCount; i++)
        {
            output[i] = new byte[StepSaltSize];
            Array.Copy(serialized, i * StepSaltSize, output[i], 0, StepSaltSize);
        }

        return output;
    }
}
