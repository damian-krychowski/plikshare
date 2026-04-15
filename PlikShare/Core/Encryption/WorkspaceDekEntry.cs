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
    public required byte[] Dek { get; init; }
}
