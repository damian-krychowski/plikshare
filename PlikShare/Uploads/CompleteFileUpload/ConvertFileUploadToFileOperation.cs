using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Uploads.CompleteFileUpload;

public class ConvertFileUploadToFileOperation(
    FileUploadCache fileUploadCache,
    ConvertFileUploadToFileQuery convertFileUploadToFileQuery)
{
    public async ValueTask<Result> Execute(
        WorkspaceContext workspace,
        FileUploadExtId fileUploadExternalId,
        IUserIdentity userIdentity,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var fileUpload = await fileUploadCache.GetFileUpload(
            uploadExternalId: fileUploadExternalId,
            cancellationToken: cancellationToken);

        if (fileUpload is null || !fileUpload.HasUserRight(workspace.Id, userIdentity))
        {
            Log.Warning(
                "Could not convert FileUpload '{FileUploadExternalId}' to File because FileUpload was not found. ",
                fileUploadExternalId);

            return new Result(
                Code: ResultCode.FileUploadNotFound);
        }
        
        var result = await convertFileUploadToFileQuery.Execute(
            fileUpload: new ConvertFileUploadToFileQuery.FileUpload(
                Id: fileUpload.Id,
                UploadAlgorithm: fileUpload.UploadAlgorithm,
                FileSizeInBytes: fileUpload.FileToUpload.SizeInBytes),
            workspace: fileUpload.Workspace,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        if(result.Code == ConvertFileUploadToFileQuery.ResultCode.FileUploadNotYetCompleted)
            return new Result(
                Code: ResultCode.FileUploadNotYetCompleted);

        await fileUploadCache.Invalidate(
            uploadExternalId: fileUpload.ExternalId,
            cancellationToken: cancellationToken);

        return new Result(
            Code: ResultCode.Ok,
            FileExternalId: fileUpload.FileToUpload.S3FileKey.FileExternalId);
    }

    public readonly record struct Result(
        ResultCode Code,
        FileExtId FileExternalId = default);

    public enum ResultCode
    {
        Ok = 0,
        FileUploadNotFound,
        FileUploadNotYetCompleted
    }
}