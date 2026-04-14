using PlikShare.Files.Records;
using PlikShare.Uploads.Cache;
using System.ComponentModel;

namespace PlikShare.Core.Encryption;

[ImmutableObject(true)]
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

public static class FileEncryptionMetadataExtensions
{
    extension(FileEncryptionMetadata? metadata)
    {
        public int CalculateBufferSize(FilePart part)
        {
            if (metadata is null)
                return part.SizeInBytes;

            if (metadata.FormatVersion == 1)
                return Aes256GcmStreamingV1.CalculateEncryptedPartSize(
                    part);

            if (metadata.FormatVersion == 2)
                return Aes256GcmStreamingV2.CalculateEncryptedPartSize(
                    part,
                    metadata.ChainStepSalts.Count);

            throw new InvalidOperationException(
                $"Unsupported file encryption format version '{metadata.FormatVersion}'.");
        }
    }
}