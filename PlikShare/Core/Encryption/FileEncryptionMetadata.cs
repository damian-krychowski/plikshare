namespace PlikShare.Core.Encryption;

public sealed class FileEncryptionMetadata
{
    public required byte FormatVersion { get; init; }
    public required byte KeyVersion { get; init; }
    public required byte[] Salt { get; init; }
    public required byte[] NoncePrefix { get; init; }

    /// <summary>
    /// Sequence of 32-byte salts that derive intermediate DEKs from the storage DEK
    /// down to the DEK used to derive this file's <see cref="Salt"/>-based file_key.
    /// Empty for legacy V1 files which derive file_key directly from the storage DEK.
    /// First step is typically the workspace salt; subsequent steps add box, user or link
    /// scope as the schema evolves.
    /// </summary>
    public required IReadOnlyList<byte[]> ChainStepSalts { get; init; }
}