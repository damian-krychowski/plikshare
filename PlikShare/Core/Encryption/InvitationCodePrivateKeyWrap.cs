using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Wraps an ephemeral X25519 private key with a symmetric KEK derived from invitation-code
/// entropy bytes. Used at invite-time to stage a transient user keypair for a brand-new
/// invitee whose real encryption identity does not yet exist: the owner (unlocked) triggers
/// the generation of the ephemeral keypair, persists the public key in plain and the
/// private key under this wrap. Subsequent invitations from any owner can seal new
/// Workspace DEKs to the shared ephemeral public key without ever touching the invitation
/// code again. At encryption-password setup the invitee supplies the invitation code,
/// we unwrap the ephemeral private key, open every <c>ewek_*</c> sealed DEK, and re-seal
/// with the user's just-generated real public key.
///
/// Callers pass the raw invitation-code entropy (decoded from whatever transport
/// representation they use — Base62 is a transport detail that stops at the boundary).
/// HKDF-SHA256 derives a 32-byte KEK from those bytes; no slow KDF because invitation
/// codes are expected to carry full-entropy random bytes.
///
/// The derived KEK never leaves a <see cref="SecureBytes"/> (pinned, mlocked, zeroed
/// on dispose) — leaking it would compromise the ephemeral identity (and therefore every
/// ephemeral DEK wrap produced against the matching public key). Delegates the AES-256-GCM
/// framing to <see cref="SymmetricAeadWrap"/>.
/// </summary>
public static class InvitationCodePrivateKeyWrap
{
    private static readonly byte[] KekInfo =
        "plikshare-ephemeral-user-keypair-v1\0"u8.ToArray();

    public static byte[] Wrap(ReadOnlySpan<byte> invitationCodeBytes, ReadOnlySpan<byte> privateKey)
    {
        if (invitationCodeBytes.IsEmpty)
            throw new ArgumentException(
                "Invitation-code entropy must not be empty.",
                nameof(invitationCodeBytes));

        using var kek = DeriveKek(invitationCodeBytes);

        return kek.Use(
            state: privateKey,
            action: static (kekSpan, pkSpan) => SymmetricAeadWrap.Wrap(kekSpan, pkSpan));
    }

    /// <summary>
    /// Unwraps an ephemeral private key previously produced by <see cref="Wrap"/>. Returns a
    /// <see cref="SecureBytes"/> (pinned, mlocked, zeroed on dispose) that the caller
    /// MUST dispose. Throws on tag-verification failure (wrong invitation code or
    /// tampered ciphertext).
    /// </summary>
    public static SecureBytes Unwrap(ReadOnlySpan<byte> invitationCodeBytes, ReadOnlySpan<byte> wrapped)
    {
        if (invitationCodeBytes.IsEmpty)
            throw new ArgumentException(
                "Invitation-code entropy must not be empty.",
                nameof(invitationCodeBytes));

        using var kek = DeriveKek(invitationCodeBytes);

        return kek.Use(
            state: wrapped,
            action: static (kekSpan, wrappedSpan) => SymmetricAeadWrap.Unwrap(kekSpan, wrappedSpan));
    }

    private static SecureBytes DeriveKek(ReadOnlySpan<byte> invitationCodeBytes)
    {
        return SecureBytes.Create(
            length: SymmetricAeadWrap.KekSize,
            state: invitationCodeBytes,
            initializer: static (output, ikm) =>
            {
                HKDF.DeriveKey(
                    hashAlgorithmName: HashAlgorithmName.SHA256,
                    ikm: ikm,
                    output: output,
                    salt: [],
                    info: KekInfo);
            });
    }
}
