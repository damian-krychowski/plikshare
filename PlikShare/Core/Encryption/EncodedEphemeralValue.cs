namespace PlikShare.Core.Encryption;

public readonly record struct EncodedEphemeralValue
{
    public string Encoded { get; }

    public EncodedEphemeralValue(string encoded)
    {
        ArgumentException.ThrowIfNullOrEmpty(encoded);

        if (!encoded.StartsWith(EphemeralKeyRing.ReservedPrefix, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Ephemeral value must start with reserved prefix '{EphemeralKeyRing.ReservedPrefix}'.",
                nameof(encoded));

        Encoded = encoded;
    }

    public override string ToString() => "[ephemeral]";
}
