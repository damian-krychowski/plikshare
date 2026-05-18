using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Preview.GetZipBulkDownloadLink;

public class GetZipBulkDownloadLinkOperation(
    IMasterDataEncryption masterDataEncryption,
    IClock clock,
    GetFileDetailsQuery getFileDetailsQuery,
    PreSignedUrlsService preSignedUrlsService)
{
    public Result Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        GetZipBulkDownloadLinkRequestDto request,
        int? boxFolderId,
        int? boxLinkId,
        IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var (isEmpty, file) = getFileDetailsQuery.Execute(
            workspaceId: workspace.Id,
            fileExternalId: fileExternalId,
            boxFolderId: boxFolderId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (isEmpty)
            return new Result(Code: ResultCode.FileNotFound);

        if (file.Extension != ".zip")
            return new Result(Code: ResultCode.WrongFileExtension);

        // Empty selection would produce a zero-entry zip; reject it at the link
        // stage so the client cannot accidentally generate a useless download URL.
        if (request.SelectedFolderIds.Length == 0 && request.SelectedEntryIndices.Length == 0)
            return new Result(Code: ResultCode.EmptySelection);

        var preSignedUrl = preSignedUrlsService.GeneratePreSignedZipBulkDownloadUrl(
            payload: new PreSignedUrlsService.ZipBulkDownloadPayload
            {
                FileExternalId = file.ExternalId,
                SelectedFolderIds = request.SelectedFolderIds,
                SelectedEntryIndices = request.SelectedEntryIndices,
                ExcludedFolderIds = request.ExcludedFolderIds,
                ExcludedEntryIndices = request.ExcludedEntryIndices,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ExpirationDate = clock.UtcNow.AddMinutes(10),
                BoxLinkId = boxLinkId,
                WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption)
            });

        return new Result(
            Code: ResultCode.Ok,
            DownloadPreSignedUrl: preSignedUrl);
    }

    public record Result(
        ResultCode Code,
        string? DownloadPreSignedUrl = default);

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        WrongFileExtension,
        EmptySelection
    }
}
