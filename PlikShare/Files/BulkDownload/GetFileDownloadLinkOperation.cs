using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.BulkDownload;

public class GetBulkDownloadLinkOperation(
    IMasterDataEncryption masterDataEncryption,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService,
    GetBulkDownloadDetailsQuery getBulkDownloadDetailsQuery)
{
    public Result Execute(
        WorkspaceContext workspace,
        GetBulkDownloadLinkRequestDto request,
        IUserIdentity userIdentity,
        int? boxFolderId,
        int? boxLinkId,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var selectedFolders = request.SelectedFolders ?? [];
        var selectedFiles = request.SelectedFiles ?? [];
        var excludedFolders = request.ExcludedFolders ?? [];
        var excludedFiles = request.ExcludedFiles ?? [];

        var downloadDetails = getBulkDownloadDetailsQuery.Execute(
            workspace: workspace,
            selectedFolderExternalIds: selectedFolders,
            excludedFolderExternalIds: excludedFolders,
            selectedFileExternalIds: selectedFiles,
            excludedFileExternalIds: excludedFiles,
            boxFolderId: boxFolderId);

        if (downloadDetails.SelectedFiles.Count != selectedFiles.Count)
            return new Result(
                Code: ResultCode.FilesNotFound,
                NotFoundFileExternalIds: selectedFiles
                    .Except(downloadDetails.SelectedFiles.Select(f => f.ExternalId.Value))
                    .ToList());

        if (downloadDetails.ExcludedFiles.Count != excludedFiles.Count)
            return new Result(
                Code: ResultCode.FilesNotFound,
                NotFoundFileExternalIds: selectedFiles
                    .Except(downloadDetails.SelectedFiles.Select(f => f.ExternalId.Value))
                    .ToList());

        if (downloadDetails.SelectedFolders.Count != selectedFolders.Count)
            return new Result(
                Code: ResultCode.FoldersNotFound,
                NotFoundFolderExternalIds: selectedFolders
                    .Except(downloadDetails.SelectedFolders.Select(f => f.ExternalId.Value))
                    .ToList());

        if (downloadDetails.ExcludedFolders.Count != excludedFolders.Count)
            return new Result(
                Code: ResultCode.FoldersNotFound,
                NotFoundFolderExternalIds: selectedFolders
                    .Except(downloadDetails.SelectedFolders.Select(f => f.ExternalId.Value))
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
                ExpirationDate = clock.UtcNow.Add(TimeSpan.FromMinutes(1)),
                BoxLinkId = boxLinkId,
                WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption)
            });

        return new Result(
            Code: ResultCode.Ok,
            PreSignedUrl: preSignedUrl);
    }

    public record Result(
        ResultCode Code,
        string? PreSignedUrl = default,
        List<string>? NotFoundFileExternalIds = default,
        List<string>? NotFoundFolderExternalIds = default);
    
    public enum ResultCode
    {
        Ok = 0,
        FilesNotFound,
        FoldersNotFound
    }
}