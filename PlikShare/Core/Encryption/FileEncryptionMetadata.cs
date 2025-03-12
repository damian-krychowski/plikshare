using System.ComponentModel;

namespace PlikShare.Core.Encryption;

[ImmutableObject(true)]
public sealed class FileEncryptionMetadata
{
    public required byte KeyVersion { get; init; }
    public required byte[] Salt { get; init; }
    public required byte[] NoncePrefix { get; init; }
}