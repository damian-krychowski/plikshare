using PlikShare.BulkDownload;
using PlikShare.Folders.Id;
using PlikShare.QuickShareExternalAccess.Contracts;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.EffectiveSet;

namespace PlikShare.QuickShareExternalAccess.GetContent;

public class GetQuickShareContentOperation(
    GetQuickShareItemDbIdsQuery getItemDbIdsQuery,
    BulkDownloadDetailsQuery bulkDownloadDetailsQuery)
{
    public GetQuickShareContentResponseDto Execute(QuickShareContext quickShare)
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

        // Order so the client can wire parent refs in a single pass — every parent
        // appears before its children. Ancestor-chain length grows monotonically
        // top-down, so ordering by it suffices.
        var downloadFolders = details
            .FolderSubtree
            .GetDownloadFolders()
            .OrderBy(f => f.AncestorFolderIds.Length)
            .ToList();

        var folders = new List<QuickShareContentFolderDto>(downloadFolders.Count);

        foreach (var folder in downloadFolders)
        {
            FolderExtId? parentExternalId = null;

            if (!details.FolderSubtree.IsTopFolder(folder.Id) && folder.AncestorFolderIds.Length > 0)
            {
                var parent = details.FolderSubtree.TryGetDownloadFolder(
                    folder.AncestorFolderIds[^1]);

                parentExternalId = parent?.ExternalId;
            }

            folders.Add(new QuickShareContentFolderDto(
                ExternalId: folder.ExternalId,
                ParentExternalId: parentExternalId,
                Name: folder.Name));
        }

        var files = new List<QuickShareContentFileDto>(details.Files.Count);
        long totalSize = 0;

        foreach (var file in details.Files)
        {
            FolderExtId? folderExternalId = file.FolderId.HasValue
                ? details.FolderSubtree.TryGetDownloadFolder(file.FolderId.Value)?.ExternalId
                : null;

            files.Add(new QuickShareContentFileDto(
                ExternalId: file.ExternalId,
                FolderExternalId: folderExternalId,
                Name: file.Name,
                Extension: file.Extension,
                SizeInBytes: file.SizeInBytes));

            totalSize += file.SizeInBytes;
        }

        return new GetQuickShareContentResponseDto(
            Folders: folders,
            Files: files,
            TotalSizeInBytes: totalSize);
    }
}
