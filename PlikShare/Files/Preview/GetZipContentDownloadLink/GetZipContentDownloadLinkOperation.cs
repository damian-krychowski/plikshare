using PlikShare.Core.Clock;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Zip;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Preview.GetZipContentDownloadLink;

public class GetZipContentDownloadLinkOperation(
    IClock clock,
    GetFileDetailsQuery getFileDetailsQuery,
    PreSignedUrlsService preSignedUrlsService)
{
    public Result Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        ZipFileDto zipFile,
        ContentDispositionType contentDisposition,
        int? boxFolderId,
        int? boxLinkId,
        IUserIdentity userIdentity,
        CancellationToken cancellationToken)
    {
        var (isEmpty, file) = getFileDetailsQuery.Execute(
            workspaceId: workspace.Id,
            fileExternalId: fileExternalId,
            boxFolderId: boxFolderId);

        if (isEmpty)
            return new Result(Code: ResultCode.FileNotFound);

        if(file.Extension != ".zip")
            return new Result(Code: ResultCode.WrongFileExtension);

        var preSignedUrl = preSignedUrlsService.GeneratePreSignedZipContentDownloadUrl(
            payload: new PreSignedUrlsService.ZipContentDownloadPayload
            {
                FileExternalId = file.ExternalId,
                ZipEntry = new PreSignedUrlsService.ZipEntryPayload
                {
                    FileName = GetFileName(zipFile),
                    OffsetToLocalFileHeader = zipFile.OffsetToLocalFileHeader,
                    CompressedSizeInBytes = zipFile.CompressedSizeInBytes,
                    SizeInBytes = zipFile.SizeInBytes,
                    FileNameLength = zipFile.FileNameLength,
                    CompressionMethod = zipFile.CompressionMethod,
                    IndexInArchive = zipFile.IndexInArchive
                },
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ExpirationDate = clock.UtcNow.AddMinutes(10),
                ContentDisposition = contentDisposition,
                BoxLinkId = boxLinkId
            });

        return new Result(
            Code: ResultCode.Ok,
            DownloadPreSignedUrl: preSignedUrl);
    }

    private static string GetFileName(ZipFileDto zf)
    {
        return zf.FilePath.Split("/", StringSplitOptions.RemoveEmptyEntries).Last();
    }

    public record Result(
        ResultCode Code,
        string? DownloadPreSignedUrl = default);

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound,
        WrongFileExtension
    }
}