using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Download;

public class GetFileDownloadLinkOperation(
    GetFileDetailsQuery getFileDetailsQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        ContentDispositionType contentDisposition,
        int? boxFolderId,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        CancellationToken cancellationToken)
    {
        var fileQueryResult = getFileDetailsQuery.Execute(
            workspaceId: workspace.Id,
            fileExternalId: fileExternalId,
            boxFolderId: boxFolderId);

        if (fileQueryResult.IsEmpty)
            return new Result(Code: ResultCode.FileNotFound);

        var preSignedUrl = await workspace
            .Storage
            .GetPreSignedDownloadFileLink(
                bucketName: workspace.BucketName,
                key: new S3FileKey
                {
                    FileExternalId = fileExternalId,
                    S3KeySecretPart = fileQueryResult.Value.S3KeySecretPart
                },
                contentType: fileQueryResult.Value.ContentType,
                fileName: fileQueryResult.Value.Name + fileQueryResult.Value.Extension,
                contentDisposition: contentDisposition,
                boxLinkId: boxLinkId,
                userIdentity: userIdentity,
                enforceInternalPassThrough: enforceInternalPassThrough,
                cancellationToken: cancellationToken);

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
        FileNotFound
    }
}