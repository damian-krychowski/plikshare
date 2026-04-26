namespace PlikShare.Core.Encryption;

/// <summary>
/// A single unwrapped Storage DEK together with its version. The Storage DEK is the root
/// per-storage key material from which all Workspace DEKs in that storage are derived
/// (HKDF over Storage DEK + workspace salt). Storage DEK v0 is itself deterministically
/// derived from the storage's recovery seed via <see cref="StorageDekDerivation.DeriveDek"/>,
/// so the recovery code alone — without the database — is sufficient to reconstruct v0
/// and, transitively, every Workspace DEK that depends on it.
///
/// After a Storage DEK rotation, newer versions coexist with older ones: existing files
/// keep referencing the <see cref="DekVersion"/> they were encrypted under (recorded in the
/// V2 file header and propagated to <see cref="WorkspaceDekEntry.StorageDekVersion"/>),
/// while new files use the latest version. Each active version is therefore kept available
/// for unwrapping until no file references it anymore.
/// </summary>
public sealed class StorageDekEntry: IDisposable
{
    public required int DekVersion { get; init; }
    public required SecureBytes Dek { get; init; }

    public void Dispose()
    {
        Dek.Dispose();
    }
}

public static class StorageDekEntryExtensions
{
    extension(StorageDekEntry storageDekEntry)
    {
        public WorkspaceDekEntry DeriveWorkspaceDek(IReadOnlyList<byte[]> workspaceDekSalts)
        {
            return new WorkspaceDekEntry
            {
                Dek = storageDekEntry.Dek.Use(span => KeyDerivationChain.Derive(
                    startingDek: span,
                    stepSalts: workspaceDekSalts)),

                StorageDekVersion = storageDekEntry.DekVersion
            };
        }
    }
}