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

    public static byte[] Derive(
        ReadOnlySpan<byte> startingDek,
        IReadOnlyList<byte[]> stepSalts)
    {
        if (startingDek.Length != DerivedKeySize)
            throw new ArgumentException(
                $"Starting DEK must be {DerivedKeySize} bytes, got {startingDek.Length}.",
                nameof(startingDek));

        if (stepSalts.Count == 0)
            return startingDek.ToArray();

        Span<byte> current = stackalloc byte[DerivedKeySize];
        startingDek.CopyTo(current);

        Span<byte> next = stackalloc byte[DerivedKeySize];

        foreach (var salt in stepSalts)
        {
            if (salt.Length != StepSaltSize)
                throw new ArgumentException(
                    $"Each step salt must be {StepSaltSize} bytes, got {salt.Length}.",
                    nameof(stepSalts));

            HKDF.DeriveKey(
                hashAlgorithmName: HashAlgorithmName.SHA256,
                ikm: current,
                output: next,
                salt: salt,
                info: []);

            next.CopyTo(current);
        }

        return current.ToArray();
    }

    public static byte[]? Serialize(IReadOnlyList<byte[]> stepSalts)
    {
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
