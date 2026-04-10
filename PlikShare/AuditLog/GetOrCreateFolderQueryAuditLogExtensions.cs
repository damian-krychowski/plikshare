using PlikShare.AuditLog.Details;
using PlikShare.Folders.Create;
using PlikShare.Folders.Id;

namespace PlikShare.AuditLog;

static class GetOrCreateFolderQueryAuditLogExtensions
{
    extension(GetOrCreateFolderQuery.Folder folder)
    {
        public Audit.FolderRef ToAuditLogFolderRef() => new()
        {
            ExternalId = FolderExtId.Parse(folder.ExternalId),
            Name = folder.Name,
            FolderPath = folder.Ancestors.Length == 0
                ? null
                : string.Join("/", folder.Ancestors.Select(a => a.Name))
        };
    }

    extension(IEnumerable<GetOrCreateFolderQuery.Folder>? folders)
    {
        public List<Audit.FolderRef> ToAuditLogFolderRefs() => folders
            ?.Select(f => f.ToAuditLogFolderRef())
            .ToList() ?? [];
    }
}