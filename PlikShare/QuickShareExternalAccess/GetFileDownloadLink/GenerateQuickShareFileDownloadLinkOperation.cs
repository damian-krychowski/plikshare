using PlikShare.BulkDownload;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.EffectiveSet;

namespace PlikShare.QuickShareExternalAccess.GetFileDownloadLink;

public class GenerateQuickShareFileDownloadLinkOperation(
    GetQuickShareItemDbIdsQuery getItemDbIdsQuery,
    BulkDownloadDetailsQuery bulkDownloadDetailsQuery,
    GetFileDownloadLinkOperation getFileDownloadLinkOperation)
{
    public async ValueTask<Result> Execute(
        QuickShareContext quickShare,
        FileExtId fileExternalId,
        ContentDispositionType contentDisposition,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        CancellationToken cancellationToken)
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

        var isInShare = details.Files.Any(f => f.ExternalId == fileExternalId);

        if (!isInShare)
            return new Result(ResultCode.FileNotInShare);

        var result = await getFileDownloadLinkOperation.Execute(
            workspace: quickShare.Workspace,
            fileExternalId: fileExternalId,
            contentDisposition: contentDisposition,
            boxFolderId: null,
            boxLinkId: null,
            userIdentity: userIdentity,
            enforceInternalPassThrough: enforceInternalPassThrough,
            workspaceEncryptionSession: null,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetFileDownloadLinkOperation.ResultCode.Ok => new Result(
                Code: ResultCode.Ok,
                DownloadPreSignedUrl: result.DownloadPreSignedUrl),

            GetFileDownloadLinkOperation.ResultCode.FileNotFound => new Result(
                Code: ResultCode.FileNotFound),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetFileDownloadLinkOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    public record Result(
        ResultCode Code,
        string? DownloadPreSignedUrl = null);

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        FileNotInShare
    }
}
