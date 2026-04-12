namespace PlikShare.Storages.Encryption;

public class EncryptionKeyProvider
{
    public ManagedEncryptionKeyProvider? Managed { get; init; }
    public FullEncryptionKeyProvider? Full { get; init; }

    public byte GetLatestKeyVersion()
    {
        if (Managed is not null)
            return Managed.GetLatestKeyVersion();

        if (Full is not null)
            return Full.GetLatestKeyVersion();

        throw new InvalidOperationException(
            "Cannot determine latest key version because neither Managed nor Full encryption key provider is configured.");
    }
}