using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Wraps a plaintext Workspace DEK with a symmetric KEK derived from invitation-code
/// entropy bytes. Used at invite-time to stage a DEK for a brand-new invitee whose
/// public key does not yet exist: the owner (unlocked) produces the wrap, the invitee
/// later unwraps it during encryption-password setup using the invitation code they
/// received by email.
///
/// Callers pass the raw invitation-code entropy (decoded from whatever transport
/// representation they use — Base62 is a transport detail that stops at the boundary).
/// HKDF-SHA256 derives a 32-byte KEK from those bytes; no slow KDF because invitation
/// codes are expected to carry full-entropy random bytes.
///
/// The derived KEK never leaves a <see cref="SecureBytes"/> (pinned, mlocked, zeroed
/// on dispose) — leaking it would compromise every ephemeral wrap produced under the
/// same invitation code. Delegates the AES-256-GCM framing to <see cref="SymmetricAeadWrap"/>.
/// </summary>
public static class InvitationCodeDekWrap
{
    private static readonly byte[] KekInfo =
        "plikshare-ephemeral-wek-v1\0"u8.ToArray();

    public static byte[] Wrap(ReadOnlySpan<byte> invitationCodeBytes, ReadOnlySpan<byte> dek)
    {
        if (invitationCodeBytes.IsEmpty)
            throw new ArgumentException(
                "Invitation-code entropy must not be empty.",
                nameof(invitationCodeBytes));

        using var kek = DeriveKek(invitationCodeBytes);

        return kek.Use(
            state: dek,
            action: static (kekSpan, dekSpan) => SymmetricAeadWrap.Wrap(kekSpan, dekSpan));
    }

    /// <summary>
    /// Unwraps a DEK previously produced by <see cref="Wrap"/>. Returns a
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
