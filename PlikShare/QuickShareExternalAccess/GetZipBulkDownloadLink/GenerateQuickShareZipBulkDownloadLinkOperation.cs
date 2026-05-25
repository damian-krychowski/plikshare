using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipBulkDownloadLink;
using PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts;
using PlikShare.QuickShareExternalAccess.EffectiveSet;
using PlikShare.QuickShares.Cache;

namespace PlikShare.QuickShareExternalAccess.GetZipBulkDownloadLink;

public class GenerateQuickShareZipBulkDownloadLinkOperation(
    IsFileInQuickShareQuery isFileInQuickShareQuery,
    GetZipBulkDownloadLinkOperation getZipBulkDownloadLinkOperation)
{
    public Result Execute(
        QuickShareContext quickShare,
        FileExtId fileExternalId,
        GetZipBulkDownloadLinkRequestDto request,
        IUserIdentity userIdentity)
    {
        if (!isFileInQuickShareQuery.Execute(quickShare, fileExternalId))
            return new Result(Code: ResultCode.FileNotInShare);

        var result = getZipBulkDownloadLinkOperation.Execute(
            workspace: quickShare.Workspace,
            fileExternalId: fileExternalId,
            request: request,
            boxFolderId: null,
            boxLinkId: null,
            userIdentity: userIdentity,
            workspaceEncryptionSession: null);

        return result.Code switch
        {
            GetZipBulkDownloadLinkOperation.ResultCode.Ok => new Result(
                Code: ResultCode.Ok,
                DownloadPreSignedUrl: result.DownloadPreSignedUrl),

            GetZipBulkDownloadLinkOperation.ResultCode.FileNotFound => new Result(
                Code: ResultCode.FileNotFound),

            GetZipBulkDownloadLinkOperation.ResultCode.WrongFileExtension => new Result(
                Code: ResultCode.WrongFileExtension),

            GetZipBulkDownloadLinkOperation.ResultCode.EmptySelection => new Result(
                Code: ResultCode.EmptySelection),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetZipBulkDownloadLinkOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    public record Result(
        ResultCode Code,
        string? DownloadPreSignedUrl = default);

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        FileNotInShare,
        WrongFileExtension,
        EmptySelection
    }
}
