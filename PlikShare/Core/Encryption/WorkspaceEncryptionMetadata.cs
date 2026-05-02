namespace PlikShare.Core.Encryption;

public sealed class WorkspaceEncryptionMetadata
{
    public required byte[] Salt { get; init; }
}
