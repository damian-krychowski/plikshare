using System.ComponentModel;

namespace PlikShare.Core.Encryption;

[ImmutableObject(true)]
public sealed class WorkspaceEncryptionMetadata
{
    public required byte[] Salt { get; init; }
}
