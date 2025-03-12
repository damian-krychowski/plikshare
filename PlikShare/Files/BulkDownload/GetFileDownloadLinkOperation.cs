using PlikShare.Core.Clock;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.BulkDownload;

public class GetBulkDownloadLinkOperation(
    IClock clock,
    PreSignedUrlsService preSignedUrlsService,
    GetBulkDownloadDetailsQuery getBulkDownloadDetailsQuery)
{
    public Result Execute(
        WorkspaceContext workspace,
        GetBulkDownloadLinkRequestDto request,
        IUserIdentity userIdentity,
        int? boxFolderId)
    {
        var downloadDetails = getBulkDownloadDetailsQuery.Execute(
            workspace: workspace,
            selectedFolderExternalIds: request.SelectedFolders,
            excludedFolderExternalIds: request.ExcludedFolders,
            selectedFileExternalIds: request.SelectedFiles,
            excludedFileExternalIds: request.ExcludedFiles,
            boxFolderId: boxFolderId);

        if (downloadDetails.SelectedFiles.Count != request.SelectedFiles.Count)
            return new Result(
                Code: ResultCode.FilesNotFound,
                NotFoundFileExternalIds: request
                    .SelectedFiles
                    .Except(downloadDetails.SelectedFiles.Select(f => f.ExternalId))
                    .ToList());

        if (downloadDetails.ExcludedFiles.Count != request.ExcludedFiles.Count)
            return new Result(
                Code: ResultCode.FilesNotFound,
                NotFoundFileExternalIds: request
                    .SelectedFiles
                    .Except(downloadDetails.SelectedFiles.Select(f => f.ExternalId))
                    .ToList());

        if (downloadDetails.SelectedFolders.Count != request.SelectedFolders.Count)
            return new Result(
                Code: ResultCode.FoldersNotFound,
                NotFoundFolderExternalIds: request
                    .SelectedFolders
                    .Except(downloadDetails.SelectedFolders.Select(f => f.ExternalId))
                    .ToList());

        if (downloadDetails.ExcludedFolders.Count != request.ExcludedFolders.Count)
            return new Result(
                Code: ResultCode.FoldersNotFound,
                NotFoundFolderExternalIds: request
                    .SelectedFolders
                    .Except(downloadDetails.SelectedFolders.Select(f => f.ExternalId))
                    .ToList());

        var preSignedUrl = preSignedUrlsService.GeneratePreSignedBulkDownloadUrl(
            new PreSignedUrlsService.BulkDownloadPayload
            {
                SelectedFileIds = downloadDetails.SelectedFiles.Select(f => f.Id).ToArray(),
                ExcludedFileIds = downloadDetails.ExcludedFiles.Select(f => f.Id).ToArray(),
                SelectedFolderIds = downloadDetails.SelectedFolders.Select(f => f.Id).ToArray(),
                ExcludedFolderIds = downloadDetails.ExcludedFolders.Select(f => f.Id).ToArray(),
                WorkspaceId = workspace.Id,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ExpirationDate = clock.UtcNow.Add(TimeSpan.FromMinutes(1))
            });

        return new Result(
            Code: ResultCode.Ok,
            PreSignedUrl: preSignedUrl);
    }

    public record Result(
        ResultCode Code,
        string? PreSignedUrl = default,
        List<FileExtId>? NotFoundFileExternalIds = default,
        List<FolderExtId>? NotFoundFolderExternalIds = default);
    
    public enum ResultCode
    {
        Ok = 0,
        FilesNotFound,
        FoldersNotFound
    }
}