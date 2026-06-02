using PlikShare.BulkDownload;
using PlikShare.Files.Id;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.EffectiveSet;

namespace PlikShare.QuickShareExternalAccess.EffectiveSet;

// Resolves the share's effective file set (selected/excluded folders + files
// flattened to the actual reachable files) and checks whether a given file is
// inside it. The check walks the whole share to populate `details.Files`, so
// callers should run it once per request — not in a tight loop.
public class IsFileInQuickShareQuery(
    GetQuickShareItemDbIdsQuery getItemDbIdsQuery,
    BulkDownloadDetailsQuery bulkDownloadDetailsQuery)
{
    public bool Execute(
        QuickShareContext quickShare,
        FileExtId fileExternalId)
    {
        var dbIds = getItemDbIdsQuery.Execute(
            quickShareId: quickShare.Id);

        var details = bulkDownloadDetailsQuery.GetDetailsFromDb(
            selectedFileIds: dbIds.SelectedFileIds,
            excludedFileIds: dbIds.ExcludedFileIds,
            selectedFolderIds: dbIds.SelectedFolderIds,
            excludedFolderIds: dbIds.ExcludedFolderIds,
            workspace: quickShare.Workspace,
            workspaceEncryptionSession: null);

        return details.Files.Any(f => f.ExternalId == fileExternalId);
    }
}
