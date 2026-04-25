using PlikShare.AuditLog.Details;
using PlikShare.Core.Encryption;
using PlikShare.Folders.Create;
using PlikShare.Folders.Id;

namespace PlikShare.AuditLog;

static class GetOrCreateFolderQueryAuditLogExtensions
{
    extension(GetOrCreateFolderQuery.Folder folder)
    {
        /// <summary>
        /// Names on <see cref="GetOrCreateFolderQuery.Folder"/> are decrypted plaintext
        /// (the query pulls them through the session-aware deserializer so the query's
        /// user-facing response can render them). For encrypted workspaces we must re-wrap
        /// them before they enter <c>al_details</c> so the audit log never carries plaintext
        /// for a workspace whose contents are meant to be hidden from global admins.
        ///
        /// The re-encryption uses the session's latest DEK and a fresh nonce, so the produced
        /// envelope is byte-different from the one stored in <c>fo_name</c> — that is fine:
        /// the audit log doesn't need byte-equality, only that the plaintext stays inside an
        /// envelope a workspace-member-scoped reader could still decrypt.
        ///
        /// When <paramref name="workspaceEncryptionSession"/> is <c>null</c> (non-encrypted
        /// workspace or box external access) <see cref="EncryptableMetadataExtensions.Encode"/>
        /// is a plaintext passthrough, so this call is a no-op cost-wise.
        /// </summary>
        public Audit.FolderRef ToAuditLogFolderRef(
            WorkspaceEncryptionSession? workspaceEncryptionSession) => new()
        {
            ExternalId = FolderExtId.Parse(folder.ExternalId),
            Name = workspaceEncryptionSession.Encode(folder.Name),
            FolderPath = folder.Ancestors.Length == 0
                ? null
                : folder.Ancestors
                    .Select(a => workspaceEncryptionSession.Encode(a.Name))
                    .ToList()
        };
    }

    extension(IEnumerable<GetOrCreateFolderQuery.Folder>? folders)
    {
        public List<Audit.FolderRef> ToAuditLogFolderRefs(
            WorkspaceEncryptionSession? workspaceEncryptionSession) => folders
            ?.Select(f => f.ToAuditLogFolderRef(workspaceEncryptionSession))
            .ToList() ?? [];
    }
}