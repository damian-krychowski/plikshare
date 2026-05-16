using PlikShare.BulkDownload;
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
            workspaceId: quickShare.Workspace.Id,
            selectedFileIds: dbIds.SelectedFileIds,
            excludedFileIds: dbIds.ExcludedFileIds,
            selectedFolderIds: dbIds.SelectedFolderIds,
            excludedFolderIds: dbIds.ExcludedFolderIds,
            storageClient: quickShare.Workspace.Storage,
            workspaceEncryptionSession: null);

        var files = new List<QuickShareContentFileDto>(details.Files.Count);
        long totalSize = 0;

        foreach (var file in details.Files)
        {
            var folderPath = details.FolderSubtree.GetPath(file.FolderId);

            var filePath = folderPath is null
                ? file.FullName
                : $"{folderPath}/{file.FullName}";

            files.Add(new QuickShareContentFileDto(
                ExternalId: file.ExternalId,
                FilePath: filePath,
                Name: file.Name,
                Extension: file.Extension,
                SizeInBytes: file.SizeInBytes));

            totalSize += file.SizeInBytes;
        }

        return new GetQuickShareContentResponseDto(
            Files: files,
            TotalSizeInBytes: totalSize);
    }
}
