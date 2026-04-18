namespace PlikShare.Core.Encryption;

/// <summary>
/// A single unwrapped Workspace DEK together with the Storage DEK version it was derived
/// from and the workspace salt that went into the HKDF step producing it. Files encrypted
/// before a rotation carry the older <see cref="StorageDekVersion"/> in their V2 header;
/// after rotation there may be several entries per workspace-member.
///
/// <see cref="Salt"/> is the same value for every entry belonging to one workspace (stored
/// once on <c>w_workspaces.w_encryption_salt</c>) — it is copied onto each entry so the
/// record is self-contained for audit, re-derivation, and presigned-URL payloads that
/// should not require a separate workspace-row lookup to reconstruct the chain.
///
/// Used both inside <see cref="WorkspaceEncryptionSession"/> (one entry per wrap row the
/// caller holds) and inside the DataProtection-sealed presigned-URL payload (copied over
/// so unauthenticated direct transfers can still pick the right DEK per file).
/// </summary>
public sealed class WorkspaceDekEntry
{
    public required int StorageDekVersion { get; init; }
    public required byte[] Salt { get; init; }
    public required SecureBytes Dek { get; init; }
}

public sealed class WorkspaceDekEntryWire
{
    public required int StorageDekVersion { get; init; }
    public required byte[] Salt { get; init; }

    /// <summary>
    /// The Workspace DEK encrypted with <see cref="IMasterDataEncryption.EncryptBytes"/>.
    /// Serializable without exposing plaintext: the raw DEK bytes never materialize as
    /// a byte[] on the managed heap during encode/decode.
    /// </summary>
    public required byte[] EncryptedDek { get; init; }
}

public static class WorkspaceDekEntryWireExtensions
{
    /// <summary>
    /// Converts an in-memory entry into its wire form. The DEK plaintext is read
    /// only inside the pinned SecureBytes buffer — it is encrypted by
    /// <paramref name="masterEncryption"/> directly from that span, never copied
    /// to an unpinned heap byte[].
    /// </summary>
    public static WorkspaceDekEntryWire ToWire(
        this WorkspaceDekEntry entry,
        IMasterDataEncryption masterEncryption)
    {
        var encryptedDek = entry.Dek.Use(
            masterEncryption,
            static (span, enc) => enc.FastEncryptBytes(span));

        return new WorkspaceDekEntryWire
        {
            StorageDekVersion = entry.StorageDekVersion,
            Salt = entry.Salt,
            EncryptedDek = encryptedDek
        };
    }

    public static WorkspaceDekEntryWire[] ToWires(
        this IEnumerable<WorkspaceDekEntry> entries,
        IMasterDataEncryption masterEncryption)
    {
        return entries
            .Select(entry => entry.ToWire(masterEncryption))
            .ToArray();
    }

    /// <summary>
    /// Converts a wire entry back into its in-memory form. The DEK plaintext is
    /// written directly into a freshly allocated pinned SecureBytes buffer —
    /// AesGcm.Decrypt writes straight into the pinned memory, so plaintext never
    /// lands on the unpinned heap.
    /// </summary>
    public static WorkspaceDekEntry ToEntry(
        this WorkspaceDekEntryWire wire,
        IMasterDataEncryption masterEncryption)
    {
        var plaintextLength = masterEncryption.GetFastDecryptedLength(wire.EncryptedDek);

        var dek = SecureBytes.Create(
            length: plaintextLength,
            state: new DecryptState
            {
                Encryption = masterEncryption,
                EncryptedDek = wire.EncryptedDek
            },
            initializer: static (output, s) =>
                s.Encryption.FastDecryptBytes(s.EncryptedDek, output));

        return new WorkspaceDekEntry
        {
            StorageDekVersion = wire.StorageDekVersion,
            Salt = wire.Salt,
            Dek = dek
        };
    }

    public static WorkspaceDekEntry[] ToEntries(
        this IEnumerable<WorkspaceDekEntryWire> wires,
        IMasterDataEncryption masterEncryption)
    {
        return wires
            .Select(wire => wire.ToEntry(masterEncryption))
            .ToArray();
    }

    private readonly ref struct DecryptState
    {
        public required IMasterDataEncryption Encryption { get; init; }
        public required byte[] EncryptedDek { get; init; }
    }
}