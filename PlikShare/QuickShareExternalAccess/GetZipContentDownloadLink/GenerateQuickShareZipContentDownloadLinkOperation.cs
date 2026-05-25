using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipContentDownloadLink;
using PlikShare.QuickShareExternalAccess.EffectiveSet;
using PlikShare.QuickShares.Cache;
using PlikShare.Storages.Zip;

namespace PlikShare.QuickShareExternalAccess.GetZipContentDownloadLink;

public class GenerateQuickShareZipContentDownloadLinkOperation(
    IsFileInQuickShareQuery isFileInQuickShareQuery,
    GetZipContentDownloadLinkOperation getZipContentDownloadLinkOperation)
{
    public Result Execute(
        QuickShareContext quickShare,
        FileExtId fileExternalId,
        ZipFileDto zipFile,
        ContentDispositionType contentDisposition,
        IUserIdentity userIdentity,
        CancellationToken cancellationToken)
    {
        if (!isFileInQuickShareQuery.Execute(quickShare, fileExternalId))
            return new Result(Code: ResultCode.FileNotInShare);

        var result = getZipContentDownloadLinkOperation.Execute(
            workspace: quickShare.Workspace,
            fileExternalId: fileExternalId,
            zipFile: zipFile,
            contentDisposition: contentDisposition,
            boxFolderId: null,
            boxLinkId: null,
            userIdentity: userIdentity,
            workspaceEncryptionSession: null,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetZipContentDownloadLinkOperation.ResultCode.Ok => new Result(
                Code: ResultCode.Ok,
                DownloadPreSignedUrl: result.DownloadPreSignedUrl),

            GetZipContentDownloadLinkOperation.ResultCode.FileNotFound => new Result(
                Code: ResultCode.FileNotFound),

            GetZipContentDownloadLinkOperation.ResultCode.WrongFileExtension => new Result(
                Code: ResultCode.WrongFileExtension),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetZipContentDownloadLinkOperation),
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
        WrongFileExtension
    }
}
