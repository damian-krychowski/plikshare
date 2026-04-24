using PlikShare.Core.Encryption;

namespace PlikShare.AuditLog;

/// <summary>
/// Read-side substitution: replaces encrypted-envelope names
/// (those that start with <see cref="EncryptableMetadataExtensions.ReservedPrefix"/>)
/// with the literal <see cref="EncryptedMarker"/> when serving audit log entries to the admin UI.
///
/// Audit records are written with whatever shape the source had — plaintext for non-encrypted
/// workspaces, <c>pse:…</c> envelopes for encrypted ones. Global admins (who view audit logs)
/// do not hold per-workspace DEKs and therefore cannot decrypt envelope names. The read path
/// applies this mask so the admin UI renders a stable "hidden by workspace encryption" sentinel
/// instead of raw ciphertext.
///
/// Never call this at audit write time — envelopes must reach <c>al_details</c> intact so that
/// future workspace-scoped readers (with the appropriate DEK) can still decrypt them.
/// </summary>
internal static class AuditNameMask
{
    public const string EncryptedMarker = "[encrypted]";

    public static string MaskIfEncrypted(string name) =>
        name.StartsWith(EncryptableMetadataExtensions.ReservedPrefix, StringComparison.Ordinal)
            ? EncryptedMarker
            : name;

    public static List<string> MaskIfEncrypted(List<string> names) =>
        names.ConvertAll(MaskIfEncrypted);
}
