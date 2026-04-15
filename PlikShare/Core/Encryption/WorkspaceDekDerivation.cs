using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Derives a per-workspace DEK from its parent Storage DEK and the workspace's persisted salt
/// (<c>w_workspaces.w_encryption_salt</c>). A single HKDF-SHA256 step with the workspace salt
/// means any two sibling workspaces on the same storage produce independent DEKs — a team
/// member who only holds one workspace's DEK cannot decrypt files in another workspace on the
/// same storage, even though both share the same underlying Storage DEK.
///
/// <b>Info parameter is intentionally empty</b> so this derivation is identical to a single
/// step of <see cref="KeyDerivationChain.Derive"/>. That equivalence is load-bearing: the V2
/// file-header's chain salts let offline recovery walk the chain from Storage DEK straight
/// down to the file key without knowing about any workspace-specific domain labels. If this
/// helper used a non-empty <c>info</c>, the file format would need to carry it too.
///
/// Derivation is deterministic: the same (storageDek, workspaceSalt) always yields the same
/// Workspace DEK, which is what lets offline recovery reconstruct a workspace's DEK from the
/// recovery seed plus the per-workspace salt.
/// </summary>
public static class WorkspaceDekDerivation
{
    public const int DekSize = 32;
    public const int SaltSize = 32;

    public static byte[] Derive(
        ReadOnlySpan<byte> storageDek,
        ReadOnlySpan<byte> workspaceSalt)
    {
        if (storageDek.Length != DekSize)
            throw new ArgumentException(
                $"Storage DEK must be {DekSize} bytes, got {storageDek.Length}.",
                nameof(storageDek));

        if (workspaceSalt.Length != SaltSize)
            throw new ArgumentException(
                $"Workspace salt must be {SaltSize} bytes, got {workspaceSalt.Length}.",
                nameof(workspaceSalt));

        var dek = new byte[DekSize];

        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: storageDek,
            output: dek,
            salt: workspaceSalt,
            info: []);

        return dek;
    }
}
