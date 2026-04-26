namespace PlikShare.Core.Encryption;

/// <summary>
/// A single unwrapped Workspace DEK together with the Storage DEK version it was derived
/// from. The Workspace DEK is derived deterministically from a specific version of the
/// Storage DEK combined with the workspace salt (HKDF), so the pair
/// (<see cref="StorageDekVersion"/>, workspace salt) fully identifies which key material
/// produced this DEK. Files encrypted before a Storage DEK rotation carry the older
/// <see cref="StorageDekVersion"/> in their V2 header; after rotation there may be several
/// entries per workspace-member, one per Storage DEK version still referenced by existing
/// files.
///
/// The owning workspace id is held once on the enclosing
/// <see cref="WorkspaceEncryptionSession.WorkspaceId"/>. The workspace salt is stored on
/// <c>w_workspaces.w_encryption_salt</c> and read through <see cref="Workspaces.Cache.WorkspaceCache"/>
/// at the call sites that need it (file-header chain steps); it is intentionally not
/// duplicated here.
/// </summary>
public sealed class WorkspaceDekEntry: IDisposable
{
    public required int StorageDekVersion { get; init; }
    public required SecureBytes Dek { get; init; }

    public void Dispose()
    {
        Dek.Dispose();
    }
}

public sealed class WorkspaceDekEntryWire
{
    public required int WorkspaceId { get; init; }
    public required int StorageDekVersion { get; init; }

    /// <summary>
    /// The Workspace DEK encrypted with <see cref="IMasterDataEncryption.EncryptBytes"/>.
    /// Serializable without exposing plaintext: the raw DEK bytes never materialize as
    /// a byte[] on the managed heap during encode/decode.
    /// </summary>
    public required byte[] EncryptedDek { get; init; }
}

public static class WorkspaceDekEntryWireExtensions
{
    extension(WorkspaceEncryptionSession? session)
    {
        public WorkspaceDekEntryWire[] ToWires(
            IMasterDataEncryption masterEncryption)
        {
            if (session is null)
                return [];

            var entries = session.Entries;
            var wires = new WorkspaceDekEntryWire[entries.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                wires[i] = new WorkspaceDekEntryWire
                {
                    WorkspaceId = session.WorkspaceId,
                    StorageDekVersion = entries[i].StorageDekVersion,
                    EncryptedDek = entries[i].Dek.ToWire(masterEncryption)
                };
            }

            return wires;
        }
    }

    extension(WorkspaceDekEntryWire wire)
    {
        public WorkspaceDekEntry ToEntry(
            IMasterDataEncryption masterEncryption)
        {
            return new WorkspaceDekEntry
            {
                StorageDekVersion = wire.StorageDekVersion,

                Dek = SecureBytes.FromWire(
                    encryptedSecureBytes: wire.EncryptedDek,
                    masterEncryption: masterEncryption)
            };
        }
    }

    extension(IEnumerable<WorkspaceDekEntryWire> wires)
    {
        public WorkspaceEncryptionSession ToSession(
            IMasterDataEncryption masterEncryption)
        {
            var wireArray = wires as WorkspaceDekEntryWire[] ?? wires.ToArray();

            if (wireArray.Length == 0)
                throw new InvalidOperationException(
                    "Cannot build WorkspaceEncryptionSession from an empty WorkspaceDekEntryWire collection.");

            var workspaceId = wireArray[0].WorkspaceId;

            for (var i = 1; i < wireArray.Length; i++)
            {
                if (wireArray[i].WorkspaceId != workspaceId)
                    throw new InvalidOperationException(
                        $"Cannot build WorkspaceEncryptionSession: entries belong to different workspaces " +
                        $"(found {workspaceId} and {wireArray[i].WorkspaceId}).");
            }
            
            return new WorkspaceEncryptionSession(
                workspaceId: workspaceId,
                entries: wireArray
                    .Select(wire => wire.ToEntry(masterEncryption))
                    .ToArray());
        }
    }
}
