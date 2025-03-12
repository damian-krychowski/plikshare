using System.ComponentModel;
using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;

namespace PlikShare.Storages;

[ImmutableObject(true)]
public sealed class FileEncryption
{
    public required StorageEncryptionType EncryptionType { get; init; }
    public FileEncryptionMetadata? Metadata { get; init; }
};